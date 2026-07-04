# Journal — m5-command-set-v2 (append-only)

## [2026-07-04] Forward-поток: raw → wiki → код → тесты

**Вход.** `.planning/M5-murzak-parity-prompt.md` (аудит замены murzak в PerfectWar). Открытые
вопросы §7 закрыты человеком ДО code-diff: обычные тулы (не footgun), имя `write_asset`,
объём P0+P1, `AllowRunCsharp` в PerfectWar не включать. Гейт ревью raw/wiki человек делегировал
(«обновление raw/wiki твоя задача»).

**Raw-diff.** F3: секция «Command-set v2 (M5)» AC3.9–3.14; из Out of scope убран reflection-API
(M5 и есть v2), добавлены `type-get-json-schema`/built-in-ресурсы (§6 промпта — остаются на
escape hatches). Консистентность: INV-1..5 не затронуты; write_asset компилируемых → reload-job
(INV-1).

**Wiki-diff.** `command-set.md`: 9 новых тулов в Core-таблицы (44 всего), §v2 сокращён; попутно
закрыт drift — `ping`/`get_config` отсутствовали в таблице.

**Код.** Новые файлы: `WriteAssetTool`, `ComponentTools` (add/remove_component), `TypeResolve`
(общий резолв типов: полное имя → уникальное короткое → подсказки), `ReflectionTools`
(call_method/find_method), `MultiSceneTools` (list/create/unload/set_active_scene). Расширены:
`SceneEditTools` (`ObjectRefs` scene-GO/asset-path/instanceId; `get_object` вложенность+бюджет;
`modify_object` bulk+nested транзакционно, персист ассетов `SaveAssetIfDirty`), `ScreenshotTool`
(`camera`), регистрация в `EnsureStarted`.

**Тесты.** 46 новых EditMode (write_asset sync/guards/канал; reload-spanning e2e write_asset
через реальный HTTP `tools/call` + `WaitForDomainReload` — паттерн ReloadSurvivalTests;
компоненты; bulk/nested/asset-target; reflection; multi-scene; screenshot camera). `McpProbe`
обобщён до `CallToolAsync`.

## [2026-07-04] Верификация и находки (прогоны в TestProject~ через собственный MCP)

Прогон 1 — **181/184**, три падения, все — неверные предположения тестов, не баги тулов:
1. `UnityEngine.Collider` в C#-API **не abstract** (Unity отказывает нативно) → abstract-guard
   тестируется собственным `ShtlM5AbstractComp`.
2. У `Rigidbody` **нет managed** `[DisallowMultipleComponent]` — единственность enforce'ится
   нативно → пре-чек тестируется фикстурой `ShtlM5SingleComp`. Находка №2: MonoBehaviour из
   **Editor-сборки нельзя добавить компонентом** → фикстура живёт в `TestProject~/Assets`
   (runtime), тест резолвит тип по имени (без compile-time ссылки).
3. Unity запрещает **вторую untitled-сцену** → `NewScene(Additive)` в тестовом окружении бросал
   (активная сцена test-runner'а untitled). Тест сохраняет активную сцену перед сценарием.

Прогон 2 — **182/184**. Живой пробой через новые тулы (curl → `create_scene`/`set_active_scene`)
выяснено: `NewScene(Additive)` **сам активирует** новую сцену, а `SceneManager.SetActiveScene`
возвращает **false для уже-активной** → `set_active_scene` сделан идемпотентным (уже активная =
успех), семантика активации задокументирована в описании `create_scene`.

Прогон 3 — **184/184 зелёный** (см. финальную запись).

**Догфудинг-заметка.** Вся верификация шла через сам shtl-mcp (`refresh_assets` →
reload-spanning `run_tests` → `get_job`), включая паузы listener'а на domain reload —
recovery-петля работает как задумано.

## [2026-07-04] Финал

184/184 EditMode зелёные. README (Status/Tools), CHANGELOG 0.5.0, `package.json` 0.5.0,
`wiki/index.md`, `wiki/log.md` обновлены. Атомарный коммит raw+wiki+code.
