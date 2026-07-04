using System;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Shtl.Mcp.Tools
{
    /// Общее для multi-scene тулов (AC3.13): поиск открытой сцены по path или имени, описание сцены.
    internal static class OpenScenes
    {
        public static Scene Find(string key, out string error)
        {
            error = null;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == key || s.name == key)
                {
                    return s;
                }
            }
            error = "no open scene with path or name: " + key;
            return default;
        }

        public static JObject Describe(Scene s) => new JObject
        {
            ["name"] = s.name,
            ["path"] = s.path, // пусто = сцена ни разу не сохранялась (untitled)
            ["isLoaded"] = s.isLoaded,
            ["isDirty"] = s.isDirty,
            ["isActive"] = s == SceneManager.GetActiveScene(),
            ["rootCount"] = s.isLoaded ? s.rootCount : 0
        };
    }

    /// Открытые сцены (аддитивный multi-scene setup): path/isLoaded/isActive/isDirty.
    public sealed class ListScenesTool : ITool
    {
        public string Name => "list_scenes";
        public string Description => "List open scenes (multi-scene setup): name, path, isLoaded, isDirty, isActive, rootCount.";
        public bool NeedsMainThread => true;
        public JObject InputSchema => new JObject { ["type"] = "object", ["properties"] = new JObject() };

        public JObject Invoke(JObject args)
        {
            var scenes = new JArray();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                scenes.Add(OpenScenes.Describe(SceneManager.GetSceneAt(i)));
            }
            return new JObject { ["count"] = SceneManager.sceneCount, ["scenes"] = scenes };
        }
    }

    /// Создать новую пустую сцену аддитивно (текущие остаются открытыми); опц. сохранить в asset.
    public sealed class CreateSceneTool : ITool
    {
        public string Name => "create_scene";
        public string Description =>
            "Create a new empty scene additively (current scenes stay open; the new scene becomes active). " +
            "Optional 'path' saves it as a scene asset ('Assets/....unity'). Edit mode only.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["path"] = new JObject { ["type"] = "string", ["description"] = "Optional save-as path, e.g. 'Assets/Scenes/New.unity'." }
            }
        };

        public JObject Invoke(JObject args)
        {
            var path = (string)args["path"];
            Scene scene;
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            }
            catch (Exception e)
            {
                // play mode / второй untitled — Unity бросает InvalidOperationException
                return new JObject { ["error"] = "could not create scene: " + e.Message };
            }
            if (!string.IsNullOrEmpty(path))
            {
                bool saved;
                try { saved = EditorSceneManager.SaveScene(scene, path); }
                catch (Exception e) { return new JObject { ["error"] = "scene created but save failed: " + e.Message }; }
                if (!saved)
                {
                    return new JObject { ["error"] = "scene created but could not be saved to: " + path };
                }
            }
            var o = OpenScenes.Describe(scene);
            o["created"] = true;
            return o;
        }
    }

    /// Выгрузить открытую сцену (несохранённые изменения теряются — как open_scene; см. sceneDirty/AC4.9).
    public sealed class UnloadSceneTool : ITool
    {
        public string Name => "unload_scene";
        public string Description =>
            "Close an open scene by path or name (unsaved changes in it are lost — check list_scenes isDirty " +
            "and save_scene first). The last open scene cannot be closed.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["scene"] = new JObject { ["type"] = "string", ["description"] = "Scene path ('Assets/....unity') or name." }
            },
            ["required"] = new JArray { "scene" }
        };

        public JObject Invoke(JObject args)
        {
            var key = (string)args["scene"];
            if (string.IsNullOrEmpty(key))
            {
                return new JObject { ["error"] = "scene is required" };
            }
            var scene = OpenScenes.Find(key, out var err);
            if (err != null)
            {
                return new JObject { ["error"] = err };
            }
            if (SceneManager.sceneCount == 1)
            {
                return new JObject { ["error"] = "cannot close the only open scene" };
            }
            bool wasDirty = scene.isDirty;
            var path = scene.path;
            var name = scene.name;
            bool ok;
            try { ok = EditorSceneManager.CloseScene(scene, true); }
            catch (Exception e) { return new JObject { ["error"] = "could not close scene: " + e.Message }; }
            if (!ok)
            {
                return new JObject { ["error"] = "Unity refused to close scene: " + key };
            }
            return new JObject { ["unloaded"] = true, ["name"] = name, ["path"] = path, ["discardedUnsavedChanges"] = wasDirty };
        }
    }

    /// Сделать открытую загруженную сцену активной (в неё идут новые объекты).
    public sealed class SetActiveSceneTool : ITool
    {
        public string Name => "set_active_scene";
        public string Description => "Make an open loaded scene the active one (new GameObjects go there). Path or name.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["scene"] = new JObject { ["type"] = "string", ["description"] = "Scene path ('Assets/....unity') or name." }
            },
            ["required"] = new JArray { "scene" }
        };

        public JObject Invoke(JObject args)
        {
            var key = (string)args["scene"];
            if (string.IsNullOrEmpty(key))
            {
                return new JObject { ["error"] = "scene is required" };
            }
            var scene = OpenScenes.Find(key, out var err);
            if (err != null)
            {
                return new JObject { ["error"] = err };
            }
            if (!scene.isLoaded)
            {
                return new JObject { ["error"] = "scene is not loaded: " + key };
            }
            // SetActiveScene возвращает false для УЖЕ активной сцены — идемпотентный успех, не ошибка
            if (SceneManager.GetActiveScene() != scene && !SceneManager.SetActiveScene(scene))
            {
                return new JObject { ["error"] = "Unity refused to activate scene: " + key };
            }
            var o = OpenScenes.Describe(scene);
            o["activated"] = true;
            return o;
        }
    }
}
