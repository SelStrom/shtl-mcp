# TASK: T4 — assets (clear_logs, refresh_assets, AssetDatabase CRUD)

**Status:** done (7 тулов; 63/63 + e2e зелёные)
**Привязка:** F3/AC3.1 (тулсет), F4/AC4.2 (refresh_assets ⏳). На фундаменте T1 (JobStore) и T3 (ReloadJobs).

## Тулы

| Тул | Что | Поток | Заметки |
|---|---|---|---|
| `clear_logs` | Очистить буфер консольных логов (`LogBuffer.Clear`) | фон | возвращает `{cleared:N}` |
| `refresh_assets` ⏳ | `AssetDatabase.Refresh` как job | main | делит reload-job-канал с recompile (та же финализация: reload→done, иначе grace→no changes) |
| `find_assets` | `AssetDatabase.FindAssets(filter[,folder])` | main | до 200 `{guid,path}` + `truncated` |
| `read_asset` | текст (тексто-файлы <256KB) либо метаданные | main | `Path.GetFullPath` + `File.Exists`-guard; для бинарных/больших — `note` без content |
| `move_asset` | `AssetDatabase.MoveAsset(from,to)` | main | ошибка из MoveAsset → `{error}` |
| `delete_asset` | `AssetDatabase.DeleteAsset(path)` | main | `{deleted:bool}` |
| `create_folder` | `AssetDatabase.CreateFolder(parent,name)` | main | `{guid,path}` или `{error}` |

Размещение: `Editor/Tools/{ClearLogsTool,RefreshAssetsTool,AssetTools}.cs` (5 CRUD-классов в `AssetTools.cs`
как когезивная группа). Добавлен `LogBuffer.Clear()` (Dispatcher).

## Верификация

- Unit/integration (`AssetToolsTests`, 4): clear_logs опустошает буфер; CRUD round-trip на РЕАЛЬНОМ
  AssetDatabase (create_folder→write txt→find_assets→read content→move→delete, temp-папка чистится);
  read_asset на отсутствующем пути → error; create_folder с плохим parent → error. ✅
- Полный сьют: **63/63 passed** (59 + 4).
- E2E (MCP-протокол): `find_assets t:Script` нашёл наш тул; `read_asset` package-ассета → content
  (MonoScript, 6179B); `clear_logs` → cleared; `refresh_assets` job → `done {reloaded:false, no changes}`
  после grace (~6с) — подтверждает no-reload-after-grace путь e2e. ✅

## Заметки / долги

- `read_asset` на не-ассетах (напр. `Packages/manifest.json`) → «no asset» (вне AssetDatabase) — корректно.
- INV-3 identity-инъекция в ответы — общий долг M2, не сделано.
