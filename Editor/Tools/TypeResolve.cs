using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Shtl.Mcp.Tools
{
    /// Резолв C#-типа по имени для add/remove_component и call_method/find_method: точное полное имя →
    /// уникальное короткое имя → ошибка со списком-подсказкой (AC3.10/3.12). Скан всех сборок домена —
    /// только когда полное имя не сработало (перечисление типов дорогое, per-call editor-tooling приемлемо).
    internal static class TypeResolve
    {
        const int SuggestCap = 10;

        /// null при неудаче; error — структурированная ошибка (кандидаты/подсказки внутри).
        public static Type Find(string name, string assembly, Type baseType, out JObject error)
        {
            error = null;
            if (string.IsNullOrEmpty(name))
            {
                error = new JObject { ["error"] = "type is required" };
                return null;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .Where(a => string.IsNullOrEmpty(assembly) || a.GetName().Name == assembly)
                .ToList();
            if (assemblies.Count == 0)
            {
                error = new JObject { ["error"] = "assembly not found: " + assembly };
                return null;
            }

            foreach (var asm in assemblies)
            {
                var t = asm.GetType(name);
                if (t != null && Fits(t, baseType))
                {
                    return t;
                }
            }

            var exact = new List<Type>();
            var partial = new List<Type>();
            foreach (var t in AllTypes(assemblies))
            {
                if (!Fits(t, baseType))
                {
                    continue;
                }
                if (t.Name == name || t.FullName == name)
                {
                    exact.Add(t);
                }
                else if (partial.Count < SuggestCap && t.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    partial.Add(t);
                }
            }

            if (exact.Count == 1)
            {
                return exact[0];
            }
            if (exact.Count > 1)
            {
                error = new JObject
                {
                    ["error"] = "type name '" + name + "' is ambiguous; use a full name",
                    ["candidates"] = new JArray(exact.Take(SuggestCap).Select(t => t.FullName))
                };
                return null;
            }
            error = new JObject
            {
                ["error"] = "type not found: " + name + (baseType != null ? " (" + baseType.Name + "-derived)" : ""),
                ["suggestions"] = new JArray(partial.Select(t => t.FullName))
            };
            return null;
        }

        static bool Fits(Type t, Type baseType) => baseType == null || baseType.IsAssignableFrom(t);

        static IEnumerable<Type> AllTypes(List<System.Reflection.Assembly> assemblies)
        {
            foreach (var asm in assemblies)
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                catch { continue; }
                foreach (var t in types)
                {
                    yield return t;
                }
            }
        }
    }
}
