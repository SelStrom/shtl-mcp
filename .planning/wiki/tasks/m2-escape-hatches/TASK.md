# TASK: T8 — escape hatches (execute_menu_item, run_csharp)

**Status:** done (79/79; run_csharp gate + реальная компиляция; execute_menu_item)
**Привязка:** F3/AC3.1 (escape hatches), F2/AC2.1 (footgun-флаг из T11).

## Тулы

- **`execute_menu_item`**: `EditorApplication.ExecuteMenuItem(menuItem)` → `{executed:bool}`.
- **`run_csharp`** (FOOTGUN, gated `AllowRunCsharp`, default off): компиляция+исполнение Editor-C# в память
  через CodeDom (`CSharpCodeProvider`, Mono — доступен в этом Unity). `code` = тело `static object Run()`,
  значение через `return`. Ссылается на все загруженные сборки. Возвращает `{ok,result}` или `{error}`
  (compile-ошибки/исключение). Флаг off → `{error: disabled}`.

## Верификация

- Unit (`EscapeHatchToolsTests`, 3): gate off → «disabled»; execute_menu_item неизвестного → executed=false
  (+ LogAssert.Expect на Unity-error); **run_csharp enabled `return 40+2;` → ok, result=="42"** (CodeDom
  работает). ✅
- 79/79; e2e: run_csharp с флагом off → «disabled».

## Долги
- Длинная компиляция run_csharp синхронна (не job) — для коротких сниппетов ок; долгие → опц. job.
- INV-3 identity-инъекция — общий долг M2.
