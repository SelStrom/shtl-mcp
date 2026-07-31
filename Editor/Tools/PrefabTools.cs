using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    /// Закрыть prefab-stage (вернуться в основную сцену). Modal-free: грязный стейдж не открывает диалог.
    public sealed class ClosePrefabTool : ITool
    {
        public string Name => "close_prefab";
        public string Description =>
            "Close the prefab stage and return to the main scene. Modal-free: with unsaved changes, " +
            "'policy' decides — discard (default, changes are lost) | save (write to the asset first) | " +
            "abort (refuse and report). Check dirtiness via the 'context' of get_hierarchy.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["policy"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "For unsaved stage changes: discard (default) | save | abort."
                }
            }
        };

        public JObject Invoke(JObject args)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                return new JObject { ["closed"] = false, ["note"] = "no prefab stage is open" };
            }

            var policy = (string)args["policy"];
            if (string.IsNullOrEmpty(policy))
            {
                policy = "discard";
            }
            if (policy != "discard" && policy != "save" && policy != "abort")
            {
                return new JObject { ["error"] = "unknown policy: " + policy + " (expected discard | save | abort)" };
            }

            bool dirty = stage.scene.isDirty;
            bool saved = false;
            if (dirty)
            {
                if (policy == "abort")
                {
                    return new JObject { ["error"] = "prefab stage has unsaved changes; pass policy 'save' or 'discard'", ["dirty"] = true };
                }
                if (policy == "save")
                {
                    PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath, out saved);
                    if (!saved)
                    {
                        return new JObject { ["error"] = "saving the prefab stage failed; nothing closed", ["path"] = stage.assetPath };
                    }
                }
                // Снимаем флаг ДО GoToMainStage: иначе Unity поднимает блокирующий "Prefab Has Been Modified"
                // и подвешивает главный поток (headless — зависание, batch — Cancelling DisplayDialogComplex).
                if (!ClearDirtiness(stage))
                {
                    return new JObject
                    {
                        ["error"] = "cannot clear the stage dirty flag on this Unity version; " +
                                    "closing now would open a blocking dialog. Use save_prefab, then close_prefab.",
                        ["dirty"] = true
                    };
                }
            }

            StageUtility.GoToMainStage();
            return new JObject { ["closed"] = true, ["wasDirty"] = dirty, ["saved"] = saved };
        }

        static MethodInfo _stageClearDirtiness;
        static MethodInfo _sceneClearDirtiness;
        static bool _clearProbed;

        /// Сброс dirty-флага стейджа: публичного API нет ни в 2022, ни в 6 — идём рефлексией по
        /// internal (как ScreenshotTool за размером Game View). Не нашли — честно отказываем,
        /// а не проваливаемся в модальный диалог.
        static bool ClearDirtiness(PrefabStage stage)
        {
            if (!_clearProbed)
            {
                _clearProbed = true;
                _stageClearDirtiness = typeof(PrefabStage).GetMethod(
                    "ClearDirtiness", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, System.Type.EmptyTypes, null);
                _sceneClearDirtiness = typeof(EditorSceneManager).GetMethod(
                    "ClearSceneDirtiness", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(Scene) }, null);
            }
            try
            {
                if (_stageClearDirtiness != null)
                {
                    _stageClearDirtiness.Invoke(stage, null);
                    return !stage.scene.isDirty;
                }
                if (_sceneClearDirtiness != null)
                {
                    _sceneClearDirtiness.Invoke(null, new object[] { stage.scene });
                    return !stage.scene.isDirty;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[shtl-mcp] close_prefab: не удалось снять dirty-флаг стейджа: " + e.Message);
            }
            return false;
        }
    }

    /// Инстанцировать prefab в текущий контекст (prefab-stage, иначе активная сцена), опц. под родителя.
    public sealed class InstantiatePrefabTool : ITool
    {
        public string Name => "instantiate_prefab";
        public string Description =>
            "Instantiate a prefab asset into the current editing context (the open prefab stage if there is " +
            "one, otherwise the active scene). Optional 'parent' (GameObject path or name) parents the " +
            "instance, keeping the prefab's authored local transform. Returns the instance path.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["path"] = new JObject { ["type"] = "string", ["description"] = "Prefab asset path to instantiate." },
                ["parent"] = new JObject { ["type"] = "string", ["description"] = "Optional parent GameObject (path or name) in the current context." }
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

            // Родителя резолвим ДО создания инстанса: иначе при опечатке в имени в сцене остаётся мусор.
            Transform parent = null;
            var parentRef = (string)args["parent"];
            if (!string.IsNullOrEmpty(parentRef))
            {
                var parentGo = SceneObjects.Resolve(parentRef);
                if (parentGo == null)
                {
                    return new JObject { ["error"] = "parent not found: " + parentRef, ["context"] = SceneObjects.ContextInfo() };
                }
                parent = parentGo.transform;
            }

            var inst = PrefabUtility.InstantiatePrefab(prefab, SceneObjects.TargetScene()) as GameObject;
            if (inst == null)
            {
                return new JObject { ["error"] = "InstantiatePrefab failed" };
            }
            if (parent != null)
            {
                // worldPositionStays: false — у свежего инстанса мирового трансформа «по смыслу» ещё нет,
                // а для UI сохранение мирового вместо авторского локального ломает якоря RectTransform.
                inst.transform.SetParent(parent, false);
            }
            EditorSceneManager.MarkSceneDirty(inst.scene);
            return new JObject
            {
                ["instantiated"] = true,
                ["name"] = inst.name,
                ["path"] = SceneObjects.PathOf(inst),
                ["context"] = SceneObjects.ContextInfo()
            };
        }
    }
}
