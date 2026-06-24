# Journal — m2-asmdef-split (T2, append-only)

## [2026-06-24] анализ зависимостей + split

**Карта зависимостей (сабагент).** Для каждого .cs — namespace + внешние типы проекта + целевая
сборка. Найден РОВНО ОДИН цикл сборок: **Lifecycle ↔ Tools**, два ребра TL→LC:
- `TestRunCallbacks.cs:59` — `ShtlMcpServer.Instance.Jobs.Complete(...)` (Tools→Lifecycle).
- `GetLogsTool` → `LogBuffer` (если Logging кладётся в Lifecycle).
Обратное ребро LC→TL: `ShtlMcpServer` регистрирует все тулы, `EditorContext : IEditorContext`.

**Шаг 1 — разрыв цикла кодом (в единой сборке, верифицировано до реструктуризации).**
- `TestRunCallbacks` получил `JobStore` через конструктор (DI) вместо `ShtlMcpServer.Instance`.
  `RunTestsTool.Invoke` → `new TestRunCallbacks(_jobs)`; `ReattachIfPending(JobStore)` ← `EnsureStarted(_jobs)`.
  Удалён мёртвый `ShtlMcpServer.Jobs`. → 52/52 зелёные, reload-spanning цел (force-recompile, reload 20).

**Шаг 2 — реорганизация в 6 папок + 6 asmdef.**
- Перемещены .cs+.meta (сохранён GUID): Server/HttpServer→Transport; Server/DispatchingToolInvoker→Lifecycle;
  Dispatch+Jobs+Logging→Dispatcher; Common→Registry; AssemblyInfo→Tools. Опустевшие папки + .meta удалены.
- Создано 6 asmdef (references по имени, все Editor-only). Старый `Shtl.Mcp.Editor.asmdef` удалён.
- Тестовая asmdef: ссылки `Shtl.Mcp.Editor` → 5 сборок (Transport/Dispatcher/Registry/Tools/Lifecycle;
  UI не нужна — `DashboardWindow` тестами не используется).
- `[InternalsVisibleTo]` (AssemblyInfo) переехал в Tools — там internal-члены `TestRunnerNoThrottle`.

**Ловушка верификации.** После Write .asmdef мимо AssetDatabase Unity их сразу НЕ импортировала: первый
прогон шёл против СТАРОЙ `Shtl.Mcp.Editor.dll` (в `ScriptAssemblies` 6 split-DLL ещё не было). Форс
`recompile` (Refresh+RequestScriptCompilation) → импорт asmdef → реальная сборка split.

**Верификация (после реального split, reload 21→23).**
- `ScriptAssemblies/`: `Shtl.Mcp.{Transport,Dispatcher,Registry,Tools,Lifecycle,UI}.dll` есть, старая
  `Shtl.Mcp.Editor.dll` исчезла, 0 ошибок компиляции.
- Полный сьют против split: **52/52 passed**, reloadCount 23→24 (ReloadSurvivalTests форсит reload —
  reload-spanning жив через границы сборок), status отзывчив (max-lat 0.10с, 3 DOWN = окно reload).

Поведение не изменилось (характеризация зелёная). Реализация architecture.md §Модульность; отклонения
(имена `Shtl.Mcp.*`, Logging→Dispatcher, Common→Registry, DispatchingToolInvoker→Lifecycle, DI-разрыв
цикла) зафиксированы в architecture.md и TASK.md.
