using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Shtl.Mcp.Lifecycle;

namespace Shtl.Mcp.Editor.Tests
{
    /// RED-gate: MCP-листенер переживает domain reload и оживает после смерти листенера.
    /// Проверка — реальным JSON-RPC `status` round-trip (поведение, не приватное состояние).
    ///
    /// Механизм reload-survival (UTF 1.1.33): EditMode [UnityTest] возобновляется на той же строке
    /// после reload (раннер сериализует program counter итератора). Восстанавливается ТОЛЬКО позиция —
    /// локалы обнуляются, поэтому всё нужное после reload держим в [SerializeField]-полях fixture
    /// (их раннер сериализует через JsonUtility).
    ///
    /// Вложенные энумераторы (PollStatus) пампим ВРУЧНУЮ на верхнем уровне
    /// (while (e.MoveNext()) { yield return e.Current; }): EditMode-раннер не гарантирует авто-нестинг
    /// `yield return IEnumerator`, но понимает `null` и IEditModeTestYieldInstruction (WaitForDomainReload).
    public class ReloadSurvivalTests
    {
        // Переживают domain reload (JsonUtility-сериализуемые поля fixture). НЕ static, НЕ property.
        [SerializeField] int _port;
        [SerializeField] string _project;
        [SerializeField] int _reloadBefore;

        /// yield-poll: крутим status round-trip, пока не вернёт ожидаемый projectName или не выйдет
        /// таймаут. HTTP идёт на фоновом Task; `yield return null` отдаёт кадр главному потоку, чтобы
        /// он дренил диспетчер (иначе deadlock — см. McpProbe). result[0] = успех. Только yield null.
        static IEnumerator PollStatus(int port, string expectedProject, double timeoutSec, bool[] result)
        {
            result[0] = false;
            double deadline = EditorApplication.timeSinceStartup + timeoutSec;
            while (EditorApplication.timeSinceStartup < deadline)
            {
                Task<string> call = McpProbe.CallStatusAsync(port);
                while (!call.IsCompleted)
                {
                    yield return null;
                }

                if (call.Status == TaskStatus.RanToCompletion)
                {
                    if (McpProbe.TryGetProjectName(call.Result, out var name) &&
                        (expectedProject == null || name == expectedProject))
                    {
                        result[0] = true;
                        yield break;
                    }
                }
                else
                {
                    _ = call.Exception; // observe faulted task (connection refused, пока листенер лежит)
                }

                yield return null; // пауза кадр и повтор
            }
        }

        [UnityTest]
        public IEnumerator Watchdog_Rebinds_AfterListenerDeath()
        {
            // arrange: сервер поднят бутстрапом; гарантируем и берём порт
            ShtlMcpServer.Instance.EnsureStarted();
            Assert.That(ShtlMcpServer.Instance.IsListening, Is.True, "сервер должен слушать до теста");
            int port = ShtlMcpServer.Instance.Port;

            var ok = new bool[1];
            var before = PollStatus(port, null, 5, ok);
            while (before.MoveNext())
            {
                yield return before.Current;
            }
            Assert.That(ok[0], Is.True, $"status должен отвечать на {port} до убийства листенера");

            // act: убить листенер и поднять через watchdog (его ветка: if (!IsListening) EnsureStarted())
            ShtlMcpServer.Instance.StopListenerForReload();
            ShtlMcpServer.Instance.WatchdogTick();

            // assert: снова отвечает на ТОМ ЖЕ порту (poll поглощает TIME_WAIT и повторы фонового watchdog)
            var after = PollStatus(port, null, 10, ok);
            while (after.MoveNext())
            {
                yield return after.Current;
            }
            Assert.That(ok[0], Is.True, $"watchdog должен поднять листенер на том же порту {port}");
        }

        [UnityTest]
        [Category("DomainReload")]
        public IEnumerator Listener_Respawns_AfterDomainReload()
        {
            // arrange: захватить порт + projectName в сериализуемые поля (переживут reload)
            ShtlMcpServer.Instance.EnsureStarted();
            Assert.That(ShtlMcpServer.Instance.IsListening, Is.True, "сервер должен слушать до reload");
            _port = ShtlMcpServer.Instance.Port;

            var ok = new bool[1];
            var probe = PollStatus(_port, null, 5, ok);
            while (probe.MoveNext())
            {
                yield return probe.Current;
            }
            Assert.That(ok[0], Is.True, "status должен отвечать до reload");

            // зафиксировать ожидаемый projectName (для сверки идентичности инстанса после reload)
            Task<string> idCall = McpProbe.CallStatusAsync(_port);
            while (!idCall.IsCompleted)
            {
                yield return null;
            }
            Assert.That(idCall.Status, Is.EqualTo(TaskStatus.RanToCompletion), "проба идентичности должна пройти");
            Assert.That(McpProbe.TryGetStatus(idCall.Result, out var statusBefore), Is.True);
            _project = (string)statusBefore["projectName"];
            _reloadBefore = (int)statusBefore["reloadCount"];
            Assert.That(_project, Is.Not.Null.And.Not.Empty);

            // act: форсировать domain reload. Тест возобновится ПОСЛЕ reload на следующей строке;
            // локалы (port, ok, probe, idCall) к этому моменту потеряны, _port/_project — восстановлены из JSON.
            EditorUtility.RequestScriptReload();
            yield return new WaitForDomainReload();

            // assert: бутстрап ([InitializeOnLoad]/afterAssemblyReload/watchdog) должен заново поднять
            // листенер на том же порту. Без re-spawn логики проба не пройдёт → тест красный.
            var after = new bool[1];
            var post = PollStatus(_port, _project, 10, after);
            while (post.MoveNext())
            {
                yield return post.Current;
            }
            Assert.That(after[0], Is.True,
                $"листенер должен заново подняться на {_port} с projectName={_project} после domain reload");

            // AC4.7: reloadCount наблюдаемо вырос после пережитого reload (re-spawn виден по live-каналу)
            Task<string> postCall = McpProbe.CallStatusAsync(_port);
            while (!postCall.IsCompleted)
            {
                yield return null;
            }
            Assert.That(McpProbe.TryGetStatus(postCall.Result, out var statusAfter), Is.True);
            Assert.That((int)statusAfter["reloadCount"], Is.GreaterThan(_reloadBefore),
                $"reloadCount должен вырасти после domain reload (было {_reloadBefore})");
        }
    }
}
