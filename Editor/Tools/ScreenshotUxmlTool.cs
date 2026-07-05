using System;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shtl.Mcp.Tools
{
    /// Рендер одного UXML-ассета в PNG прямо в Edit Mode, без Play mode. Строит временную offscreen
    /// UI Toolkit-панель (PanelSettings с targetTexture), клонирует в неё UXML, форсит layout+repaint
    /// через internal panel-API и снимает RenderTexture. Дополняет ScreenshotTool: тот в overlay-режиме
    /// требует Play mode и рисует backbuffer со ВСЕМ экраном, а этот изолированно проверяет вёрстку
    /// одного UXML со стилями из его собственных <Style>-импортов. Возвращает _content с image-элементом
    /// (см. McpRouter-конвенцию).
    ///
    /// Почему offscreen работает в edit-режиме, а overlay — нет: overlay-панель компонуется в backbuffer
    /// player loop'ом, который вне Play mode не тикает; targetTexture-панель рендерится в свою RT прямым
    /// вызовом panel-API, минуя player loop.
    public sealed class ScreenshotUxmlTool : ITool
    {
        const int MaxDim = 4096;

        public string Name => "screenshot_uxml";
        public string Description =>
            "Render a UXML asset to a PNG in Edit Mode (no Play mode required): builds a temporary offscreen " +
            "UI Toolkit panel, forces layout + repaint, captures its RenderTexture. 'uxml' (required) is the " +
            "asset path. Optional 'uss' (array of stylesheet asset paths applied on top of the UXML's own " +
            "<Style> imports), 'theme' (ThemeStyleSheet .tss asset path; auto-picks the first project theme " +
            "if omitted), 'clearColor' (#RRGGBB background fill; transparent if omitted), width/height " +
            "(default 1024x768). Returns image-content.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["uxml"] = new JObject { ["type"] = "string", ["description"] = "Asset path to the .uxml (e.g. Assets/Content/UI/Foo.uxml)." },
                ["uss"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" },
                    ["description"] = "Extra USS asset paths applied on top of the UXML's own <Style> imports."
                },
                ["theme"] = new JObject { ["type"] = "string", ["description"] = "ThemeStyleSheet (.tss) asset path; auto-picks the first project theme if omitted." },
                ["clearColor"] = new JObject { ["type"] = "string", ["description"] = "Background fill as #RRGGBB (or #RRGGBBAA); transparent if omitted." },
                ["width"] = new JObject { ["type"] = "integer", ["description"] = "Image width (default 1024)." },
                ["height"] = new JObject { ["type"] = "integer", ["description"] = "Image height (default 768)." }
            },
            ["required"] = new JArray { "uxml" }
        };

        public JObject Invoke(JObject args)
        {
            var uxmlPath = (string)args["uxml"];
            if (string.IsNullOrEmpty(uxmlPath))
            {
                return new JObject { ["error"] = "'uxml' is required (asset path to a .uxml)" };
            }
            var vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (vta == null)
            {
                return new JObject { ["error"] = "UXML asset not found: " + uxmlPath };
            }

            var theme = ResolveTheme((string)args["theme"], out var themeError);
            if (theme == null)
            {
                return new JObject { ["error"] = themeError };
            }

            int w = Mathf.Clamp(args["width"] != null ? (int)args["width"] : 1024, 16, MaxDim);
            int h = Mathf.Clamp(args["height"] != null ? (int)args["height"] : 768, 16, MaxDim);

            RenderTexture rt = null;
            PanelSettings ps = null;
            GameObject go = null;
            Texture2D tex = null;
            var prevActive = RenderTexture.active;
            try
            {
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                rt.Create();

                ps = ScriptableObject.CreateInstance<PanelSettings>();
                ps.hideFlags = HideFlags.HideAndDontSave;
                ps.themeStyleSheet = theme;
                ps.scaleMode = PanelScaleMode.ConstantPixelSize; // 1:1 пиксели → вёрстка без масштабного искажения
                var clearArg = (string)args["clearColor"];
                if (!string.IsNullOrEmpty(clearArg))
                {
                    if (!ColorUtility.TryParseHtmlString(clearArg, out var clear))
                    {
                        return new JObject { ["error"] = "clearColor is not a valid #RRGGBB(AA) value: " + clearArg };
                    }
                    ps.clearColor = true;
                    ps.colorClearValue = clear;
                }
                ps.targetTexture = rt;

                go = new GameObject("__shtl_uxml_capture");
                go.hideFlags = HideFlags.HideAndDontSave;
                var doc = go.AddComponent<UIDocument>();
                doc.panelSettings = ps;

                var root = doc.rootVisualElement;
                if (root == null)
                {
                    return new JObject { ["error"] = "UIDocument produced no rootVisualElement" };
                }
                vta.CloneTree(root);

                if (args["uss"] is JArray ussPaths)
                {
                    foreach (var token in ussPaths)
                    {
                        var ussPath = (string)token;
                        var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
                        if (sheet == null)
                        {
                            return new JObject { ["error"] = "USS asset not found: " + ussPath };
                        }
                        root.styleSheets.Add(sheet);
                    }
                }

                if (!RenderPanel(root.panel))
                {
                    return new JObject { ["error"] = "UI Toolkit offscreen render API not found (Unity version mismatch)" };
                }

                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                var png = tex.EncodeToPNG();
                return ImageContent(png, $"uxml '{uxmlPath}' {w}x{h} ({png.Length} bytes PNG)");
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (tex != null)
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
                if (ps != null)
                {
                    UnityEngine.Object.DestroyImmediate(ps);
                }
                if (rt != null)
                {
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
            }
        }

        /// Тема обязательна: без ThemeStyleSheet панель не резолвит layout (root остаётся NaN) и шрифты.
        /// Явный путь → строгая загрузка; иначе берём первую тему проекта (детерминированно по GUID-порядку).
        static ThemeStyleSheet ResolveTheme(string themePath, out string error)
        {
            error = null;
            if (!string.IsNullOrEmpty(themePath))
            {
                var explicitTheme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
                if (explicitTheme == null)
                {
                    error = "ThemeStyleSheet not found: " + themePath;
                }
                return explicitTheme;
            }
            var guids = AssetDatabase.FindAssets("t:ThemeStyleSheet");
            if (guids.Length == 0)
            {
                error = "no ThemeStyleSheet (.tss) in project; pass 'theme' explicitly";
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // Порядок критичен и проверен эмпирически: только полная цепочка рисует offscreen-панель в edit-режиме.
        // Одни ValidateLayout+Update (без UpdateForRepaint/Render) дают посчитанный layout, но пустую RT.
        static readonly string[] PanelSteps = { "ValidateLayout", "UpdateForRepaint", "Update", "Render" };

        /// Форсит layout+repaint панели через internal API (версионно-зависимое — reflection с null-guard).
        /// true, если удалось вызвать хотя бы рисующий шаг (UpdateForRepaint/Render); иначе рендера не будет.
        static bool RenderPanel(IPanel panel)
        {
            if (panel == null)
            {
                return false;
            }
            var panelType = panel.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            bool painted = false;
            foreach (var step in PanelSteps)
            {
                var method = panelType.GetMethod(step, flags, null, Type.EmptyTypes, null);
                if (method == null)
                {
                    continue;
                }
                method.Invoke(panel, null);
                if (step == "UpdateForRepaint" || step == "Render")
                {
                    painted = true;
                }
            }
            var util = typeof(PanelSettings).Assembly.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
            var renderOffscreen = util?.GetMethod("RenderOffscreenPanels", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            renderOffscreen?.Invoke(null, null);
            return painted;
        }

        static JObject ImageContent(byte[] png, string caption) => new JObject
        {
            ["_content"] = new JArray
            {
                new JObject { ["type"] = "image", ["data"] = Convert.ToBase64String(png), ["mimeType"] = "image/png" },
                new JObject { ["type"] = "text", ["text"] = caption }
            }
        };
    }
}
