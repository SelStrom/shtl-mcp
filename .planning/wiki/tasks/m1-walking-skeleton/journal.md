# Journal: M1 — Walking Skeleton

Append-only журнал исполнения. Режим: subagent-driven (свежий субагент на задачу).
Среда: Unity 2022.3.62f3 (batchmode+лицензия проверены headless), dotnet 10, mono.

## [2026-06-13] Исполнение Tasks 1–13

**Подход.** 13 задач плана. Чистая логика (2–9) — TDD с реальными EditMode-тестами
(RED→GREEN на каждой). Интеграция (10–12) — компиляция + headless-смоук от
оркестратора. Тривиальные verbatim-задачи верифицировались напрямую; регресс-гейты
после shared-изменений; финальный полный прогон.

**Результаты.**
- Все 12 коммитов реализации: `a7df6dd` (skeleton) → `799bf46` (dashboard).
- **34/34 EditMode-теста зелёные** (Fnv 3, Port 3, ServerName 4, Registry 4,
  Dispatcher 4, LogBuffer 3, JsonRpc 2, McpRouter 7, Status 2, GetLogs 2).
- **Headless HTTP smoke-тест (оркестратор):** Unity в фоне → авто-старт через
  `[InitializeOnLoad]` → по HTTP проверены `initialize` (serverInfo + instructions),
  `tools/list` (2 инструмента со схемами), `tools/call status` (полная идентичность,
  port=9756=детерминированный preferred, mode=edit, health=ok), `get_logs` (реально
  перехватил Unity-warning → подписка на логи работает), `registry.json` записан с
  heartbeat. Конвейер HttpListener→router→invoker→dispatcher→tools→registry→lifecycle
  подтверждён end-to-end.

**Отклонения от плана (осознанные).**
1. **Один Editor-asmdef** с папками-модулями вместо 6 раздельных (как и помечено в
   PLAN.md). Физический split — кандидат на M2.
2. **Пробел Task 1:** `com.unity.test-framework` не был в манифесте; добавлен на
   Task 2 (1.1.33). Test-asmdef поправлен: `overrideReferences: true`, Newtonsoft в
   `precompiledReferences` как `Newtonsoft.Json.dll` (не в `references`). После
   изменения — регресс-гейт 30/30, затем 34/34. Канонический asmdef зафиксирован.
3. Локальный `brace-style` хук переформатировал однострочные `if/return` в
   Lifecycle/Dashboard на скобки — косметика, поведение не изменилось.

**Остаётся на интерактивную приёмку пользователем (headless невозможно):**
- Выживание сервера при **domain reload** в живом редакторе (механизм заложен:
  `AssemblyReloadEvents` + `[InitializeOnLoad]` + `SessionState` порт + watchdog —
  но прогон reload требует интерактивной сессии).
- `claude mcp add` в Claude Code пользователя и проверка префикса инструментов
  (HTTP-контракт уже доказан эквивалентным headless-curl'ом).

**Статус:** M1 реализован и верифицирован в headless-объёме; ветка `m1-walking-skeleton`
готова к финальному ревью и приёмке.

## [2026-06-13] Финальное ревью (opus) + правки

**Вердикт:** READY TO MERGE (M1 scope). Critical — нет. Подтверждены: отсутствие
self-deadlock (`RunOnMain` только с фонового HTTP-потока), идемпотентные подписки,
guard двойного bind, atomic-запись реестра, чистота слоёв (Unity API только в
Lifecycle/UI/log-hook).

**Исправлено сразу (коммит `3f18ea5`):**
- *Important #1* — `HttpServer.Start()` обёрнут в try/catch: если порт после reload
  ещё в TIME_WAIT, сервер остаётся «не слушающим» (без исключения наружу watchdog'а),
  порт не меняется, следующий тик повторит bind. В `ShtlMcpServer.EnsureStarted` —
  guard `if (!_http.IsListening) { _http = null; return; }`, heartbeat только при успехе.
- *Important #2* — добавлена подписка `AssemblyReloadEvents.afterAssemblyReload += Init`
  (рядом с `delayCall`), сужает окно недоступности после reload; соответствует
  контракту `lifecycle-and-reload.md` §1.
- Регресс после правок: 34/34; повторный headless-смоук — happy-path цел.

**M2-бэклог (приемлемо для M1, не блокеры):**
- `ClientCount` — эвристика 0/1 (не различает ≥2 клиентов; curl-проба считается за
  клиента). Сделать честный подсчёт активных сессий.
- `Heartbeat()` глушит исключения молча → сбой записи реестра невидим при `health:ok`.
  Логировать первый сбой в LogBuffer.
- `RegistryStore.WriteAtomic` — гонка первого писателя между двумя инстансами
  (низковероятна, самовосстанавливается).
- DRY: `ProjectPath` дублируется в `EditorContext` и `ShtlMcpServer`.
- Дашборд: глифы `●/○` могут не рендериться в части шрифтов редактора.
- `JsonRpc.Result` не null-guard'ит `id` (безвредно — нотификации не доходят).

## [2026-06-14] приёмка reload | автоматизирована
Оставшийся пункт «интерактивная приёмка reload — за пользователем» закрыт автоматически в таске
`m1-reload-survival-test`: EditMode `[UnityTest]` RED-gate на domain-reload + watchdog re-bind.
Остаётся только `claude mcp add` в живом Claude Code (подтверждено по факту использования).
