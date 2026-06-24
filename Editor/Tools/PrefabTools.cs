using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shtl.Mcp.Tools
{
    /// Создать prefab-ассет: из именованного объекта сцены (`from`) либо новый пустой (root = name).
    public sealed class CreatePrefabTool : ITool
    {
        public string Name => "create_prefab";
        public string Description =>
            "Create a prefab asset at 'path'. From an existing scene GameObject ('from' by name), or a new " +
            "empty prefab whose root is named 'name'.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["path"] = new JObject { ["type"] = "string", ["description"] = "Target asset path, e.g. 'Assets/Foo.prefab'." },
                ["from"] = new JObject { ["type"] = "string", ["description"] = "Optional scene GameObject name to make into a prefab." },
                ["name"] = new JObject { ["type"] = "string", ["description"] = "Root name for a new empty prefab (when 'from' omitted)." }
            },
            ["required"] = new JArray { "path" }
        };

        public JObject Invoke(JObject args)
        {
            var path = (string)args["path"];
            if (string.IsNullOrEmpty(path))
            {
                return new JObject { ["error"] = "path is required" };
            }

            var from = (string)args["from"];
            if (!string.IsNullOrEmpty(from))
            {
                var src = GameObject.Find(from);
                if (src == null)
                {
                    return new JObject { ["error"] = "scene GameObject not found: " + from };
                }
                var saved = PrefabUtility.SaveAsPrefabAsset(src, path);
                return saved != null
                    ? new JObject { ["created"] = true, ["path"] = path, ["from"] = from }
                    : new JObject { ["error"] = "SaveAsPrefabAsset failed" };
            }

            var temp = new GameObject(string.IsNullOrEmpty((string)args["name"]) ? "Prefab" : (string)args["name"]);
            try
            {
                var saved = PrefabUtility.SaveAsPrefabAsset(temp, path);
                return saved != null
                    ? new JObject { ["created"] = true, ["path"] = path, ["empty"] = true }
                    : new JObject { ["error"] = "SaveAsPrefabAsset failed" };
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }
    }

    /// Открыть prefab в prefab-stage для редактирования.
    public sealed class OpenPrefabTool : ITool
    {
        public string Name => "open_prefab";
        public string Description => "Open a prefab asset in an isolated prefab stage for editing.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["path"] = new JObject { ["type"] = "string", ["description"] = "Prefab asset path to open." }
            },
            ["required"] = new JArray { "path" }
        };

        public JObject Invoke(JObject args)
        {
            var path = (string)args["path"];
            if (string.IsNullOrEmpty(path))
            {
                return new JObject { ["error"] = "path is required" };
            }
            var stage = PrefabStageUtility.OpenPrefab(path);
            if (stage == null)
            {
                return new JObject { ["error"] = "could not open prefab: " + path };
            }
            return new JObject { ["opened"] = true, ["path"] = path, ["root"] = stage.prefabContentsRoot.name };
        }
    }

    /// Сохранить открытый prefab-stage в ассет.
    public sealed class SavePrefabTool : ITool
    {
        public string Name => "save_prefab";
        public string Description => "Save the currently open prefab stage back to its asset.";
        public bool NeedsMainThread => true;
        public JObject InputSchema => new JObject { ["type"] = "object", ["properties"] = new JObject() };

        public JObject Invoke(JObject args)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                return new JObject { ["error"] = "no prefab stage is open" };
            }
            PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath, out bool ok);
            return new JObject { ["saved"] = ok, ["path"] = stage.assetPath };
        }
    }

    /// Закрыть prefab-stage (вернуться в основную сцену).
    public sealed class ClosePrefabTool : ITool
    {
        public string Name => "close_prefab";
        public string Description => "Close the prefab stage and return to the main scene.";
        public bool NeedsMainThread => true;
        public JObject InputSchema => new JObject { ["type"] = "object", ["properties"] = new JObject() };

        public JObject Invoke(JObject args)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                return new JObject { ["closed"] = false, ["note"] = "no prefab stage is open" };
            }
            StageUtility.GoToMainStage();
            return new JObject { ["closed"] = true };
        }
    }

    /// Инстанцировать prefab в активную сцену.
    public sealed class InstantiatePrefabTool : ITool
    {
        public string Name => "instantiate_prefab";
        public string Description => "Instantiate a prefab asset into the active scene.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["path"] = new JObject { ["type"] = "string", ["description"] = "Prefab asset path to instantiate." }
            },
            ["required"] = new JArray { "path" }
        };

        public JObject Invoke(JObject args)
        {
            var path = (string)args["path"];
            if (string.IsNullOrEmpty(path))
            {
                return new JObject { ["error"] = "path is required" };
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                return new JObject { ["error"] = "no prefab at path: " + path };
            }
            var inst = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (inst == null)
            {
                return new JObject { ["error"] = "InstantiatePrefab failed" };
            }
            EditorSceneManager.MarkSceneDirty(inst.scene);
            return new JObject { ["instantiated"] = true, ["name"] = inst.name };
        }
    }
}
