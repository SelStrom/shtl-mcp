using System;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Shtl.Mcp.Tools
{
    /// Кадр Game- или Scene-view как MCP image-content (PNG, base64). По умолчанию рендерит камеру в
    /// RenderTexture (работает и в edit-режиме). С `overlay:true` (только Play mode) отдаёт composited-кадр
    /// Game View — со Screen-Space-Overlay uGUI и UI Toolkit runtime-панелями, которые camera-рендер не
    /// захватывает. Возвращает `_content` с image-элементом (см. McpRouter-конвенцию).
    public sealed class ScreenshotTool : ITool
    {
        const int MaxDim = 2048;

        public string Name => "screenshot";
        public string Description =>
            "Capture the Game or Scene view as a PNG image (view: 'game' | 'scene', default 'game'). " +
            "Optional 'camera' (GameObject path or name with a Camera) captures that specific camera instead. " +
            "Optional 'overlay' (bool, Game view + Play mode only): capture the composited frame WITH " +
            "Screen-Space-Overlay uGUI and UI Toolkit runtime panels (a plain camera render omits them). " +
            "width/height default to the current Game View resolution & aspect (no distortion); pass one to " +
            "derive the other from that aspect, or both to force an exact size for a specific case.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["view"] = new JObject { ["type"] = "string", ["description"] = "'game' (Camera.main) or 'scene' (Scene view)." },
                ["camera"] = new JObject { ["type"] = "string", ["description"] = "GameObject path or name with a Camera component; takes priority over 'view' (AC3.14)." },
                ["overlay"] = new JObject { ["type"] = "boolean", ["description"] = "Game view + Play mode only: composited frame WITH Screen-Space-Overlay uGUI and UI Toolkit panels." },
                ["width"] = new JObject { ["type"] = "integer", ["description"] = "Image width. Default: current Game View width; if only height is given, derived from the current aspect." },
                ["height"] = new JObject { ["type"] = "integer", ["description"] = "Image height. Default: current Game View height; if only width is given, derived from the current aspect." }
            }
        };

        public JObject Invoke(JObject args)
        {
            var view = (string)args["view"] ?? "game";
            ResolveTargetSize(args, out int w, out int h);

            var camName = (string)args["camera"];
            bool overlay = args["overlay"] != null && (bool)args["overlay"];
            if (overlay)
            {
                // overlay = экранный composited Game View; несовместим с конкретной камерой / scene-view
                if (!string.IsNullOrEmpty(camName) || view == "scene")
                {
                    return new JObject { ["error"] = "overlay capture is only valid for the Game view (drop 'camera'/'scene')" };
                }
                if (!EditorApplication.isPlaying)
                {
                    return new JObject { ["error"] = "overlay capture requires Play mode (Screen-Space-Overlay UI exists only at runtime)" };
                }
                var composited = CaptureComposited(w, h);
                if (composited == null)
                {
                    return new JObject { ["error"] = "no Game View to composite (open a Game view window)" };
                }
                return ImageContent(composited, $"game (composited) view {w}x{h} ({composited.Length} bytes PNG)");
            }

            Camera cam;
            if (!string.IsNullOrEmpty(camName))
            {
                var go = SceneObjects.Resolve(camName);
                if (go == null)
                {
                    return new JObject { ["error"] = "camera GameObject not found: " + camName };
                }
                cam = go.GetComponent<Camera>();
                if (cam == null)
                {
                    return new JObject { ["error"] = "no Camera component on: " + SceneObjects.PathOf(go) };
                }
                view = "camera '" + go.name + "'";
            }
            else if (view == "scene")
            {
                var sv = SceneView.lastActiveSceneView;
                cam = sv != null ? sv.camera : null;
                if (cam == null)
                {
                    return new JObject { ["error"] = "no active Scene view to capture" };
                }
            }
            else
            {
                cam = Camera.main;
                if (cam == null)
                {
                    foreach (var c in Camera.allCameras)
                    {
                        if (c.enabled)
                        {
                            cam = c;
                            break;
                        }
                    }
                }
                if (cam == null)
                {
                    return new JObject { ["error"] = "no camera to capture for the Game view" };
                }
            }

            var png = Capture(cam, w, h);
            return ImageContent(png, $"{view} view {w}x{h} ({png.Length} bytes PNG)");
        }

        static MethodInfo _getGameViewSize;
        static bool _gameViewSizeResolved;

        /// Текущий target-размер главного Game View через internal API (версионно-зависимый, reflection).
        /// false, если API/окна нет (edit без Game View) — тогда вызывающий берёт фолбэк 1024x576.
        static bool TryGetGameViewSize(out int w, out int h)
        {
            w = 0;
            h = 0;
            if (!_gameViewSizeResolved)
            {
                _gameViewSizeResolved = true;
                var pmvType = typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
                _getGameViewSize = pmvType?.GetMethod("GetMainPlayModeViewTargetSize",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (_getGameViewSize == null)
                {
                    _getGameViewSize = typeof(Handles).GetMethod("GetMainGameViewSize",
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                }
            }
            if (_getGameViewSize == null)
            {
                return false;
            }
            try
            {
                if (_getGameViewSize.Invoke(null, null) is Vector2 size)
                {
                    w = (int)size.x;
                    h = (int)size.y;
                    return w > 0 && h > 0;
                }
            }
            catch
            {
                // internal API дрогнул на версии — молча в фолбэк
            }
            return false;
        }

        /// Дефолт — текущее разрешение/aspect Game View (без искажения кадра). Явные width/height задают
        /// целевой кейс; одна сторона доводит другую по текущему aspect. Фолбэк 1024x576, если Game View нет.
        static void ResolveTargetSize(JObject args, out int w, out int h)
        {
            bool haveNative = TryGetGameViewSize(out int nativeW, out int nativeH);
            int? argW = args["width"] != null ? (int?)(int)args["width"] : null;
            int? argH = args["height"] != null ? (int?)(int)args["height"] : null;
            ComputeTargetSize(argW, argH, haveNative, nativeW, nativeH, out w, out h);
        }

        /// Чистая политика размера (выделена для тестов). Приоритет: оба заданы → как есть; одна сторона →
        /// вторая по текущему aspect; ничего → native Game View; без native → 1024x576. Клампы [16, MaxDim].
        internal static void ComputeTargetSize(int? argW, int? argH, bool haveNative, int nativeW, int nativeH, out int w, out int h)
        {
            if (argW.HasValue && argH.HasValue)
            {
                w = argW.Value;
                h = argH.Value;
            }
            else if (argW.HasValue)
            {
                w = argW.Value;
                double aspect = haveNative ? (double)nativeH / nativeW : 576.0 / 1024.0;
                h = (int)System.Math.Round(w * aspect);
            }
            else if (argH.HasValue)
            {
                h = argH.Value;
                double aspect = haveNative ? (double)nativeW / nativeH : 1024.0 / 576.0;
                w = (int)System.Math.Round(h * aspect);
            }
            else if (haveNative)
            {
                w = nativeW;
                h = nativeH;
            }
            else
            {
                w = 1024;
                h = 576;
            }
            w = Mathf.Clamp(w, 16, MaxDim);
            h = Mathf.Clamp(h, 16, MaxDim);
        }

        static JObject ImageContent(byte[] png, string caption) => new JObject
        {
            ["_content"] = new JArray
            {
                new JObject { ["type"] = "image", ["data"] = Convert.ToBase64String(png), ["mimeType"] = "image/png" },
                new JObject { ["type"] = "text", ["text"] = caption }
            }
        };

        static byte[] Capture(Camera cam, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            Texture2D tex = null;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                return tex.EncodeToPNG();
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                if (tex != null)
                {
                    UnityEngine.Object.DestroyImmediate(tex); // освободить и на exception-пути
                }
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        /// Composited-кадр Game View (камеры + Screen-Space-Overlay uGUI + UI Toolkit) через
        /// PlayModeView.RenderView. Возвращает null, если Game View окна нет. Play mode only.
        static byte[] CaptureComposited(int w, int h)
        {
            var src = RenderGameView();
            if (src == null)
            {
                return null;
            }

            // src принадлежит PlayModeView — только читаем, НЕ Release/Destroy. Blit в свою temp для ресайза.
            var prevActive = RenderTexture.active;
            RenderTexture dst = null;
            Texture2D tex = null;
            try
            {
                dst = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                // GameView RT ориентирована под окно (origin сверху); ReadPixels читает снизу-вверх →
                // без вертикального флипа кадр перевёрнут. scale.y=-1, offset.y=1 отражают по вертикали.
                Graphics.Blit(src, dst, new Vector2(1f, -1f), new Vector2(0f, 1f));
                RenderTexture.active = dst;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                return tex.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (tex != null)
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
                if (dst != null)
                {
                    RenderTexture.ReleaseTemporary(dst);
                }
            }
        }

        static MethodInfo _getMainView;
        static MethodInfo _renderView;

        /// Форсит синхронный рендер главного Game View в его target texture и возвращает её.
        /// Internal UnityEditor API (PlayModeView) — версионно-зависимый, поэтому reflection с null-guard.
        static RenderTexture RenderGameView()
        {
            var pmvType = typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
            if (pmvType == null)
            {
                return null;
            }
            if (_getMainView == null)
            {
                _getMainView = pmvType.GetMethod("GetMainPlayModeView", BindingFlags.Static | BindingFlags.NonPublic);
                _renderView = pmvType.GetMethod("RenderView", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            if (_getMainView == null || _renderView == null)
            {
                return null;
            }
            var view = _getMainView.Invoke(null, null);
            if (view == null)
            {
                return null;
            }
            return _renderView.Invoke(view, new object[] { Vector2.zero, false }) as RenderTexture;
        }
    }
}
