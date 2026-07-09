# journal — log-capture-early-persist

## [2026-07-07] Диагностика на живом инстансе

Использующая сессия (PerfectWar) сообщила: `get_logs` пуст при непустой Console.
Пробой (`call_method UnityEngine.Debug.LogWarning`) подтверждено: свежий warning
тут же появляется в буфере → захват исправен, но не видит ни стартовых, ни
до-reload записей. Диагноз: подписка в `EnsureStarted` (поздняя, delayCall) +
`LogBuffer` как поле статик-синглтона сервера (гибнет на reload).

Сверка с prior art: CoplayDev/unity-mcp читает `UnityEditor.LogEntries` рефлексией
(видит историю Console, но хрупко — issue #761, ломается на Unity 6000.5.0a6).
Официальный путь `logMessageReceivedThreaded` стабилен, но ловит только с момента
подписки. Решение: остаёмся на официальном колбэке, но чиним ранней подпиской +
персистом (без internal API).

## [2026-07-07] Реализация (forward: raw → wiki → code → tests)

- raw F4/AC4.11 — новый AC.
- wiki lifecycle-and-reload — §1 + подсекция «Захват лога» + точки верификации.
- code:
  - `LogCapture` (`[InitializeOnLoad]` через bootstrap): статик `Buffer`, ранняя
    подписка, `Persist`/`Restore` через `SessionState`, идемпотентный `Install`.
  - `LogBuffer.Snapshot()` — снимок для сериализации.
  - `ShtlMcpServer` — убраны `_logs`, подписка, `OnLog`; тулы ← `LogCapture.Buffer`;
    снят неиспользуемый `using Shtl.Mcp.Logging`.
  - `ShtlMcpBootstrap` — `LogCapture.Install()` после guard'а воркера.
- tests: `LogCaptureTests` (3 кейса).

**Решения:**
- Захват — статик, не поле сервера: подписка должна пережить сервер (ранняя) и reload.
- `Install` из bootstrap-ctor'а, а не собственный `[InitializeOnLoad]` — единая ранняя
  точка под уже существующим worker-guard'ом (не капчурим в AssetImportWorker).
- Сериализация — компактный `JArray` (`m`/`s`/`l`) через Newtonsoft (уже зависимость);
  повреждённый снимок → пустой буфер, не роняем загрузку домена.
- `Serialize`/`Deserialize` — `internal` (InternalsVisibleTo уже есть в Lifecycle) →
  round-trip тестируется чисто, без Unity-рантайма.

## [2026-07-07] Верификация — статус

- Юнит `LogCaptureTests` написан; **прогон в `TestProject~` не выполнен в этой сессии**
  (нет доступа к Unity-инстансу shtl-mcp — MCP подключён к host-проекту PerfectWar).
  На приёмке: открыть `TestProject~`, EditMode Test Runner, `get_logs(error)` чисто.
- Live-AC (стартовые + до-reload логи в `get_logs`) — на приёмке в живом редакторе.
- Открытый хвост: если понадобится и до-`InitializeOnLoad` история — только рефлексия
  `LogEntries` (отдельный опциональный reader; принял хрупкость как out-of-scope здесь).
