using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shtl.Mcp.Tools
{
    /// Общие операции над объектами текущего контекста редактирования: резолв по пути (`A/B/C`) или
    /// имени (вкл. неактивные), путь объекта, JSON-описание, обход иерархии.
    /// Контекст — открытый prefab-stage, иначе активная сцена (AC3.16): пока стейдж открыт, Unity и в
    /// своей Hierarchy показывает его, а не сцену, поэтому тулы следуют за тем же контекстом.
    internal static class SceneObjects
    {
        /// Открытый prefab-stage или null. Единственная точка знания о стейдже.
        public static PrefabStage CurrentStage()
        {
            return PrefabStageUtility.GetCurrentPrefabStage();
        }

        /// Где сейчас работают тулы — чтобы «объект не найден» не выглядел загадкой.
        public static JObject ContextInfo()
        {
            var stage = CurrentStage();
            if (stage != null)
            {
                return new JObject
                {
                    ["kind"] = "prefabStage",
                    ["prefabPath"] = stage.assetPath,
                    ["root"] = stage.prefabContentsRoot != null ? stage.prefabContentsRoot.name : null,
                    ["dirty"] = stage.scene.isDirty
                };
            }
            var active = SceneManager.GetActiveScene();
            return new JObject
            {
                ["kind"] = "scene",
                ["scene"] = active.name,
                // пусто = сцена ни разу не сохранялась (untitled)
                ["scenePath"] = active.path,
                ["dirty"] = active.isDirty
            };
        }

        /// Сцена, куда попадают новые объекты: стейджа, если открыт, иначе активная.
        public static Scene TargetScene()
        {
            var stage = CurrentStage();
            return stage != null ? stage.scene : SceneManager.GetActiveScene();
        }

        public static GameObject Resolve(string target)
        {
            if (string.IsNullOrEmpty(target))
            {
                return null;
            }
            var byPath = ResolvePath(target);
            if (byPath != null)
            {
                return byPath;
            }
            return All().FirstOrDefault(g => g.name == target);
        }

        static GameObject ResolvePath(string path)
        {
            var parts = path.Split('/');
            var cur = Roots().FirstOrDefault(r => r.name == parts[0]);
            for (int i = 1; i < parts.Length && cur != null; i++)
            {
                cur = ChildNamed(cur.transform, parts[i]);
            }
            return cur;
        }

        static GameObject ChildNamed(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == name)
                {
                    return parent.GetChild(i).gameObject;
                }
            }
            return null;
        }

        static GameObject[] Roots()
        {
            return TargetScene().GetRootGameObjects();
        }

        public static IEnumerable<GameObject> All()
        {
            foreach (var root in Roots())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    yield return t.gameObject;
                }
            }
        }

        public static string PathOf(GameObject go)
        {
            var stack = new Stack<string>();
            var t = go.transform;
            while (t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }
            return string.Join("/", stack);
        }

        public static JObject Describe(GameObject go, bool components)
        {
            var o = new JObject
            {
                ["name"] = go.name,
                ["path"] = PathOf(go),
                ["active"] = go.activeSelf,
                ["instanceId"] = go.GetInstanceID()
            };
            if (components)
            {
                var comps = new JArray();
                foreach (var c in go.GetComponents<Component>())
                {
                    comps.Add(c != null ? c.GetType().Name : "<missing>");
                }
                o["components"] = comps;
            }
            return o;
        }

        public static JObject Node(GameObject go, int depth, int maxDepth, ref int budget)
        {
            var o = new JObject
            {
                ["name"] = go.name,
                ["active"] = go.activeSelf
            };
            budget--;
            if (depth < maxDepth && go.transform.childCount > 0 && budget > 0)
            {
                var children = new JArray();
                for (int i = 0; i < go.transform.childCount && budget > 0; i++)
                {
                    children.Add(Node(go.transform.GetChild(i).gameObject, depth + 1, maxDepth, ref budget));
                }
                o["children"] = children;
            }
            else if (go.transform.childCount > 0)
            {
                o["childCount"] = go.transform.childCount; // не развёрнуто (глубина/бюджет)
            }
            return o;
        }

        public static JArray RootNodes(GameObject scopeRoot, int maxDepth)
        {
            int budget = 2000; // ограничение размера ответа
            var arr = new JArray();
            if (scopeRoot != null)
            {
                arr.Add(Node(scopeRoot, 0, maxDepth, ref budget));
            }
            else
            {
                foreach (var r in Roots())
                {
                    arr.Add(Node(r, 0, maxDepth, ref budget));
                }
            }
            return arr;
        }

        public static void MarkDirty(GameObject go)
        {
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        public static Vector3 ToVec3(JToken t, Vector3 fallback)
        {
            if (t is JArray a && a.Count >= 3)
            {
                return new Vector3((float)a[0], (float)a[1], (float)a[2]);
            }
            return fallback;
        }
    }

    /// Дерево объектов текущего контекста (опц. от корня `root`, глубина `maxDepth`).
    public sealed class GetHierarchyTool : ITool
    {
        public string Name => "get_hierarchy";
        public string Description =>
            "Dump the current editing context as a tree (name, active, children): the open prefab stage if " +
            "there is one, otherwise the active scene. 'context' in the response says which. Optional 'root' " +
            "scopes to a subtree; 'maxDepth' limits depth (default 5).";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["root"] = new JObject { ["type"] = "string", ["description"] = "Optional GameObject (path or name) to scope to." },
                ["maxDepth"] = new JObject { ["type"] = "integer", ["description"] = "Max tree depth (default 5)." }
            }
        };

        public JObject Invoke(JObject args)
        {
            int maxDepth = args["maxDepth"] != null ? (int)args["maxDepth"] : 5;
            GameObject scope = null;
            var root = (string)args["root"];
            if (!string.IsNullOrEmpty(root))
            {
                scope = SceneObjects.Resolve(root);
                if (scope == null)
                {
                    return new JObject { ["error"] = "root not found: " + root };
                }
            }
            // Поля scene* берутся из контекста, а не из активной сцены: при открытом стейдже дерево — его,
            // и рассинхрон заголовка с содержимым читался бы как «объектов сцены нет».
            var target = SceneObjects.TargetScene();
            return new JObject
            {
                ["context"] = SceneObjects.ContextInfo(),
                ["scene"] = target.name,
                ["scenePath"] = target.path, // пусто = сцена ни разу не сохранялась (untitled) или это стейдж
                // sceneDirty (AC4.9): LLM решает проактивно — сохранить через save_scene перед разрушающей
                // операцией (open_scene/recompile молча отбросят несохранённое; блокирующего модала нет).
                ["sceneDirty"] = target.isDirty,
                ["tree"] = SceneObjects.RootNodes(scope, maxDepth)
            };
        }
    }

    /// Найти объекты текущего контекста по имени (вкл. неактивные).
    public sealed class FindGameObjectTool : ITool
    {
        public string Name => "find_gameobject";
        public string Description =>
            "Find GameObjects by exact name (including inactive) in the current editing context — the open " +
            "prefab stage if there is one, otherwise the active scene. Returns path, active, components, " +
            "and 'context' saying where the search ran.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["name"] = new JObject { ["type"] = "string", ["description"] = "Exact GameObject name." }
            },
            ["required"] = new JArray { "name" }
        };

        public JObject Invoke(JObject args)
        {
            var name = (string)args["name"];
            if (string.IsNullOrEmpty(name))
            {
                return new JObject { ["error"] = "name is required" };
            }
            var all = SceneObjects.All().Where(g => g.name == name).ToList();
            var matches = new JArray();
            foreach (var go in all.Take(200))
            {
                matches.Add(SceneObjects.Describe(go, true));
            }
            return new JObject
            {
                // context: пустой результат при открытом стейдже — почти всегда «искал не там», а не «нет объекта»
                ["context"] = SceneObjects.ContextInfo(),
                ["count"] = all.Count,
                ["truncated"] = all.Count > 200,
                ["matches"] = matches
            };
        }
    }

    /// Создать объект (опц. примитив, опц. родитель).
    public sealed class GameObjectCreateTool : ITool
    {
        public string Name => "gameobject_create";
        public string Description =>
            "Create a GameObject in the active scene. Optional 'primitive' (cube|sphere|capsule|cylinder|" +
            "plane|quad), optional 'parent' (path or name).";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["name"] = new JObject { ["type"] = "string", ["description"] = "Name for the new GameObject." },
                ["primitive"] = new JObject { ["type"] = "string", ["description"] = "Optional primitive type." },
                ["parent"] = new JObject { ["type"] = "string", ["description"] = "Optional parent (path or name)." }
            }
        };

        public JObject Invoke(JObject args)
        {
            var name = (string)args["name"];
            var prim = (string)args["primitive"];
            GameObject go;
            if (!string.IsNullOrEmpty(prim))
            {
                if (!System.Enum.TryParse<PrimitiveType>(Cap(prim), out var pt))
                {
                    return new JObject { ["error"] = "unknown primitive: " + prim };
                }
                go = GameObject.CreatePrimitive(pt);
                if (!string.IsNullOrEmpty(name))
                {
                    go.name = name;
                }
            }
            else
            {
                go = new GameObject(string.IsNullOrEmpty(name) ? "GameObject" : name);
            }

            var parent = (string)args["parent"];
            if (!string.IsNullOrEmpty(parent))
            {
                var p = SceneObjects.Resolve(parent);
                if (p == null)
                {
                    Object.DestroyImmediate(go);
                    return new JObject { ["error"] = "parent not found: " + parent };
                }
                go.transform.SetParent(p.transform, true);
            }
            SceneObjects.MarkDirty(go);
            return SceneObjects.Describe(go, false);
        }

        static string Cap(string s) => char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
    }

    /// Изменить объект: имя, активность, transform (position/rotation/scale как [x,y,z]).
    public sealed class GameObjectModifyTool : ITool
    {
        public string Name => "gameobject_modify";
        public string Description =>
            "Modify a GameObject (by path or name): name, active, position/rotation/scale ([x,y,z], local).";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["target"] = new JObject { ["type"] = "string", ["description"] = "GameObject path or name." },
                ["name"] = new JObject { ["type"] = "string", ["description"] = "New name." },
                ["active"] = new JObject { ["type"] = "boolean", ["description"] = "Set activeSelf." },
                ["position"] = new JObject { ["type"] = "array", ["description"] = "Local position [x,y,z]." },
                ["rotation"] = new JObject { ["type"] = "array", ["description"] = "Local euler angles [x,y,z]." },
                ["scale"] = new JObject { ["type"] = "array", ["description"] = "Local scale [x,y,z]." }
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
            if (args["name"] != null)
            {
                go.name = (string)args["name"];
            }
            if (args["active"] != null)
            {
                go.SetActive((bool)args["active"]);
            }
            var t = go.transform;
            if (args["position"] != null)
            {
                t.localPosition = SceneObjects.ToVec3(args["position"], t.localPosition);
            }
            if (args["rotation"] != null)
            {
                t.localEulerAngles = SceneObjects.ToVec3(args["rotation"], t.localEulerAngles);
            }
            if (args["scale"] != null)
            {
                t.localScale = SceneObjects.ToVec3(args["scale"], t.localScale);
            }
            SceneObjects.MarkDirty(go);
            return SceneObjects.Describe(go, false);
        }
    }

    /// Уничтожить объект.
    public sealed class GameObjectDestroyTool : ITool
    {
        public string Name => "gameobject_destroy";
        public string Description => "Destroy a GameObject (by path or name) and its children.";
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
            var scene = go.scene;
            var path = SceneObjects.PathOf(go);
            Object.DestroyImmediate(go);
            EditorSceneManager.MarkSceneDirty(scene);
            return new JObject { ["destroyed"] = true, ["path"] = path };
        }
    }

    /// Переродитель: parent=null/пусто → в корень. worldPositionStays по умолчанию true.
    public sealed class SetParentTool : ITool
    {
        public string Name => "set_parent";
        public string Description => "Reparent a GameObject. Empty 'parent' moves it to the scene root.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["child"] = new JObject { ["type"] = "string", ["description"] = "Child GameObject (path or name)." },
                ["parent"] = new JObject { ["type"] = "string", ["description"] = "New parent (path or name); empty = scene root." },
                ["worldPositionStays"] = new JObject { ["type"] = "boolean", ["description"] = "Keep world transform (default true)." }
            },
            ["required"] = new JArray { "child" }
        };

        public JObject Invoke(JObject args)
        {
            var child = SceneObjects.Resolve((string)args["child"]);
            if (child == null)
            {
                return new JObject { ["error"] = "child not found: " + (string)args["child"] };
            }
            bool wps = args["worldPositionStays"] == null || (bool)args["worldPositionStays"];
            var parentName = (string)args["parent"];
            if (string.IsNullOrEmpty(parentName))
            {
                child.transform.SetParent(null, wps);
            }
            else
            {
                var parent = SceneObjects.Resolve(parentName);
                if (parent == null)
                {
                    return new JObject { ["error"] = "parent not found: " + parentName };
                }
                child.transform.SetParent(parent.transform, wps);
            }
            SceneObjects.MarkDirty(child);
            return SceneObjects.Describe(child, false);
        }
    }
}
