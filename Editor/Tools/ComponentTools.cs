using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Shtl.Mcp.Tools
{
    /// Добавить компонент на GameObject (AC3.10) — жизненный цикл, который modify_object не покрывает
    /// (тот пишет свойства существующего компонента). Возвращает ref добавленного (instanceId) для
    /// последующего modify_object.
    public sealed class AddComponentTool : ITool
    {
        public string Name => "add_component";

        public string Description =>
            "Add a component to a scene GameObject by type name (short 'Rigidbody2D' or full " +
            "'UnityEngine.Rigidbody2D'). Returns the added component's instanceId for modify_object.";

        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["target"] = new JObject { ["type"] = "string", ["description"] = "GameObject path or name." },
                ["type"] = new JObject { ["type"] = "string", ["description"] = "Component type name (short or full)." }
            },
            ["required"] = new JArray { "target", "type" }
        };

        public JObject Invoke(JObject args)
        {
            var go = SceneObjects.Resolve((string)args["target"]);
            if (go == null)
            {
                return new JObject { ["error"] = "target not found: " + (string)args["target"] };
            }
            var type = TypeResolve.Find((string)args["type"], null, typeof(Component), out var err);
            if (type == null)
            {
                return err;
            }
            if (type.IsAbstract)
            {
                return new JObject { ["error"] = "type is abstract, cannot add: " + type.FullName };
            }
            if (Attribute.IsDefined(type, typeof(DisallowMultipleComponent)) && go.GetComponent(type) != null)
            {
                return new JObject { ["error"] = "component disallows multiples (DisallowMultipleComponent) and already exists: " + type.FullName };
            }

            Component comp;
            try { comp = go.AddComponent(type); }
            catch (Exception e) { return new JObject { ["error"] = "AddComponent failed: " + e.Message }; }
            if (comp == null)
            {
                return new JObject { ["error"] = "Unity refused to add component: " + type.FullName + " (see Console)" };
            }
            SceneObjects.MarkDirty(go);
            return new JObject
            {
                ["added"] = true,
                ["type"] = type.FullName,
                ["instanceId"] = comp.GetInstanceID(),
                ["target"] = SceneObjects.PathOf(go)
            };
        }
    }

    /// Удалить компонент с GameObject (AC3.10). Required-конфликты (RequireComponent) — внятная ошибка
    /// до удаления, не молчаливый отказ Unity.
    public sealed class RemoveComponentTool : ITool
    {
        public string Name => "remove_component";

        public string Description =>
            "Remove a component from a scene GameObject by type name. If several components share the type, " +
            "pass 'index' (0-based, GetComponents order).";

        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["target"] = new JObject { ["type"] = "string", ["description"] = "GameObject path or name." },
                ["type"] = new JObject { ["type"] = "string", ["description"] = "Component type name (short or full)." },
                ["index"] = new JObject { ["type"] = "integer", ["description"] = "Which one when several share the type (0-based)." }
            },
            ["required"] = new JArray { "target", "type" }
        };

        public JObject Invoke(JObject args)
        {
            var go = SceneObjects.Resolve((string)args["target"]);
            if (go == null)
            {
                return new JObject { ["error"] = "target not found: " + (string)args["target"] };
            }
            var type = TypeResolve.Find((string)args["type"], null, typeof(Component), out var err);
            if (type == null)
            {
                return err;
            }
            if (typeof(Transform).IsAssignableFrom(type))
            {
                return new JObject { ["error"] = "Transform cannot be removed" };
            }

            var comps = go.GetComponents(type);
            if (comps.Length == 0)
            {
                return new JObject { ["error"] = "component not found: " + type.FullName };
            }
            int index = args["index"] != null ? (int)args["index"] : -1;
            if (comps.Length > 1 && index < 0)
            {
                return new JObject { ["error"] = comps.Length + " components of type " + type.FullName + "; pass 'index' (0.." + (comps.Length - 1) + ")" };
            }
            if (index >= comps.Length)
            {
                return new JObject { ["error"] = "index " + index + " out of range (0.." + (comps.Length - 1) + ")" };
            }
            var victim = comps[Math.Max(index, 0)];

            var blocker = RequiredBy(go, victim);
            if (blocker != null)
            {
                return new JObject { ["error"] = "cannot remove " + type.FullName + ": required by " + blocker + " (RequireComponent)" };
            }

            UnityEngine.Object.DestroyImmediate(victim);
            if (victim != null)
            {
                // Unity отказал по причине вне RequireComponent-пре-чека (лог в Console)
                return new JObject { ["error"] = "Unity refused to remove component: " + type.FullName + " (see Console)" };
            }
            SceneObjects.MarkDirty(go);
            return new JObject { ["removed"] = true, ["type"] = type.FullName, ["target"] = SceneObjects.PathOf(go) };
        }

        /// FullName компонента, требующего victim через [RequireComponent], либо null. Учитывает, что
        /// требование удовлетворяет любой оставшийся компонент подходящего типа.
        static string RequiredBy(GameObject go, Component victim)
        {
            var victimType = victim.GetType();
            foreach (var other in go.GetComponents<Component>())
            {
                if (other == null || other == victim)
                {
                    continue;
                }
                foreach (RequireComponent rc in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    foreach (var req in new[] { rc.m_Type0, rc.m_Type1, rc.m_Type2 })
                    {
                        if (req == null || !req.IsAssignableFrom(victimType))
                        {
                            continue;
                        }
                        bool satisfiedByOther = go.GetComponents(req).Any(c => c != null && c != victim);
                        if (!satisfiedByOther)
                        {
                            return other.GetType().FullName;
                        }
                    }
                }
            }
            return null;
        }
    }
}
