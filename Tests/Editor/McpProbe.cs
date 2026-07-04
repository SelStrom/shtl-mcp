using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Shtl.Mcp.Editor.Tests
{
    /// Минимальный HTTP JSON-RPC клиент для reload-тестов: дёргает `tools/call status`
    /// у in-process листенера и достаёт projectName.
    ///
    /// ⚠️ Вызывать ТОЛЬКО с фонового потока (через Task). `status.NeedsMainThread == true`:
    /// синхронный запрос из главного потока теста повесит редактор (listener-поток уходит в
    /// RunOnMain и ждёт дрейн диспетчера на EditorApplication.update, который не тикнет, пока
    /// главный поток заблокирован на сокете). Тест должен `yield`-ить, пока Task не завершится.
    internal static class McpProbe
    {
        /// POST status на http://127.0.0.1:port/. Тело ответа или исключение при сетевой ошибке
        /// (например, connection refused, пока листенер лежит) — гоняется на пуле потоков.
        public static Task<string> CallStatusAsync(int port) => CallToolAsync(port, "status", null);

        /// POST tools/call произвольного инструмента. Тело ответа или исключение — как CallStatusAsync.
        public static Task<string> CallToolAsync(int port, string name, JObject arguments)
        {
            var payload = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "tools/call",
                ["params"] = new JObject { ["name"] = name, ["arguments"] = arguments ?? new JObject() }
            }.ToString(Newtonsoft.Json.Formatting.None);
            return Task.Run(() =>
            {
                var req = (HttpWebRequest)WebRequest.Create($"http://127.0.0.1:{port}/");
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Proxy = null;          // без системного proxy-lookup (латентность/зависания)
                req.KeepAlive = false;     // чистое закрытие на каждую пробу при polling
                req.Timeout = 4000;
                req.ReadWriteTimeout = 4000;

                var bytes = Encoding.UTF8.GetBytes(payload);
                using (var s = req.GetRequestStream())
                {
                    s.Write(bytes, 0, bytes.Length);
                }
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    return rd.ReadToEnd();
                }
            });
        }

        /// Полезная нагрузка любого инструмента из result.content[0].text (алиас TryGetStatus —
        /// формат ответа один для всех tools/call).
        public static bool TryGetToolPayload(string respJson, out JObject payload)
            => TryGetStatus(respJson, out payload);

        /// Распарсить вложенный JSON статуса из result.content[0].text (тело status-инструмента).
        /// false, если ответ не похож на успешный status round-trip.
        public static bool TryGetStatus(string respJson, out JObject status)
        {
            status = null;
            try
            {
                var o = JObject.Parse(respJson);
                var text = (string)o["result"]?["content"]?[0]?["text"];
                if (string.IsNullOrEmpty(text))
                {
                    return false;
                }
                status = JObject.Parse(text);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// projectName из статуса. false, если ответ не похож на успешный status round-trip.
        public static bool TryGetProjectName(string respJson, out string projectName)
        {
            projectName = null;
            if (!TryGetStatus(respJson, out var status))
            {
                return false;
            }
            projectName = (string)status["projectName"];
            return !string.IsNullOrEmpty(projectName);
        }
    }
}
