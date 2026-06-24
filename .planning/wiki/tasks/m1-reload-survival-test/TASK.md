# TASK: M1 reload-survival автотест (RED-gate)

**Status:** done (оба теста зелёные; RED-gate подтверждён — на сломанном re-spawn оба краснеют)
**Привязка:** raw F4 (play-edit-reload, AC4.1 re-spawn вокруг reload), F2 (watchdog-выживание,
AC2.2), F3 (`status` как проба пайплайна). Системы: `wiki/systems/lifecycle-and-reload.md`.
Закрывает оставшийся пункт приёмки `wiki/tasks/m1-walking-skeleton/TASK.md` («интерактивная
приёмка reload — за пользователем»), переводя его из ручного в автоматический RED-gate.

## Контекст (зачем)

M1 поставил re-spawn-машинерию (`ShtlMcpBootstrap`: `beforeAssemblyReload→StopListenerForReload`,
`afterAssemblyReload→Init→EnsureStarted`, watchdog `Tick` 1с), но **без автотеста** — все 34
EditMode-теста это чистая логика. Ручная проверка через `status`+`get_logs` **структурно
неспособна** подтвердить re-spawn: `Uptime` durable (считается от `SessionState` `StartedKey`,
ставится один раз за сессию — переживает и reload, и `RestartNow`), pid/port не меняются, поля
`reloadCount`/`listenerUptime` нет. Headline-риск M1 остаётся непокрытым регрессом — этот таск
его закрывает.

## Цель

Автоматический EditMode-тест, доказывающий, что MCP-листенер **переживает domain reload** и
**оживает после смерти листенера**, проверяя это **реальным JSON-RPC `status` round-trip** (а не
приватным состоянием). RED-gate: падает, если убрать соответствующую ветку re-spawn.

## Scope / не в scope

- **В scope:** (1) domain-reload survival; (2) watchdog re-bind.
- **Не в scope:** play→edit (показан вручную; в этом проекте `m_EnterPlayModeOptionsEnabled: 0`,
  т.е. вход в Play = полный domain reload → подмножество теста 1); job-survival (M2, в M1 джобов нет);
  out-of-process batch-harness (fallback, только если внутритестовый reload окажется хрупким).

## Решения дизайна (зафиксированы)

- **UTF reload-survival механизм** (исходники UTF 1.1.33): EditMode `[UnityTest] IEnumerator` +
  `yield return new WaitForDomainReload()` после `EditorUtility.RequestScriptReload()`. Раннер —
  `ScriptableObject`, сериализует program counter итератора → тест **возобновляется на той же строке**
  после reload. Восстанавливается только позиция; **локалы обнуляются**, **поля fixture выживают лишь
  если `JsonUtility`-сериализуемы** (`public`/`[SerializeField]`, не static/не property).
- **Триггер reload:** `RequestScriptReload()` + `WaitForDomainReload()` (чистый reload без правки
  файлов; детерминированно). НЕ `RecompileScripts` (тот требует реального изменения .cs, иначе бросает
  «Editor does not need to recompile»).
- **Проба «жив»:** полный JSON-RPC `tools/call status` round-trip; проверка `projectName` в ответе.
- **⚠️ Анти-deadlock (критично):** `StatusTool.NeedsMainThread == true`. Синхронный HTTP-запрос из
  главного потока теста повесит редактор (listener-поток → `RunOnMain` ждёт дрейн диспетчера на
  `EditorApplication.update`, а главный поток заблокирован на сокете). Поэтому HTTP-проба идёт **на
  фоновом потоке (Task)**, а `[UnityTest]` `yield return null` крутит цикл, пока Task не завершится —
  тогда `EditorApplication.update` тикает и дренит диспетчер.
- **Локация:** файлы в существующий `Tests/Editor/` (asmdef `Shtl.Mcp.Editor.Tests` уже EditMode +
  TestRunner-рефы; новый asmdef не нужен). **Аддитивный тест → raw-diff не требуется.**

## Контракты, которые дёргает тест

- Листенер: `http://127.0.0.1:<port>/`, `POST`, тело — JSON-RPC, 200+тело / 202 пусто (`HttpServer`).
- Проба: `{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"status","arguments":{}}}`
  → `result.content[0].text` = JSON статуса, в нём `projectName` (`McpRouter` + `StatusTool`).
- Публичная поверхность: `Shtl.Mcp.Lifecycle.ShtlMcpServer.Instance.{Port, IsListening,
  EnsureStarted(), StopListenerForReload(), WatchdogTick()}`.

## Acceptance

- AC-1: тест `Listener_Respawns_AfterDomainReload` зелёный на текущем коде; форсит реальный domain
  reload и подтверждает `status` round-trip на **том же** порту с тем же `projectName` после reload.
- AC-2: тест `Watchdog_Rebinds_AfterListenerDeath` зелёный; после `StopListenerForReload()` +
  `WatchdogTick()` листенер снова отвечает на том же порту.
- AC-3 (RED-gate подтверждён): временный слом `afterAssemblyReload += Init` валит тест 1; слом ветки
  `if (!IsListening) EnsureStarted()` в `WatchdogTick` валит тест 2. Оба отката восстановлены.
- AC-4: оба теста — EditMode, проходят в Unity Test Runner; тест 1 помечен `[Category("DomainReload")]`
  (исключаем из быстрого inner-loop, в CI гоняем полностью).

## План шагов

1. **HTTP-хелпер** `Tests/Editor/ReloadSurvival/McpProbe.cs` — статический `Task<string>
   CallStatusAsync(int port)` (фоновый HttpWebRequest/HttpClient, POST JSON-RPC, вернуть тело);
   хелпер `bool TryGetProjectName(string respJson, out string name)` (распарсить `result.content[0].text`).
2. **Общая база/утиль** в тестовом файле: `IEnumerator PollStatusUntilProjectName(int port, string
   expected, double timeoutSec)` — yield-цикл: запустить `CallStatusAsync` на фоне, `yield return null`
   пока не завершится; повторять до успеха/таймаута. Использует `EditorApplication.timeSinceStartup`
   для дедлайна (не `DateTime` блокирующе).
3. **Тест 2 (watchdog, проще, без reload)** `Watchdog_Rebinds_AfterListenerDeath`:
   arrange (EnsureStarted, захватить порт, проба ок) → `StopListenerForReload()` → `WatchdogTick()` →
   poll до ответа на том же порту. Реализовать и прогнать первым (быстрый цикл).
4. **Тест 1 (domain reload)** `Listener_Respawns_AfterDomainReload`, `[Category("DomainReload")]`:
   `[SerializeField] int _port; [SerializeField] string _project;` → arrange (захватить порт+project,
   проба ок) → `RequestScriptReload()` + `yield return new WaitForDomainReload()` → после reload
   `PollStatusUntilProjectName(_port, _project, ~10с)`.
5. **Прогон** в Unity Test Runner (EditMode). Зафиксировать результат в journal.
6. **RED-gate верификация** (AC-3): по очереди сломать каждую ветку, прогнать, увидеть красное,
   откатить. Зафиксировать в journal.
7. **Batch-mode проверка** (отметка исследователя): убедиться, что `[InitializeOnLoad]`-бутстрап
   оживает после reload в `-runTests -testPlatform EditMode`. Если в batch не оживает — тест 1
   помечается интерактив-онли (`[Explicit]`/категория), batch покрывает тест 2; зафиксировать решение.
8. **Завершение:** `wiki/index.md` (если появилась code-страница), строка в `wiki/log.md`
   (`forward | reload-survival test | m1-reload-survival-test`), при необходимости — pin code-ref.

## Риски / открытые пункты

- **Batch-mode respawn** (шаг 7) — единственное непроверенное допущение; митигировано fallback'ом.
- **TIME_WAIT порта** после reload: `HttpServer.Start` ловит занятый порт и оставляет listener
  незапущенным, watchdog поднимает на следующем тике → poll-таймаут теста ≥10с это поглощает.
- **Прогон теста блипает живую MCP-сессию** (reload рвёт соединение) — ожидаемо, не блокер.
