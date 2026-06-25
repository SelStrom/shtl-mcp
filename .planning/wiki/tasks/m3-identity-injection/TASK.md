# TASK: T2 — identity-инъекция (INV-3)

**Status:** done (projectName во всех ответах; 87/87)
**Привязка:** INV-3 (`raw/domain/overview.md` — ответ несёт идентичность инстанса). Реализация существующего
интента — raw не менялся.

## Реализация

`McpRouter` инжектит `projectName` (= `Application.productName`, передан из `ShtlMcpServer` при старте на
главном потоке) в КАЖДЫЙ ответ `tools/call` — кросс-каттинг в транспорте, по одному месту:
- **text-результат** → поле `projectName` в самом JSON-результате (модель видит в тексте).
- **image/`_content`-результат** → отдельный text-элемент `projectName: <name>` (в _content поле не вставить).
- **ошибка тула** → префикс `[<projectName>] Error: ...`.

`status` и так возвращал `projectName` — инъекция перезаписывает тем же значением (в проде идентичны).

## Верификация

- Unit (`McpRouterTests`, +2): `ToolsCall_InjectsProjectName_INV3` (text-результат несёт projectName);
  `ToolError_CarriesProjectName_INV3` (ошибка несёт); `PassesThrough_ContentConvention` усилен (image →
  text-элемент с projectName). 87/87 (85 + 2).
- E2e: `get_config`/`ping`/`get_logs` → `projectName:"shtl-mcp"`; `screenshot` → text-элемент
  `projectName: shtl-mcp`.

## Заметки
- Идентичность по `projectName`; `serverName` (registry) — отдельно (в `status`). При мульти-инстансе
  модель по `projectName` понимает, какой Unity ответил.
