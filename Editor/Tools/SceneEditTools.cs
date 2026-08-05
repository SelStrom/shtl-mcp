using System;
using System.Collections.Generic;
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

        // true — записано; false — не записано, причина в error.
        public static bool Write(SerializedProperty p, JToken v, out string error)
        {
            error = null;
            switch (p.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                {
                    return WriteObjectRef(p, v, out error);
                }
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
                    error = "unsupported property type for write: " + p.propertyType;
                    return false;
                }
            }
        }

        /// Ссылочное поле: значение — та же ссылка, что и target у get_object/modify_object
        /// (scene-GO по пути/имени, asset-path, instanceId), null снимает ссылку. Если поле ждёт
        /// компонент, а указан GameObject — берём с него компонент нужного типа.
        static bool WriteObjectRef(SerializedProperty p, JToken v, out string error)
        {
            error = null;
            if (v == null || v.Type == JTokenType.Null)
            {
                p.objectReferenceValue = null;
                return true;
            }

            var obj = ObjectRefs.Resolve(v, out var resolveErr);
            if (obj == null)
            {
                error = resolveErr;
                return false;
            }

            var expected = ExpectedType(p);
            if (expected == null)
            {
                // Тип поля не разрешился (встроенный ресурс, тип из выгруженной сборки) — пишем как есть:
                // несовместимую ссылку Unity отбросит, и это видно в results ответа.
                p.objectReferenceValue = obj;
                return true;
            }

            if (!expected.IsInstanceOfType(obj) && obj is GameObject go)
            {
                var component = go.GetComponent(expected);
                if (component != null)
                {
                    obj = component;
                }
            }

            if (!expected.IsInstanceOfType(obj))
            {
                error = "value type mismatch: property expects " + expected.Name + ", got " + obj.GetType().Name;
                return false;
            }

            p.objectReferenceValue = obj;
            return true;
        }

        /// Тип ссылочного поля из SerializedProperty.type — строки вида "PPtr&lt;$Material&gt;".
        /// Возвращает null, если имя не распознано или тип не найден.
        static Type ExpectedType(SerializedProperty p)
        {
            var raw = p.type;
            const string prefix = "PPtr<$";
            if (string.IsNullOrEmpty(raw) || !raw.StartsWith(prefix, StringComparison.Ordinal) || !raw.EndsWith(">", StringComparison.Ordinal))
            {
                return null;
            }

            var name = raw.Substring(prefix.Length, raw.Length - prefix.Length - 1);
            return TypeResolve.Find(name, null, typeof(UnityEngine.Object), out _);
        }
    }

    /// Резолв Unity Object по ссылке: scene-GO (путь/имя) → asset-path (Assets/, Packages/) → instanceId
    /// (число или числовая строка). Обобщение target'а get_object/modify_object на ассеты (AC3.11).
    internal static class ObjectRefs
    {
        public static UnityEngine.Object Resolve(JToken target, out string error)
        {
            error = null;
            if (target == null || target.Type == JTokenType.Null)
            {
                error = "target is required";
                return null;
            }
            if (target.Type == JTokenType.Integer)
            {
                return ById((long)target, out error);
            }
            var s = (string)target;
            if (string.IsNullOrEmpty(s))
            {
                error = "target is required";
                return null;
            }
            if (s.StartsWith("Assets/", StringComparison.Ordinal) || s.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var byPath = AssetDatabase.LoadMainAssetAtPath(s);
                if (byPath == null)
                {
                    error = "no asset at path: " + s;
                }
                return byPath;
            }
            var go = SceneObjects.Resolve(s);
            if (go != null)
            {
                return go;
            }
            if (long.TryParse(s, out var id))
            {
                return ById(id, out error);
            }
            error = "target not found: " + s;
            return null;
        }

        static UnityEngine.Object ById(long id, out string error)
        {
            error = null;
            var obj = EditorUtility.InstanceIDToObject((int)id);
            if (obj == null)
            {
                error = "no object with instanceId " + id;
            }
            return obj;
        }
    }

    /// Serialized-свойства объекта (AC3.11): scene-GO → по компонентам; ассет/instanceId (ScriptableObject,
    /// материал, конфиг) → собственные свойства. Вложенные структуры/массивы — до maxDepth, с бюджетом ответа.
    public sealed class GetObjectTool : ITool
    {
        const int TopCap = 40;   // top-level свойств на компонент/объект
        const int ArrayCap = 25; // элементов массива на свойство
        const int Budget = 600;  // всего значений в ответе

        public string Name => "get_object";
        public string Description =>
            "Inspect serialized properties. Target: scene GameObject (path/name), asset path ('Assets/...') " +
            "or instanceId. GameObject → per-component properties; other objects (ScriptableObject, material, " +
            "config) → own properties. 'maxDepth' expands nested structs/arrays (default 2).";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["target"] = new JObject
                {
                    ["type"] = new JArray { "string", "integer" },
                    ["description"] = "GameObject path/name, asset path, or instanceId."
                },
                ["maxDepth"] = new JObject { ["type"] = "integer", ["description"] = "Nesting depth for structs/arrays (default 2)." }
            },
            ["required"] = new JArray { "target" }
        };

        public JObject Invoke(JObject args)
        {
            var obj = ObjectRefs.Resolve(args["target"], out var err);
            if (obj == null)
            {
                return new JObject { ["error"] = err };
            }
            int maxDepth = args["maxDepth"] != null ? (int)args["maxDepth"] : 2;
            int budget = Budget;

            if (obj is GameObject go)
            {
                var comps = new JArray();
                foreach (var c in go.GetComponents<Component>())
                {
                    if (c == null)
                    {
                        continue;
                    }
                    comps.Add(new JObject
                    {
                        ["type"] = c.GetType().Name,
                        ["instanceId"] = c.GetInstanceID(),
                        ["properties"] = Props(c, maxDepth, ref budget)
                    });
                }
                return new JObject
                {
                    ["path"] = SceneObjects.PathOf(go),
                    ["instanceId"] = go.GetInstanceID(),
                    ["components"] = comps,
                    ["truncated"] = budget <= 0
                };
            }

            var o = new JObject
            {
                ["name"] = obj.name,
                ["type"] = obj.GetType().Name,
                ["instanceId"] = obj.GetInstanceID(),
                ["properties"] = Props(obj, maxDepth, ref budget),
                ["truncated"] = budget <= 0
            };
            var assetPath = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(assetPath))
            {
                o["assetPath"] = assetPath;
            }
            return o;
        }

        static JObject Props(UnityEngine.Object host, int maxDepth, ref int budget)
        {
            var props = new JObject();
            var so = new SerializedObject(host);
            var it = so.GetIterator();
            bool more = it.NextVisible(true);
            int n = 0;
            while (more && n < TopCap && budget > 0)
            {
                if (it.name != "m_Script")
                {
                    props[it.name] = ReadTree(it, 1, maxDepth, ref budget);
                    n++;
                }
                more = it.NextVisible(false);
            }
            return props;
        }

        static JToken ReadTree(SerializedProperty p, int depth, int maxDepth, ref int budget)
        {
            budget--;
            if (p.propertyType == SerializedPropertyType.Generic && depth < maxDepth)
            {
                if (p.isArray)
                {
                    var arr = new JArray();
                    int n = Math.Min(p.arraySize, ArrayCap);
                    for (int i = 0; i < n && budget > 0; i++)
                    {
                        arr.Add(ReadTree(p.GetArrayElementAtIndex(i), depth + 1, maxDepth, ref budget));
                    }
                    if (p.arraySize > n)
                    {
                        arr.Add("… +" + (p.arraySize - n) + " more");
                    }
                    return arr;
                }
                var o = new JObject();
                var child = p.Copy();
                var end = p.GetEndProperty();
                bool enter = child.NextVisible(true);
                while (enter && !SerializedProperty.EqualContents(child, end) && budget > 0)
                {
                    o[child.name] = ReadTree(child, depth + 1, maxDepth, ref budget);
                    enter = child.NextVisible(false);
                }
                return o;
            }
            if (p.propertyType == SerializedPropertyType.Generic && p.isArray)
            {
                return "array[" + p.arraySize + "]"; // глубина исчерпана
            }
            return SerializedValues.Read(p);
        }
    }

    /// Записать serialized-свойства через SerializedObject (AC3.11): одиночная форма (component/property/
    /// value) или bulk 'changes' с вложенными путями ('m_Size.x') — транзакционно (всё или ничего). Target —
    /// scene-GO, asset-path или instanceId; правки ассетов сохраняются на диск.
    public sealed class ModifyObjectTool : ITool
    {
        public string Name => "modify_object";
        public string Description =>
            "Set serialized properties via SerializedObject. Target: scene GameObject (path/name), asset path " +
            "or instanceId. Single form: component+property+value. Bulk form: 'changes' " +
            "[{component?, property, value}, ...] applied as one transaction (all or nothing). Property paths " +
            "may be nested: 'm_Size.x', 'items.Array.data[0]'. Omit 'component' for non-GameObject targets.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["target"] = new JObject
                {
                    ["type"] = new JArray { "string", "integer" },
                    ["description"] = "GameObject path/name, asset path, or instanceId."
                },
                ["component"] = new JObject { ["type"] = "string", ["description"] = "Component type name (GameObject targets, single form), e.g. 'BoxCollider'." },
                ["property"] = new JObject { ["type"] = "string", ["description"] = "Serialized property path, e.g. 'm_IsTrigger' or 'm_Size.x' (single form)." },
                ["value"] = new JObject { ["description"] = "New value (int/bool/float/string/enum-index/[x,y,z]/[r,g,b,a]) (single form)." },
                ["changes"] = new JObject { ["type"] = "array", ["description"] = "Bulk form: [{component?, property, value}, ...]." }
            },
            ["required"] = new JArray { "target" }
        };

        public JObject Invoke(JObject args)
        {
            var obj = ObjectRefs.Resolve(args["target"], out var resolveErr);
            if (obj == null)
            {
                return new JObject { ["error"] = resolveErr };
            }

            var changes = new List<JObject>();
            if (args["changes"] is JArray arr)
            {
                foreach (var c in arr)
                {
                    if (!(c is JObject co) || co["property"] == null || co["value"] == null)
                    {
                        return new JObject { ["error"] = "each change needs 'property' and 'value'" };
                    }
                    changes.Add(co);
                }
            }
            else if (args["property"] != null && args["value"] != null)
            {
                changes.Add(new JObject { ["component"] = args["component"], ["property"] = args["property"], ["value"] = args["value"] });
            }
            if (changes.Count == 0)
            {
                return new JObject { ["error"] = "provide 'changes' array or component+property+value" };
            }

            // Фаза 1: зарезолвить все хосты и свойства ДО записи — транзакционность (всё или ничего).
            var sos = new Dictionary<UnityEngine.Object, SerializedObject>();
            var writes = new List<(SerializedProperty prop, JToken value, string path)>();
            foreach (var c in changes)
            {
                var host = ResolveHost(obj, (string)c["component"], out var err);
                if (host == null)
                {
                    return new JObject { ["error"] = err };
                }
                if (!sos.TryGetValue(host, out var so))
                {
                    so = new SerializedObject(host);
                    sos[host] = so;
                }
                var propPath = (string)c["property"];
                var p = so.FindProperty(propPath);
                if (p == null)
                {
                    return new JObject { ["error"] = "property not found: " + propPath };
                }
                writes.Add((p, c["value"], propPath));
            }

            // Фаза 2: записать всё; ошибка → ApplyModifiedProperties не вызывается ни для одного хоста.
            foreach (var w in writes)
            {
                try
                {
                    if (!SerializedValues.Write(w.prop, w.value, out var writeErr))
                    {
                        return new JObject { ["error"] = writeErr + " (" + w.path + ")" };
                    }
                }
                catch (Exception e)
                {
                    // value не подходит под тип проперти (строка для int, короткий массив для Vector и т.п.)
                    return new JObject { ["error"] = "value does not match property type " + w.prop.propertyType + " (" + w.path + "): " + e.Message };
                }
            }

            foreach (var kv in sos)
            {
                kv.Value.ApplyModifiedProperties();
                PersistDirty(kv.Key);
            }

            var results = new JArray();
            foreach (var w in writes)
            {
                results.Add(new JObject { ["property"] = w.path, ["value"] = SerializedValues.Read(w.prop) });
            }
            var res = new JObject { ["modified"] = true, ["applied"] = writes.Count, ["results"] = results };
            if (writes.Count == 1)
            {
                // совместимость одиночной формы
                res["property"] = writes[0].path;
                res["value"] = SerializedValues.Read(writes[0].prop);
            }
            return res;
        }

        static UnityEngine.Object ResolveHost(UnityEngine.Object target, string component, out string error)
        {
            error = null;
            if (target is GameObject go)
            {
                if (string.IsNullOrEmpty(component))
                {
                    error = "component is required for GameObject targets";
                    return null;
                }
                var comp = go.GetComponents<Component>().FirstOrDefault(c =>
                    c != null && (c.GetType().Name == component || c.GetType().FullName == component));
                if (comp == null)
                {
                    error = "component not found: " + component;
                }
                return comp;
            }
            if (!string.IsNullOrEmpty(component))
            {
                error = "'component' applies to GameObject targets only (target is " + target.GetType().Name + ")";
                return null;
            }
            return target;
        }

        static void PersistDirty(UnityEngine.Object host)
        {
            var go = host as GameObject ?? (host as Component)?.gameObject;
            if (go != null && go.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(go.scene);
                return;
            }
            EditorUtility.SetDirty(host);
            AssetDatabase.SaveAssetIfDirty(host); // правка ассета видна на диске сразу (верифицируема read_asset'ом)
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

    /// Установить выделение из путей/имён объектов сцены или из путей ассетов.
    public sealed class SetSelectionTool : ITool
    {
        public string Name => "set_selection";
        public string Description => "Set the editor selection from GameObject paths/names or asset paths ('targets' array, or single 'target').";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["targets"] = new JObject { ["type"] = "array", ["description"] = "GameObject paths/names or asset paths." },
                ["target"] = new JObject { ["type"] = "string", ["description"] = "Single GameObject path/name or asset path." }
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
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(name);
                if (asset != null)
                {
                    objs.Add(asset);
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
