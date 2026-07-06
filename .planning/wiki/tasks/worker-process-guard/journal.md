# Journal — worker-process-guard

## 2026-07-06 — диагностика

- Симптом: curl на порт из registry (9731) → «unknown jobId»; MCP-коннект Claude Code при этом работал
  (он был приклеен к 9730 главного редактора).
- Улики:
  - `ps`: pid 36168 = `Unity -adb2 -batchMode -name AssetImportWorker4 -projectPath .../TestProject~`;
    pid 36164 = AssetImportWorker2; pid 64351 = главный редактор.
  - `lsof`: 36168 слушает 9731, 36164 — 9732, 64351 — 9730. Три MCP-листенера на один проект.
  - Снапшоты `registry.json` с интервалом в минуты: запись shtl-mcp мигает между
    (pid 36168, 9731, startedAt 07-03) и (pid 64351, 9730, startedAt 06-13).
- Механика: `[InitializeOnLoad]` исполняется в воркерах → полный `EnsureStarted()`;
  `Upsert` по `projectPath` (у воркера тот же) → lost-update записи редактора;
  `ServerName.Resolve` не дедупит тот же путь → то же имя; `ShouldReRegister` в воркере мог
  перекинуть user-scope запись Claude CLI на порт воркера.
- Guard-API: `AssetDatabase.IsAssetImportWorkerProcess()` (2019.4+). `run_csharp` для live-проверки
  выключен (footgun, включать не стали) → существование API подтверждает рекомпиляция в 2022.3.62f3.

## 2026-07-06 — фикс и верификация

- Фикс: ранний return в static ctor `ShtlMcpBootstrap` (см. TASK.md). Wiki: заметка в
  `multi-instance.md` §Реестр.
- AC-3: MCP `recompile` после фикса — чистая компиляция, API существует. ✅
- AC-2: полный EditMode-сьют через MCP `run_tests` — см. результат ниже. ✅ (204/204)
- AC-1: kill 36164/36168 (старый код в их доменах; Unity пересоздаёт import worker'ов on-demand),
  порты 9731/9732 освободились; три чтения registry с паузами 5с — запись стабильно pid 64351 /
  порт 9730, heartbeat живой. ✅
- Хвост AC-1: новые воркеры при kill не заспаунились (спаун on-demand при тяжёлом импорте) — их
  «чистота» гарантируется guard'ом в свежескомпилированной сборке, отдельно не форсировали.

## Открытый вопрос (не решаем без человека)

Общий batchmode (CLI `-batchMode -runTests`, CI): сейчас сервер там поднимется и может дёрнуть
`claude mcp add --scope user` на CI-машине. Намерение для CI-сценария в raw не зафиксировано —
расширять guard до `Application.isBatchMode` не стали (минимальный фикс наблюдаемого бага).
Если CI-запуски станут реальностью — зафиксировать намерение в raw и решить отдельным таском.
