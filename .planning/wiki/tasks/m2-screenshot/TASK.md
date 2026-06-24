# TASK: T9 — screenshot

**Status:** done (e2e: валидный PNG image-content; 73/73)
**Привязка:** F3/AC3.1 (тулсет), AC4.5 (работает в edit и play).

## Реализация

- **`screenshot`** (`Editor/Tools/ScreenshotTool.cs`): `view: game|scene` (default game), опц. width/height
  (1024x576, clamp ≤2048). Рендерит камеру (`Camera.main`/first enabled для game; `SceneView.lastActive
  SceneView.camera` для scene) в RenderTexture → Texture2D.ReadPixels → EncodeToPNG → base64. Нет камеры/
  scene-view → понятная ошибка.
- **McpRouter-конвенция `_content`**: тул может вернуть `{_content:[...]}` — роутер отдаёт готовые MCP
  content-элементы как есть (image), иначе оборачивает JSON как text. screenshot возвращает image+text.

## Верификация

- Router unit (`McpRouterTests.ToolsCall_PassesThrough_ContentConvention`): `_content` с image →
  content[0].type=="image", mimeType сохранён. ✅
- Полный сьют: **73/73** (72 + 1).
- E2E (MCP): screenshot game → image/png base64 (PNG-заголовок `iVBORw0KGgo`, ~76KB base64, 640x360);
  scene → image (~30KB). isError=false. ✅

## Долги

- Game-view в edit без камеры → ошибка (ожидаемо). Скриншот именно GameView-композиции (не через
  Camera.Render) — опц. улучшение. INV-3 identity-инъекция — общий долг M2.
