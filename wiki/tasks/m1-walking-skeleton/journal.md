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
