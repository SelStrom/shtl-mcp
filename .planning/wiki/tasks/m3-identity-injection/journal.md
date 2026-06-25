# Journal — m3-identity-injection (T2, append-only)

## [2026-06-25] реализация
INV-3: `projectName` во все ответы тулов. Кросс-каттинг в `McpRouter.tools/call`: text → поле в JSON,
image/`_content` → отдельный text-элемент, ошибка → префикс `[name] Error`. `projectName` =
`Application.productName`, передан из ShtlMcpServer (главный поток) в конструктор роутера. Тесты:
McpRouterTests +2 (инъекция в text + в ошибку; image-passthrough усилен на projectName-элемент);
FakeInvoker.status → `{foo}` (projectName инжектит роутер). 87/87. E2e: get_config/ping/get_logs/screenshot
несут projectName. Реализация INV-3 (raw есть) — raw не менялся.
