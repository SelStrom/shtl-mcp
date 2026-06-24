# Journal — m2-screenshot (T9, append-only)

## [2026-06-24] реализация + e2e

ScreenshotTool: рендер камеры (game: Camera.main/first enabled; scene: SceneView.lastActiveSceneView.
camera) в RenderTexture → ReadPixels → EncodeToPNG → base64. Для image-content расширил McpRouter
конвенцией `_content` (тул отдаёт готовые MCP content-элементы; иначе text-обёртка). Верификация: router
unit-тест на passthrough; 73/73; e2e — screenshot game/scene вернули валидный PNG image-content (base64
PNG-заголовок, isError=false). Реализация F3/AC3.1, AC4.5 — raw не менялся.
