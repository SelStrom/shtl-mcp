# TASK: Focus-independent re-spawn после domain reload (bugfix INV-5)

**Status:** done (AC-1 ✅: 3 unfocused MCP-recompile подряд, reloadCount 5→6→7, self-recovery ~10-14с; AC-2 — регрессию reload-survival прогнать в Test Runner)
**Тип:** bugfix кода под существующее намерение (INV-5, wiki/lifecycle-and-reload.md §1) — **без raw/wiki-изменений**.
**Привязка:** INV-5 (самовосстановление без человека), F4 (reload), F2/AC2.2 (watchdog-выживание).
Разблокирует self-service дев-петлю (MCP-`recompile`/`run_tests` без участия человека).

## Находка (как всплыло)

При попытке self-service `recompile` через MCP (пока Unity НЕ в фокусе) сервер завис в DOWN на >120с.
Диагностика: Unity жив (CPU ~0.9%, простаивает), листенер не слушает, Editor.log показал завершённый
domain reload (546мс), registry heartbeat замер на моменте reload. Сервер вернулся только когда
пользователь вернул фокус в Unity.

**Корень:** Unity троттлит/останавливает `EditorApplication.update`, когда окно не в фокусе. Re-spawn
после reload был завязан на `delayCall`→`Init` (тик `update`). В фоне `update` не тикает → re-spawn не
происходит. Chicken-egg: нет листенера → нет входящих запросов → нечем разбудить `update` → вечный сон
до фокуса. Это бьёт по INV-5 («самовосстановление без человека») и ломает любую MCP-инициированную
автономию (control-flag/T10 имел бы тот же дефект — он тоже на watchdog/`update`).

**Код-vs-intent дрейф:** wiki §1 уже декларирует «`afterAssemblyReload` → переподнятие», но код
подписывал `afterAssemblyReload += Init` *внутри* `Init` (запускаемого через `delayCall`). После reload
в новом домене подписка ещё не вооружена → событие срабатывает вхолостую. Это баг реализации, не
намерения → правим код, raw/wiki не трогаем (wiki уже корректен).

## Фикс

- **`ShtlMcpBootstrap`**: подписки `beforeAssemblyReload`/`afterAssemblyReload` перенесены в
  `[InitializeOnLoad]`-ctor. Static ctor исполняется синхронно на КАЖДОЙ загрузке домена (часть
  reload-последовательности, до `afterAssemblyReload`), независимо от фокуса. Канонический паттерн Unity.
  `delayCall` оставлен для первичного старта/подстраховки. Init идемпотентен (EnsureStarted с guard).
- **`RecompileTool`**: инкрементальный по умолчанию (`AssetDatabase.Refresh()` — импорт правок +
  компиляция затронутого); `force:true` → доп. `CompilationPipeline.RequestScriptCompilation()` (полный
  reload даже без изменений). Прошлая версия всегда форсила полную пересборку (медленно, двойной reload).

## Acceptance

- AC-1: после фокус-компиляции фикса MCP-`recompile`, вызванный пока Unity **НЕ в фокусе**, приводит к
  тому, что сервер **сам возвращается** (re-spawn через `afterAssemblyReload`), без участия человека.
  Verify: правлю комментарий в .cs → `recompile` → polling `status` (Bash curl) поднимается без фокуса.
- AC-2: фокус-кейс и `m1-reload-survival-test` по-прежнему зелёные (фикс не сломал focused-путь).
- AC-3: дев-петля: после AC-1 я пересобираю и (после T7) гоняю тесты без участия человека.

## Тест-гэп (важно)

`m1-reload-survival-test` НЕ ловит этот баг: тесты в Test Runner идут при сфокусированном редакторе →
`delayCall`/`update` тикают → re-spawn работает даже с багом. Unfocused-кейс автоматизировать тяжело
(тестраннер требует активного редактора). Верификация AC-1 — ручной эксперимент (фокус-компиляция фикса,
затем MCP-recompile в фоне). Зафиксировать результат в journal.

## Шаги

1. Код-фикс (сделано: ShtlMcpBootstrap + RecompileTool).
2. Фокус-компиляция фикса (пользователь, один раз) + `get_logs(error)` чисто.
3. Эксперимент self-recovery в фоне (AC-1): Unity не в фокусе → правлю комментарий → `recompile` → Bash-polling status → сервер встаёт сам.
4. Регрессия: `m1-reload-survival-test` зелёный (фокус-кейс).
5. Финал: journal + log.md (bugfix), если зелено.
