using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shtl.Mcp.Tools
{
    /// Чтение/запись SerializedProperty общих типов (int/bool/float/string/enum/Vector/Color/objref).
    internal static class SerializedValues
    {
        public static JToken Read(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: return p.intValue;
                case SerializedPropertyType.Boolean: return p.boolValue;
                case SerializedPropertyType.Float: return p.floatValue;
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Enum:
                    return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumNames.Length
                        ? p.enumNames[p.enumValueIndex] : (JToken)p.enumValueIndex;
                case SerializedPropertyType.Vector2: return new JArray { p.vector2Value.x, p.vector2Value.y };
                case SerializedPropertyType.Vector3: return new JArray { p.vector3Value.x, p.vector3Value.y, p.vector3Value.z };
                case SerializedPropertyType.Vector4: return new JArray { p.vector4Value.x, p.vector4Value.y, p.vector4Value.z, p.vector4Value.w };
                case SerializedPropertyType.Color: return new JArray { p.colorValue.r, p.colorValue.g, p.colorValue.b, p.colorValue.a };
                case SerializedPropertyType.ObjectReference:
                    return p.objectReferenceValue != null ? p.objectReferenceValue.name : null;
                default: return p.propertyType.ToString();
            }
        }

        // true — записано; false — тип не поддержан для записи.
        public static bool Write(SerializedProperty p, JToken v)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer:
                {
                    p.intValue = (int)v;
                    return true;
                }
                case SerializedPropertyType.Boolean:
                {
                    p.boolValue = (bool)v;
                    return true;
                }
                case SerializedPropertyType.Float:
                {
                    p.floatValue = (float)v;
                    return true;
                }
                case SerializedPropertyType.String:
                {
                    p.stringValue = (string)v;
                    return true;
                }
                case SerializedPropertyType.Enum:
                {
                    p.enumValueIndex = (int)v;
                    return true;
                }
                case SerializedPropertyType.Vector2:
                {
                    p.vector2Value = new Vector2((float)v[0], (float)v[1]);
                    return true;
                }
                case SerializedPropertyType.Vector3:
                {
                    p.vector3Value = new Vector3((float)v[0], (float)v[1], (float)v[2]);
                    return true;
                }
                case SerializedPropertyType.Color:
                {
                    p.colorValue = new Color((float)v[0], (float)v[1], (float)v[2], v.Count() > 3 ? (float)v[3] : 1f);
                    return true;
                }
                default:
                {
                    return false;
                }
            }
        }
    }

    /// Компоненты объекта + их top-level serialized-свойства со значениями.
    public sealed class GetObjectTool : ITool
    {
        const int PropCap = 40;

        public string Name => "get_object";
        public string Description => "Inspect a GameObject's components and their serialized properties (by path or name).";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["target"] = new JObject { ["type"] = "string", ["description"] = "GameObject path or name." }
            },
            ["required"] = new JArray { "target" }
        };

        public JObject Invoke(JObject args)
        {
            var go = SceneObjects.Resolve((string)args["target"]);
            if (go == null)
            {
                return new JObject { ["error"] = "target not found: " + (string)args["target"] };
            }

            var comps = new JArray();
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null)
                {
                    continue;
                }
                var props = new JObject();
                var so = new SerializedObject(c);
                var it = so.GetIterator();
                bool more = it.NextVisible(true);
                int n = 0;
                while (more && n < PropCap)
                {
                    if (it.name != "m_Script")
                    {
                        props[it.name] = SerializedValues.Read(it);
                        n++;
                    }
                    more = it.NextVisible(false);
                }
                comps.Add(new JObject { ["type"] = c.GetType().Name, ["properties"] = props });
            }
            return new JObject { ["path"] = SceneObjects.PathOf(go), ["components"] = comps };
        }
    }

    /// Записать serialized-свойство компонента через SerializedObject.
    public sealed class ModifyObjectTool : ITool
    {
        public string Name => "modify_object";
        public string Description =>
            "Set a serialized property on a component via SerializedObject. " +
            "Args: target, component (type name), property (name), value.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["target"] = new JObject { ["type"] = "string", ["description"] = "GameObject path or name." },
                ["component"] = new JObject { ["type"] = "string", ["description"] = "Component type name, e.g. 'BoxCollider'." },
                ["property"] = new JObject { ["type"] = "string", ["description"] = "Serialized property name, e.g. 'm_IsTrigger'." },
                ["value"] = new JObject { ["description"] = "New value (int/bool/float/string/enum-index/[x,y,z]/[r,g,b,a])." }
            },
            ["required"] = new JArray { "target", "component", "property", "value" }
        };

        public JObject Invoke(JObject args)
        {
            var go = SceneObjects.Resolve((string)args["target"]);
            if (go == null)
            {
                return new JObject { ["error"] = "target not found: " + (string)args["target"] };
            }
            var comp = go.GetComponent((string)args["component"]);
            if (comp == null)
            {
                return new JObject { ["error"] = "component not found: " + (string)args["component"] };
            }
            var so = new SerializedObject(comp);
            var p = so.FindProperty((string)args["property"]);
            if (p == null)
            {
                return new JObject { ["error"] = "property not found: " + (string)args["property"] };
            }
            try
            {
                if (!SerializedValues.Write(p, args["value"]))
                {
                    return new JObject { ["error"] = "unsupported property type for write: " + p.propertyType };
                }
            }
            catch (Exception e)
            {
                // value не подходит под тип проперти (строка для int, короткий массив для Vector и т.п.)
                return new JObject { ["error"] = "value does not match property type " + p.propertyType + ": " + e.Message };
            }
            so.ApplyModifiedProperties();
            SceneObjects.MarkDirty(go);
            return new JObject { ["modified"] = true, ["property"] = (string)args["property"], ["value"] = SerializedValues.Read(p) };
        }
    }

    /// Открыть сцену (заменяет текущую; несохранённые изменения теряются).
    public sealed class OpenSceneTool : ITool
    {
        public string Name => "open_scene";
        public string Description => "Open a scene by path (replaces the current scene; unsaved changes are lost).";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["path"] = new JObject { ["type"] = "string", ["description"] = "Scene asset path, e.g. 'Assets/Main.unity'." }
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
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                return new JObject { ["error"] = "could not open scene: " + path };
            }
            return new JObject { ["opened"] = true, ["scene"] = scene.name, ["path"] = path };
        }
    }

    /// Сохранить активную сцену (опц. как новый путь).
    public sealed class SaveSceneTool : ITool
    {
        public string Name => "save_scene";
        public string Description => "Save the active scene. Optional 'path' saves it as a new scene asset.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["path"] = new JObject { ["type"] = "string", ["description"] = "Optional save-as path." }
            }
        };

        public JObject Invoke(JObject args)
        {
            var scene = SceneManager.GetActiveScene();
            var path = (string)args["path"];
            if (string.IsNullOrEmpty(scene.path) && string.IsNullOrEmpty(path))
            {
                // никогда не сохранявшаяся сцена + нет save-as path → SaveScene открыл бы модальный диалог
                // и подвесил главный поток (headless — зависание). Требуем явный path.
                return new JObject { ["error"] = "active scene has never been saved; provide 'path' to save it as a new scene asset" };
            }
            bool ok = string.IsNullOrEmpty(path)
                ? EditorSceneManager.SaveScene(scene)
                : EditorSceneManager.SaveScene(scene, path);
            return new JObject { ["saved"] = ok, ["scene"] = scene.name, ["path"] = string.IsNullOrEmpty(scene.path) ? path : scene.path };
        }
    }

    /// Текущее выделение в редакторе (GameObjects).
    public sealed class GetSelectionTool : ITool
    {
        public string Name => "get_selection";
        public string Description => "Get the current editor GameObject selection.";
        public bool NeedsMainThread => true;
        public JObject InputSchema => new JObject { ["type"] = "object", ["properties"] = new JObject() };

        public JObject Invoke(JObject args)
        {
            var arr = new JArray();
            foreach (var go in Selection.gameObjects)
            {
                arr.Add(SceneObjects.PathOf(go));
            }
            return new JObject { ["count"] = arr.Count, ["selection"] = arr };
        }
    }

    /// Установить выделение из путей/имён объектов сцены.
    public sealed class SetSelectionTool : ITool
    {
        public string Name => "set_selection";
        public string Description => "Set the editor selection from GameObject paths or names ('targets' array, or single 'target').";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["targets"] = new JObject { ["type"] = "array", ["description"] = "GameObject paths or names." },
                ["target"] = new JObject { ["type"] = "string", ["description"] = "Single GameObject path or name." }
            }
        };

        public JObject Invoke(JObject args)
        {
            var names = new JArray();
            if (args["targets"] is JArray ta)
            {
                foreach (var t in ta)
                {
                    names.Add(t);
                }
            }
            if (args["target"] != null)
            {
                names.Add(args["target"]);
            }

            var objs = new System.Collections.Generic.List<UnityEngine.Object>();
            var missing = new JArray();
            foreach (var n in names)
            {
                var name = n.Type == JTokenType.String ? (string)n : n.ToString();
                var go = SceneObjects.Resolve(name);
                if (go != null)
                {
                    objs.Add(go);
                }
                else
                {
                    missing.Add(name);
                }
            }
            Selection.objects = objs.ToArray();
            return new JObject { ["selected"] = objs.Count, ["missing"] = missing };
        }
    }
}
