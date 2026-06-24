using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;

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
}
