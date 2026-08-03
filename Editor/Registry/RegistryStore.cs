using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Shtl.Mcp.Registry
{
    public sealed class RegistryStore
    {
        readonly string _file;
        static readonly JsonSerializerSettings S = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc
        };

        public RegistryStore(string file) => _file = file;

        public List<InstanceEntry> Read()
        {
            try
            {
                if (!File.Exists(_file))
                {
                    return new List<InstanceEntry>();
                }
                var json = File.ReadAllText(_file);
                return JsonConvert.DeserializeObject<List<InstanceEntry>>(json, S) ?? new List<InstanceEntry>();
            }
            catch
            {
                return new List<InstanceEntry>();
            }
        }

        public void Upsert(InstanceEntry e) => WithLock(() =>
        {
            var list = Read();
            list.RemoveAll(x => x.ProjectPath == e.ProjectPath);
            list.Add(e);
            WriteAtomic(list);
        });

        public void Remove(string projectPath) => WithLock(() =>
        {
            var list = Read();
            list.RemoveAll(x => x.ProjectPath == projectPath);
            WriteAtomic(list);
        });

        // Межпроцессная сериализация read-modify-write: эксклюзивный lock-файл рядом с реестром. Несколько
        // Unity-инстансов пишут в ОДИН registry.json (фон-heartbeat) → без этого теряются записи (lost update).
        // Ретраим на IOException (чужой держит lock); бросаем работу молча по исчерпанию — heartbeat повторится.
        void WithLock(Action mutate)
        {
            var lockPath = _file + ".lock";
            Directory.CreateDirectory(Path.GetDirectoryName(_file));
            for (int attempt = 0; attempt < 50; attempt++)
            {
                FileStream fs;
                try
                {
                    fs = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(5); // чужой инстанс держит lock — короткий backoff
                    continue;
                }
                try
                {
                    mutate();
                    return;
                }
                finally
                {
                    fs.Dispose();
                }
            }
        }

        public string LivePathForName(string serverName, TimeSpan ttl)
        {
            var now = DateTime.UtcNow;
            return Read().FirstOrDefault(x =>
                x.ServerName == serverName && now - x.LastHeartbeat <= ttl)?.ProjectPath;
        }

        /// Имя, закреплённое за путём прошлыми запусками. Без TTL: запись переживает остановку Unity
        /// (записи не удаляются), поэтому имя инстанса не зависит от того, кто сейчас поднят.
        public string NameForPath(string projectPath)
        {
            return Read().FirstOrDefault(x => x.ProjectPath == projectPath)?.ServerName;
        }

        public static List<InstanceEntry> Prune(List<InstanceEntry> list, DateTime now, TimeSpan ttl)
            => list.Where(x => now - x.LastHeartbeat <= ttl).ToList();

        void WriteAtomic(List<InstanceEntry> list)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file));
            var tmp = _file + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(list, S));
            if (File.Exists(_file))
            {
                File.Replace(tmp, _file, null);
            }
            else
            {
                File.Move(tmp, _file);
            }
        }
    }
}
