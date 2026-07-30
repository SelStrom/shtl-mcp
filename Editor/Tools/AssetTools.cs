using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Shtl.Mcp.Tools
{
    /// Поиск ассетов по фильтру Test Runner-стиля (`t:Type name`, опц. в папке). Read-only.
    public sealed class FindAssetsTool : ITool
    {
        const int Cap = 200;

        public string Name => "find_assets";
        public string Description =>
            "Find assets by AssetDatabase filter (e.g. 't:Texture2D button', 't:Script', 'name'). " +
            "Optional folder restricts the search. Returns up to 200 {guid, path}.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["filter"] = new JObject { ["type"] = "string", ["description"] = "AssetDatabase filter string." },
                ["folder"] = new JObject { ["type"] = "string", ["description"] = "Optional folder to search in, e.g. 'Assets/Prefabs'." }
            },
            ["required"] = new JArray { "filter" }
        };

        public JObject Invoke(JObject args)
        {
            var filter = (string)args["filter"] ?? "";
            var folder = (string)args["folder"];
            var guids = string.IsNullOrEmpty(folder)
                ? AssetDatabase.FindAssets(filter)
                : AssetDatabase.FindAssets(filter, new[] { folder });

            var assets = new JArray();
            foreach (var g in guids.Take(Cap))
            {
                assets.Add(new JObject { ["guid"] = g, ["path"] = AssetDatabase.GUIDToAssetPath(g) });
            }
            return new JObject
            {
                ["count"] = guids.Length,
                ["truncated"] = guids.Length > Cap,
                ["assets"] = assets
            };
        }
    }

    /// Прочитать ассет: текстовое содержимое (для текстовых файлов в пределах лимита) либо метаданные.
    public sealed class ReadAssetTool : ITool
    {
        const long MaxText = 256 * 1024;
        static readonly string[] TextExt =
        {
            ".cs", ".txt", ".json", ".md", ".shader", ".cginc", ".hlsl", ".asmdef", ".asmref",
            ".xml", ".yaml", ".yml", ".uxml", ".uss", ".csv", ".text"
        };

        public string Name => "read_asset";
        public string Description =>
            "Read an asset by project path (e.g. 'Assets/foo.cs'). Returns text content for text files " +
            "(under 256KB), otherwise type/size metadata.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["path"] = new JObject { ["type"] = "string", ["description"] = "Project-relative asset path, e.g. 'Assets/foo.cs'." }
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

            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                return new JObject { ["error"] = "no asset at path: " + path };
            }

            var o = new JObject
            {
                ["path"] = path,
                ["guid"] = guid,
                ["type"] = type != null ? type.Name : "unknown"
            };

            var full = Path.GetFullPath(path);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            long size = File.Exists(full) ? new FileInfo(full).Length : -1;
            o["size"] = size;

            if (TextExt.Contains(ext) && size >= 0 && size <= MaxText)
            {
                try
                {
                    o["content"] = File.ReadAllText(full);
                }
                catch (System.Exception e)
                {
                    o["note"] = "read failed: " + e.Message;
                }
            }
            else
            {
                o["note"] = size < 0
                    ? "file not on disk (virtual package path?) — content omitted"
                    : "binary or too large — content omitted";
            }
            return o;
        }
    }

    /// Переместить/переименовать ассет (`AssetDatabase.MoveAsset`). Editor-only.
    public sealed class MoveAssetTool : ITool
    {
        public string Name => "move_asset";
        public string Description => "Move or rename an asset (AssetDatabase.MoveAsset). Both paths project-relative.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["from"] = new JObject { ["type"] = "string", ["description"] = "Source asset path." },
                ["to"] = new JObject { ["type"] = "string", ["description"] = "Destination asset path." }
            },
            ["required"] = new JArray { "from", "to" }
        };

        public JObject Invoke(JObject args)
        {
            var from = (string)args["from"];
            var to = (string)args["to"];
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            {
                return new JObject { ["error"] = "from and to are required" };
            }
            var err = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(err))
            {
                return new JObject { ["error"] = err };
            }
            return new JObject { ["moved"] = true, ["from"] = from, ["to"] = to };
        }
    }

    /// Удалить ассет (`AssetDatabase.DeleteAsset`). Editor-only.
    public sealed class DeleteAssetTool : ITool
    {
        public string Name => "delete_asset";
        public string Description => "Delete an asset by project path (AssetDatabase.DeleteAsset).";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["path"] = new JObject { ["type"] = "string", ["description"] = "Project-relative asset path to delete." }
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
            bool ok = AssetDatabase.DeleteAsset(path);
            return new JObject { ["deleted"] = ok, ["path"] = path };
        }
    }

    /// Создать папку (`AssetDatabase.CreateFolder`). Editor-only.
    public sealed class CreateFolderTool : ITool
    {
        public string Name => "create_folder";
        public string Description => "Create a folder under a parent (AssetDatabase.CreateFolder). Returns the new path.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["parent"] = new JObject { ["type"] = "string", ["description"] = "Existing parent folder, e.g. 'Assets'." },
                ["name"] = new JObject { ["type"] = "string", ["description"] = "New folder name." }
            },
            ["required"] = new JArray { "parent", "name" }
        };

        public JObject Invoke(JObject args)
        {
            var parent = (string)args["parent"];
            var name = (string)args["name"];
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                return new JObject { ["error"] = "parent and name are required" };
            }
            var guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid))
            {
                return new JObject { ["error"] = "could not create folder (parent missing or name invalid)" };
            }
            return new JObject { ["guid"] = guid, ["path"] = AssetDatabase.GUIDToAssetPath(guid) };
        }
    }

    /// Создать бинарный ассет (`AssetDatabase.CreateAsset`): материал, ScriptableObject и прочие
    /// UnityEngine.Object с конструктором без параметров. Парный `write_asset` покрывает только
    /// текстовые файлы, а такие ассеты руками не собрать — их поля правит `modify_object`.
    public sealed class CreateAssetTool : ITool
    {
        public string Name => "create_asset";
        public string Description =>
            "Create a binary asset (AssetDatabase.CreateAsset): 'type' is a UnityEngine.Object type name " +
            "(Material, a ScriptableObject subclass, AnimationClip, …). Material requires 'shader'. " +
            "Path must be project-relative with the right extension ('.mat', '.asset', …). " +
            "Set fields afterwards with modify_object. Text assets go through write_asset instead.";
        public bool NeedsMainThread => true;

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["path"] = new JObject { ["type"] = "string", ["description"] = "Project-relative path with extension, e.g. 'Assets/Materials/Loot.mat'." },
                ["type"] = new JObject { ["type"] = "string", ["description"] = "UnityEngine.Object type name, e.g. 'Material' or a ScriptableObject subclass." },
                ["shader"] = new JObject { ["type"] = "string", ["description"] = "Shader name for Material, e.g. 'Universal Render Pipeline/Unlit'." },
                ["overwrite"] = new JObject { ["type"] = "boolean", ["description"] = "Replace an existing asset at 'path' (default false)." }
            },
            ["required"] = new JArray { "path", "type" }
        };

        public JObject Invoke(JObject args)
        {
            var path = (string)args["path"];
            if (string.IsNullOrEmpty(path))
            {
                return new JObject { ["error"] = "path is required" };
            }
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return new JObject { ["error"] = "path must be under Assets/: " + path };
            }
            if (string.IsNullOrEmpty(Path.GetExtension(path)))
            {
                return new JObject { ["error"] = "path needs an extension ('.mat', '.asset', …): " + path };
            }

            bool overwrite = args["overwrite"] != null && (bool)args["overwrite"];
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                if (!overwrite)
                {
                    return new JObject { ["error"] = "asset already exists (pass overwrite=true): " + path };
                }
                AssetDatabase.DeleteAsset(path);
            }

            var type = TypeResolve.Find((string)args["type"], null, typeof(UnityEngine.Object), out var typeError);
            if (type == null)
            {
                return typeError;
            }

            var asset = Instantiate(type, (string)args["shader"], out var createError);
            if (asset == null)
            {
                return new JObject { ["error"] = createError };
            }

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return new JObject
            {
                ["created"] = true,
                ["path"] = path,
                ["type"] = type.FullName,
                ["guid"] = AssetDatabase.AssetPathToGUID(path)
            };
        }

        static UnityEngine.Object Instantiate(Type type, string shaderName, out string error)
        {
            error = null;
            if (type == typeof(Material))
            {
                if (string.IsNullOrEmpty(shaderName))
                {
                    error = "'shader' is required for Material";
                    return null;
                }
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    error = "shader not found: " + shaderName;
                    return null;
                }
                return new Material(shader);
            }

            if (typeof(ScriptableObject).IsAssignableFrom(type))
            {
                return ScriptableObject.CreateInstance(type);
            }

            // Остальные UnityEngine.Object — только те, что конструируются без аргументов
            // (AnimationClip, PhysicMaterial, …). Прочее (Texture2D, RenderTexture) требует
            // размеров и в задачу этого тула не входит.
            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                error = "type has no parameterless constructor: " + type.FullName;
                return null;
            }
            try
            {
                return (UnityEngine.Object)Activator.CreateInstance(type);
            }
            catch (Exception e)
            {
                error = "could not create " + type.FullName + ": " + e.Message;
                return null;
            }
        }
    }
}
