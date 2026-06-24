# Journal — reload-respawn-focus-independent (append-only)

## [2026-06-14] находка + фикс

Контекст: пользователь предложил добавить MCP-команды для self-service recompile/test. Добавил
`RecompileTool`; при первом же self-вызове `recompile` (Unity НЕ в фокусе) сервер завис в DOWN >120с.

Диагностика (Bash): pid 64351 жив, CPU 0.9% (простой), ничего не слушает на 9730, Editor.log —
domain reload завершён (546мс), registry heartbeat замер на моменте reload. Сервер вернулся ТОЛЬКО
после возврата фокуса в Unity (reloadCount 2→3, listenerUptime сброс). → корень: Unity троттлит
EditorApplication.update в фоне; re-spawn был на delayCall→update → в фоне не происходит.

Код-vs-intent: wiki §1 декларирует afterAssemblyReload→re-spawn, но код подписывал afterAssemblyReload
внутри Init (delayCall-deferred) → после reload подписка не вооружена. Баг реализации → фикс кода,
raw/wiki не трогаем.

Фикс: ShtlMcpBootstrap — подписки before/afterAssemblyReload перенесены в [InitializeOnLoad]-ctor
(исполняется синхронно на каждой загрузке домена, независимо от фокуса; afterAssemblyReload фиксит
re-spawn в reload-последовательности). RecompileTool — инкрементальный по умолчанию (Refresh),
force:true → полный RequestScriptCompilation.

Тест-гэп: m1-reload-survival-test не ловит баг (тесты идут focused). AC-1 — ручной unfocused-эксперимент.
Дальше: фокус-компиляция фикса (пользователь) → MCP-recompile в фоне → проверка self-recovery.

## [2026-06-23] верификация | AC-1 ✅ автономный цикл

Фокус-компиляция фикса (пользователь, Ctrl+R; первый клик не сработал — Auto Refresh off): reloadCount
3→4, get_logs чисто → новый бутстрап активен.

Эксперимент self-recovery (Unity НЕ в фокусе, пользователь в терминале):
- правка комментария в .cs → MCP `recompile` → reloadCount 4→5, сервер сам поднялся (listenerUptime 61) — без фокуса.
- ещё правка → MCP `recompile` → reloadCount 5→6, listenerUptime 11 — без фокуса.
- удаление маркера → MCP `recompile` → reloadCount 6→7, listenerUptime 14, get_logs(error) пуст — без фокуса.

Контраст: до фикса unfocused-recompile вешал сервер на 120с+ (до фокуса); после — self-recovery ~10-14с.
Down-окно не ловилось (recovery быстрее интервала поллинга ~1с). Поллинг — Bash curl на 9730 (MCP-канал
мигает в reload). RecompileTool заодно сделан инкрементальным (Refresh) + force:true; маркер убран.

Вывод: автономная дев-петля (edit → MCP recompile → self-recovery → verify) работает. Self-test пока нет
(нужен run_tests=T7, зависит от T1 async-job). AC-2 (focused-регрессия m1-reload-survival-test) — за
пользователем в Test Runner (риск низкий: фикс аддитивен, delayCall-путь сохранён).
