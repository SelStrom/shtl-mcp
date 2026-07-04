using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Shtl.Mcp.Tools
{
    /// Общее для call_method/find_method (AC3.12): поиск методов, сигнатуры, сериализация результата.
    internal static class Methods
    {
        public const BindingFlags All =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance |
            BindingFlags.FlattenHierarchy;

        public static string Signature(MethodInfo m)
        {
            var ps = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
            return (m.IsStatic ? "static " : "") + m.ReturnType.Name + " " + m.Name + "(" + ps + ")";
        }

        public static JObject Describe(MethodInfo m) => new JObject
        {
            ["name"] = m.Name,
            ["signature"] = Signature(m),
            ["static"] = m.IsStatic,
            ["public"] = m.IsPublic,
            ["returnType"] = m.ReturnType.FullName,
            ["declaringType"] = m.DeclaringType != null ? m.DeclaringType.FullName : null,
            ["parameterTypes"] = new JArray(m.GetParameters().Select(p => p.ParameterType.FullName))
        };

        public static JToken SerializeResult(object result)
        {
            if (result == null)
            {
                return JValue.CreateNull();
            }
            if (result is UnityEngine.Object uo)
            {
                // UnityEngine.Object не JSON-дружелюбен (циклы, нативные хэндлы) — компактный ref
                return new JObject { ["name"] = uo.name, ["type"] = uo.GetType().Name, ["instanceId"] = uo.GetInstanceID() };
            }
            try { return JToken.FromObject(result); }
            catch { return result.ToString(); }
        }
    }

    /// Найти сигнатуры методов типа — выбор перегрузки перед call_method (AC3.12).
    public sealed class FindMethodTool : ITool
    {
        const int Cap = 100;

        public string Name => "find_method";

        public string Description =>
            "List method signatures of a C# type (public and private, static and instance). Optional " +
            "'nameContains' filters by substring. Use before call_method to pick an overload.";

        public bool NeedsMainThread => false; // чистая рефлексия, Unity API не трогает

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["type"] = new JObject { ["type"] = "string", ["description"] = "Type name (short or full), e.g. 'UnityEditor.EditorApplication'." },
                ["nameContains"] = new JObject { ["type"] = "string", ["description"] = "Case-insensitive substring filter on method names." },
                ["assembly"] = new JObject { ["type"] = "string", ["description"] = "Optional assembly name to narrow the type search." }
            },
            ["required"] = new JArray { "type" }
        };

        public JObject Invoke(JObject args)
        {
            var type = TypeResolve.Find((string)args["type"], (string)args["assembly"], null, out var err);
            if (type == null)
            {
                return err;
            }
            var filter = (string)args["nameContains"];
            var methods = type.GetMethods(Methods.All)
                .Where(m => string.IsNullOrEmpty(filter) || m.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            return new JObject
            {
                ["type"] = type.FullName,
                ["count"] = methods.Count,
                ["truncated"] = methods.Count > Cap,
                ["methods"] = new JArray(methods.Take(Cap).Select(Methods.Describe))
            };
        }
    }

    /// Вызвать существующий C#-метод (static/instance, вкл. private) по типу и сигнатуре (AC3.12).
    /// Обычный тул, не footgun (решение человека): вызов существующего метода — не компиляция
    /// произвольного кода (run_csharp).
    public sealed class CallMethodTool : ITool
    {
        public string Name => "call_method";

        public string Description =>
            "Call an existing C# method (static or instance, including private) by type and name. " +
            "'parameterTypes' disambiguates overloads (see find_method). 'args' are JSON values deserialized " +
            "to parameter types; for UnityEngine.Object parameters pass a scene path, asset path or instanceId. " +
            "'target' (instance methods): scene GameObject path/name, asset path or instanceId; for Component " +
            "methods a GameObject target resolves to its component.";

        public bool NeedsMainThread => true; // вызываемый метод обычно трогает Unity API

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["type"] = new JObject { ["type"] = "string", ["description"] = "Type name (short or full)." },
                ["method"] = new JObject { ["type"] = "string", ["description"] = "Method name." },
                ["parameterTypes"] = new JObject { ["type"] = "array", ["description"] = "Parameter type names (short or full) to pick an overload." },
                ["args"] = new JObject { ["type"] = "array", ["description"] = "Arguments as JSON values (positional)." },
                ["target"] = new JObject
                {
                    ["type"] = new JArray { "string", "integer" },
                    ["description"] = "Instance methods: GameObject path/name, asset path, or instanceId. Omit for static."
                },
                ["assembly"] = new JObject { ["type"] = "string", ["description"] = "Optional assembly name to narrow the type search." }
            },
            ["required"] = new JArray { "type", "method" }
        };

        public JObject Invoke(JObject args)
        {
            var type = TypeResolve.Find((string)args["type"], (string)args["assembly"], null, out var typeErr);
            if (type == null)
            {
                return typeErr;
            }
            var methodName = (string)args["method"];
            if (string.IsNullOrEmpty(methodName))
            {
                return new JObject { ["error"] = "method is required" };
            }
            var callArgs = args["args"] as JArray ?? new JArray();

            var method = PickOverload(type, methodName, args["parameterTypes"] as JArray, callArgs.Count, out var pickErr);
            if (method == null)
            {
                return pickErr;
            }

            object instance = null;
            if (!method.IsStatic)
            {
                instance = ResolveInstance(type, args["target"], out var instErr);
                if (instance == null)
                {
                    return instErr;
                }
            }

            var ps = method.GetParameters();
            if (callArgs.Count != ps.Length)
            {
                return new JObject
                {
                    ["error"] = "args count mismatch: method takes " + ps.Length + ", got " + callArgs.Count,
                    ["signature"] = Methods.Signature(method)
                };
            }
            var converted = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                try
                {
                    converted[i] = ConvertArg(callArgs[i], ps[i].ParameterType);
                }
                catch (Exception e)
                {
                    return new JObject { ["error"] = "args[" + i + "] does not convert to " + ps[i].ParameterType.Name + ": " + e.Message };
                }
            }

            try
            {
                var result = method.Invoke(instance, converted);
                return new JObject
                {
                    ["ok"] = true,
                    ["method"] = Methods.Signature(method),
                    ["result"] = Methods.SerializeResult(result)
                };
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                return new JObject { ["error"] = "method threw " + inner.GetType().Name + ": " + inner.Message };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = "call failed: " + e.Message };
            }
        }

        static MethodInfo PickOverload(Type type, string name, JArray parameterTypes, int argCount, out JObject error)
        {
            error = null;
            var named = type.GetMethods(Methods.All).Where(m => m.Name == name).ToList();
            if (named.Count == 0)
            {
                error = new JObject { ["error"] = "method not found: " + type.FullName + "." + name + " (try find_method)" };
                return null;
            }
            var fit = parameterTypes != null
                ? named.Where(m => MatchesTypes(m, parameterTypes)).ToList()
                : named.Where(m => m.GetParameters().Length == argCount).ToList();
            if (fit.Count == 1)
            {
                return fit[0];
            }
            error = new JObject
            {
                ["error"] = fit.Count == 0
                    ? "no overload of " + name + " matches " + (parameterTypes != null ? "parameterTypes" : argCount + " arg(s)")
                    : "ambiguous overloads of " + name + "; pass parameterTypes",
                ["overloads"] = new JArray(named.Select(Methods.Signature))
            };
            return null;
        }

        static bool MatchesTypes(MethodInfo m, JArray wanted)
        {
            var ps = m.GetParameters();
            if (ps.Length != wanted.Count)
            {
                return false;
            }
            for (int i = 0; i < ps.Length; i++)
            {
                var w = (string)wanted[i];
                if (ps[i].ParameterType.FullName != w && ps[i].ParameterType.Name != w)
                {
                    return false;
                }
            }
            return true;
        }

        static object ResolveInstance(Type type, JToken target, out JObject error)
        {
            error = null;
            if (target == null || target.Type == JTokenType.Null)
            {
                error = new JObject { ["error"] = "method is not static — 'target' is required" };
                return null;
            }
            var obj = ObjectRefs.Resolve(target, out var refErr);
            if (obj == null)
            {
                error = new JObject { ["error"] = refErr };
                return null;
            }
            if (obj is GameObject go && typeof(Component).IsAssignableFrom(type))
            {
                var comp = go.GetComponent(type);
                if (comp == null)
                {
                    error = new JObject { ["error"] = "no " + type.Name + " component on " + SceneObjects.PathOf(go) };
                    return null;
                }
                return comp;
            }
            if (!type.IsInstanceOfType(obj))
            {
                error = new JObject { ["error"] = "target is " + obj.GetType().FullName + ", not " + type.FullName };
                return null;
            }
            return obj;
        }

        static object ConvertArg(JToken token, Type paramType)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }
            // UnityEngine.Object-параметр: строка/число — это ref (scene path / asset path / instanceId)
            if (typeof(UnityEngine.Object).IsAssignableFrom(paramType)
                && (token.Type == JTokenType.String || token.Type == JTokenType.Integer))
            {
                var obj = ObjectRefs.Resolve(token, out var err);
                if (obj == null)
                {
                    throw new ArgumentException(err);
                }
                if (obj is GameObject go && typeof(Component).IsAssignableFrom(paramType))
                {
                    var comp = go.GetComponent(paramType);
                    if (comp == null)
                    {
                        throw new ArgumentException("no " + paramType.Name + " component on " + SceneObjects.PathOf(go));
                    }
                    return comp;
                }
                if (!paramType.IsInstanceOfType(obj))
                {
                    throw new ArgumentException("resolved object is " + obj.GetType().Name + ", not " + paramType.Name);
                }
                return obj;
            }
            return token.ToObject(paramType);
        }
    }
}
