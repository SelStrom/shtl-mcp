using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;

namespace Shtl.Mcp.Jobs
{
    /// Реестр async-job: in-memory `Dictionary` (потокобезопасный) + персист в SessionState
    /// (переживает domain reload в пределах сессии Editor).
    ///
    /// Потоковая модель: мутации (Create/Complete/Fail) трогают SessionState → ТОЛЬКО главный поток
    /// (вызываются из async-tool на главном потоке через Dispatcher). Get читает in-memory без Unity API
    /// → потокобезопасен, можно с фонового HTTP-потока (опрос job не зависит от тика главного потока).
    public sealed class JobStore
    {
        const string DefaultKey = "Shtl.Mcp.Jobs";

        readonly string _key;
        readonly object _lock = new object();
        readonly Dictionary<string, Job> _jobs = new Dictionary<string, Job>();

        // sessionKey инъектируется в тестах: один SessionState на редактор, и тест с дефолтным ключом
        // затирал бы live-job работающего сервера. Изолированный ключ убирает коллизию (важно для
        // self-test полного сьюта, где ReloadSurvivalTests форсит reload — live-job не должен пропасть).
        public JobStore(string sessionKey = DefaultKey)
        {
            _key = sessionKey;
            Load();
        }

        /// Создать job в статусе running. Возвращает jobId. Главный поток.
        public string Create(string tool)
        {
            var job = new Job
            {
                Id = Guid.NewGuid().ToString("N"),
                Tool = tool,
                Status = "running",
                StartedAtTicks = DateTime.UtcNow.Ticks
            };
            lock (_lock)
            {
                _jobs[job.Id] = job;
            }
            Persist();
            return job.Id;
        }

        /// Пометить job завершённым с результатом (JSON-строка). Главный поток. Только running → done
        /// (идемпотентно: повторная финализация не перезатрёт терминальный статус).
        public void Complete(string id, string resultJson)
        {
            lock (_lock)
            {
                if (_jobs.TryGetValue(id, out var j) && j.Status == "running")
                {
                    j.Status = "done";
                    j.Result = resultJson;
                }
            }
            Persist();
        }

        /// Пометить job упавшим с текстом ошибки. Главный поток. Только running → failed.
        public void Fail(string id, string error)
        {
            lock (_lock)
            {
                if (_jobs.TryGetValue(id, out var j) && j.Status == "running")
                {
                    j.Status = "failed";
                    j.Error = error;
                }
            }
            Persist();
        }

        /// Обновить прогресс running-job (in-memory, БЕЗ персиста — транзиентно). Главный поток (из колбэков).
        public void SetProgress(string id, int completed, int total, string current)
        {
            lock (_lock)
            {
                if (_jobs.TryGetValue(id, out var j) && j.Status == "running")
                {
                    j.Completed = completed;
                    j.Total = total;
                    j.CurrentTest = current;
                }
            }
        }

        /// Снимок job по id или null. Потокобезопасен (без Unity API).
        public Job Get(string id)
        {
            lock (_lock)
            {
                return _jobs.TryGetValue(id, out var j) ? j : null;
            }
        }

        void Persist()
        {
            string json;
            lock (_lock)
            {
                json = JsonConvert.SerializeObject(_jobs);
            }
            SessionState.SetString(_key, json); // главный поток
        }

        void Load()
        {
            var json = SessionState.GetString(_key, "");
            if (string.IsNullOrEmpty(json))
            {
                return;
            }
            try
            {
                var loaded = JsonConvert.DeserializeObject<Dictionary<string, Job>>(json);
                if (loaded != null)
                {
                    lock (_lock)
                    {
                        foreach (var kv in loaded)
                        {
                            _jobs[kv.Key] = kv.Value;
                        }
                    }
                }
            }
            catch
            {
                // повреждённый стейт — игнорируем, начинаем чисто
            }
        }
    }
}
