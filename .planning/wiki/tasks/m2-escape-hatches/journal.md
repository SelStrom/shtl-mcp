# Journal — m2-escape-hatches (T8, append-only)

## [2026-06-24] реализация
execute_menu_item (ExecuteMenuItem) + run_csharp (footgun, gated AllowRunCsharp из T11). run_csharp:
CodeDom CSharpCodeProvider, in-memory, ссылки на все загруженные сборки, обёртка `static object Run(){…}`.
Эмпирически CodeDom В ЭТОМ Unity доступен — тест `return 40+2;` → «42» (компиляция+исполнение работают).
Фейл-итерация: execute_menu_item на неизвестном меню логирует Unity-Error → тест-фреймворк считал фейлом;
добавил LogAssert.Expect. Верификация: 79/79, e2e gate (off → disabled). raw не менялся (F3/AC3.1, F2/AC2.1).
