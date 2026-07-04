using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Shtl.Mcp.Tools
{
    /// Кадр Game- или Scene-view как MCP image-content (PNG, base64). Рендерит камеру в RenderTexture
    /// (работает и в edit-режиме). Возвращает `_content` с image-элементом (см. McpRouter-конвенцию).
    public sealed class ScreenshotTool : ITool
    {
        const int MaxDim = 2048;

        public string Name => "screenshot";
        public string Description =>
            "Capture the Game or Scene view as a PNG image (view: 'game' | 'scene', default 'game'). " +
            "Optional 'camera' (GameObject path or name with a Camera) captures that specific camera instead. " +
            "Optional width/height (default 1024x576).";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["view"] = new JObject { ["type"] = "string", ["description"] = "'game' (Camera.main) or 'scene' (Scene view)." },
                ["camera"] = new JObject { ["type"] = "string", ["description"] = "GameObject path or name with a Camera component; takes priority over 'view' (AC3.14)." },
                ["width"] = new JObject { ["type"] = "integer", ["description"] = "Image width (default 1024)." },
                ["height"] = new JObject { ["type"] = "integer", ["description"] = "Image height (default 576)." }
            }
        };

        public JObject Invoke(JObject args)
        {
            var view = (string)args["view"] ?? "game";
            int w = Mathf.Clamp(args["width"] != null ? (int)args["width"] : 1024, 16, MaxDim);
            int h = Mathf.Clamp(args["height"] != null ? (int)args["height"] : 576, 16, MaxDim);

            Camera cam;
            var camName = (string)args["camera"];
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
            return new JObject
            {
                ["_content"] = new JArray
                {
                    new JObject { ["type"] = "image", ["data"] = Convert.ToBase64String(png), ["mimeType"] = "image/png" },
                    new JObject { ["type"] = "text", ["text"] = $"{view} view {w}x{h} ({png.Length} bytes PNG)" }
                }
            };
        }

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
    }
}
