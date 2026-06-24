using System;
using System.CodeDom.Compiler;
using System.Linq;
using Microsoft.CSharp;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Shtl.Mcp.Tools
{
    /// Выполнить пункт меню редактора (`EditorApplication.ExecuteMenuItem`).
    public sealed class ExecuteMenuItemTool : ITool
    {
        public string Name => "execute_menu_item";
        public string Description => "Execute an editor menu item by path, e.g. 'GameObject/Create Empty'.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["menuItem"] = new JObject { ["type"] = "string", ["description"] = "Menu path, e.g. 'Assets/Refresh'." }
            },
            ["required"] = new JArray { "menuItem" }
        };

        public JObject Invoke(JObject args)
        {
            var menu = (string)args["menuItem"];
            if (string.IsNullOrEmpty(menu))
            {
                return new JObject { ["error"] = "menuItem is required" };
            }
            bool ok = EditorApplication.ExecuteMenuItem(menu);
            return new JObject { ["executed"] = ok, ["menuItem"] = menu };
        }
    }

    /// FOOTGUN: компиляция+исполнение произвольного Editor-C#. По умолчанию выключено (ShtlMcpConfig.
    /// AllowRunCsharp, меняет человек). Код — тело статического метода `object Run()`; верни значение через
    /// `return`. Компиляция в память (CodeDom/Mono), синхронно, без reload.
    public sealed class RunCsharpTool : ITool
    {
        readonly Func<bool> _allowed;

        public RunCsharpTool(Func<bool> allowed)
        {
            _allowed = allowed;
        }

        public string Name => "run_csharp";
        public string Description =>
            "FOOTGUN (disabled by default): compile and run arbitrary Editor C#. The 'code' is the body of " +
            "'static object Run()'; return a value with 'return'. Enable via Shtl.Mcp.AllowRunCsharp in the Editor.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["code"] = new JObject { ["type"] = "string", ["description"] = "C# statements; body of 'static object Run()'." }
            },
            ["required"] = new JArray { "code" }
        };

        public JObject Invoke(JObject args)
        {
            if (!_allowed())
            {
                return new JObject { ["error"] = "run_csharp is disabled (footgun). Enable Shtl.Mcp.AllowRunCsharp in the Editor." };
            }
            var code = (string)args["code"];
            if (string.IsNullOrEmpty(code))
            {
                return new JObject { ["error"] = "code is required" };
            }

            var src =
                "using System; using System.Linq; using System.Collections; using System.Collections.Generic; " +
                "using UnityEngine; using UnityEditor; " +
                "public static class __ShtlMcpRun { public static object Run() {\n" + code + "\n; return null; } }";

            try
            {
                using (var provider = new CSharpCodeProvider())
                {
                    var p = new CompilerParameters { GenerateInMemory = true, GenerateExecutable = false };
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (asm.IsDynamic)
                        {
                            continue;
                        }
                        string loc;
                        try { loc = asm.Location; } catch { loc = null; }
                        if (!string.IsNullOrEmpty(loc))
                        {
                            p.ReferencedAssemblies.Add(loc);
                        }
                    }

                    var results = provider.CompileAssemblyFromSource(p, src);
                    if (results.Errors.HasErrors)
                    {
                        var errs = string.Join("\n", results.Errors.Cast<CompilerError>()
                            .Where(e => !e.IsWarning).Select(e => e.ErrorText));
                        return new JObject { ["error"] = "compile errors:\n" + errs };
                    }

                    var type = results.CompiledAssembly.GetType("__ShtlMcpRun");
                    var ret = type.GetMethod("Run").Invoke(null, null);
                    return new JObject { ["ok"] = true, ["result"] = ret != null ? ret.ToString() : null };
                }
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = "run_csharp failed: " + e.GetType().Name + ": " + e.Message };
            }
        }
    }
}
