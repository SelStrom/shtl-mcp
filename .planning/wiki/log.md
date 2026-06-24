# Журнал операций (append-only)

Хронологический лог: forward-изменения, query, lint, drift-расследования, эпохи.
Префикс парсибелен: `grep "^## \[" log.md | tail -10`.

## [2026-06-13] epoch | 001 Bootstrap
Старт проекта. Зафиксированы намерение (raw/) и архитектура (wiki/systems/).

## [2026-06-13] forward | bootstrap spec | brainstorming
Создано из брейншторма: raw/domain/overview, raw/features/F1-F6, raw/epochs;
wiki/systems/{architecture, multi-instance, lifecycle-and-reload, command-set,
dashboard}; CLAUDE.md (схема агента). Код ещё не написан (greenfield) — следующий
шаг: writing-plans → план реализации.
Решения брейншторма: in-Unity Streamable HTTP MCP (без внешнего процесса);
мульти-инстанс через реестр + детерм. порт + префикс инструментов; watchdog +
reconnect; тонкое ядро + run_csharp/execute_menu_item; UI Toolkit дашборд;
Claude Code (итер.1), Unity 2022+; регистрация user-level.

## [2026-06-13] forward | F2 control-flag recovery | discuss
Добавлен LLM-инициируемый форс-рестарт: control-flag канал
`~/.unity-mcp/<serverName>.cmd`, исполняемый watchdog'ом независимо от listener'а
(работает, когда сервер недоступен; без демона; Unity не запускаем). Правки:
raw F2 (AC2.6/AC2.7), raw/domain (Control channel + INV-5), raw F6 (AC6.4),
wiki lifecycle (§4 Control-channel), wiki multi-instance (флаги в ~/.unity-mcp/),
CLAUDE.md (§Recovery playbook).

## [2026-06-13] forward | F7 recovery discoverability | discuss
Закрыт вопрос «как использующая модель узнает о восстановлении» (она не видит
dev-репо; при мёртвом сервере MCP-канал недоступен). Defense-in-depth по 3
каналам + opt-in host-крошка. Решение cold-start: вариант A (строго
самодостаточно по умолчанию) + предложение добавить указатель в host-CLAUDE.md
или recovery-скилл только с явного согласия. Правки: новый raw F7; raw/domain
INV-2 (исключение opt-in); raw F6 (AC6.1); raw F2 (AC2.7 cross-ref); новая wiki
systems/recovery-discoverability; wiki index; wiki command-set (recoveryHint +
пре-брифинг в контракте); CLAUDE.md §Recovery (две аудитории).

## [2026-06-13] plan | M1 walking skeleton | wiki/tasks/m1-walking-skeleton
Написан bite-sized план реализации (13 задач, TDD для чистой логики, integration/
manual для Unity-склейки). Декомпозиция проекта на вехи: M1 (этот срез), M2 (полный
набор инструментов + async-job + control-flag), M3 (F7 discoverability + доводка UI).
Локация планов — wiki/tasks/<slug>/PLAN.md (override дефолта skill'а под intent-driven
фреймворк). Отклонение: M1 — один Editor-asmdef с папками (6-asmdef split → M2).

## [2026-06-13] forward | M1 walking skeleton | wiki/tasks/m1-walking-skeleton
Реализован вертикальный срез (subagent-driven, ветка m1-walking-skeleton, коммиты
a7df6dd→799bf46). 34/34 EditMode-теста + headless HTTP smoke (initialize/status/
tools.list/get_logs/registry) зелёные. Code-ref: wiki/code/m1-server.md (pin 799bf46).
Детали и отклонения — wiki/tasks/m1-walking-skeleton/journal.md. Остаётся
интерактивная приёмка: reload-survival в живом редакторе + claude mcp add в Claude Code.

## [2026-06-13] forward | package as installable UPM | finish
Оформлено как UPM-пакет (раскладка: пакет в Packages/com.shtl.mcp, install via git
?path=/Packages/com.shtl.mcp — raw/wiki не протекают потребителю). Добавлены:
package.json (author/license MIT/keywords/doc+changelog+license URLs), LICENSE.md,
CHANGELOG.md (0.1.0/M1), README пакета (install/quick-start/tools), обновлён корневой
README (структура репо + install). Unity-refresh подтвердил резолв пакета без ошибок.

## [2026-06-13] forward | canonical UPM layout: upm-branch via subtree | finish
Сверка с каноном Unity (Package layout: package.json в корне пакета; Git install:
корень по умолчанию, ?path — fallback). Принят канонический приём для dev-моно-репо:
ветка `upm` = `git subtree split --prefix=Packages/com.shtl.mcp`. main остаётся
dev-моно-репо (проект+пакет+raw/wiki, тестируется), потребитель ставит чистый
`shtl-mcp.git#upm` без ?path и без протечки доков. Install-инструкции в обоих README
переведены на #upm; в корневой README добавлена секция «Релиз пакета».

## [2026-06-14] forward | reload-survival test | m1-reload-survival-test
Закрыта оставшаяся интерактивная приёмка M1 (выживание при reload) — переведена в автоматический
RED-gate. Добавлены EditMode `[UnityTest]` (`Tests/Editor/{McpProbe,ReloadSurvivalTests}.cs`):
`Listener_Respawns_AfterDomainReload` (RequestScriptReload+WaitForDomainReload, проба `status`
round-trip на том же порту) и `Watchdog_Rebinds_AfterListenerDeath` (StopListenerForReload+WatchdogTick).
Оба зелёные; RED-gate подтверждён (совмещённый слом re-spawn → оба краснеют). Аддитивный тест —
без raw-diff. Исследование UTF 1.1.33 (механизм reload-survival) — в journal таска.

## [2026-06-14] forward | status reload observability | status-reload-observability
Добавлена наблюдаемость re-spawn в `status`: `reloadCount` (durable в SessionState, инкремент в
beforeAssemblyReload) и `listenerUptimeSeconds` (сброс при каждом EnsureStarted). Закрывает дыру,
из-за которой ручная reload-приёмка была неверифицируема (durable uptimeSeconds re-spawn не отличает).
Forward-поток: raw F4/AC4.7 + wiki (command-set, lifecycle-and-reload) + code (IEditorContext,
EditorContext, ShtlMcpServer, ShtlMcpBootstrap, StatusTool) + тесты (StatusToolTests, ReloadSurvivalTests
проверяет рост reloadCount). Изменение контракта status — поведенческое, прошло forward атомарно.

## [2026-06-14] plan | M2 decomposition | wiki/m2-plan.md
Декомпозиция вехи M2 на bite-sized таски T1–T11: фундамент (T1 async-job/JobStore+get_job,
T2 6-asmdef split) → тулы параллельно (T3 play/recompile, T4 assets, T5 prefabs, T6 scene/objects,
T8 escape hatches, T9 screenshot) → механизмы (T7 run_tests, T10 control-flag, T11 config). Критпуть
T1→(T3,T7). Сквозной tool-контракт (JSON-схема, projectName/INV-3, Play/Edit AC4.5, async для reload)
вынесен в acceptance каждого tool-таска. F7/UI-доводка/v2 — вне M2 (→ M3).

## [2026-06-23] forward | focus-independent re-spawn (bugfix INV-5) | reload-respawn-focus-independent
Найден и исправлен баг: re-spawn после domain reload был завязан на delayCall→EditorApplication.update,
который Unity троттлит в фоне (окно не в фокусе) → MCP-инициированный recompile вешал сервер до возврата
фокуса (ломая INV-5 и автономию). Фикс: подписки before/afterAssemblyReload перенесены в [InitializeOnLoad]-ctor
(срабатывают в reload-последовательности, focus-independent) — канонический паттерн Unity; код приведён в
соответствие с wiki §1 (raw/wiki не менялись). RecompileTool: инкрементальный по умолчанию + force:true.
Верифицировано: 3 unfocused MCP-recompile подряд с self-recovery ~10-14с (было 120с+ зависание).
Разблокирует автономную дев-петлю (edit → MCP recompile → self-recovery).

## [2026-06-24] forward | T1 async-job (JobStore + get_job) | m2-async-job
Реализован фундамент async-job: Job (POCO), JobStore (in-memory + персист в SessionState, переживает
domain reload), get_job (опрос, NeedsMainThread=false, unknown-id → структурированная ошибка). Проводка
в ShtlMcpServer. Тесты: JobStoreTests (round-trip через эмулированный reload =AC-1, состояния),
GetJobToolTests — зелёные. Self-verified автономно (MCP recompile + headless curl). Разблокирует T3
(reload-инструменты как job) и T7 (run_tests → self-test). Реализация существующего интента (F3/AC3.5,
F4/AC4.2) — raw/wiki не менялись.

## [2026-06-24] forward | T7 run_tests (механически) + автономная дев-петля | m2-run-tests
Реализован run_tests (вариант A — жёсткая зависимость от com.unity.test-framework, по исследованию
прайор-арта: эталон CoplayDev). job+polling на JobStore (T1), durable-маркер + ReattachIfPending для
reload-spanning, сбор passed/failed/skipped+failures из дерева результата. Headless-верифицирован
автономно: JobStoreTests 5/5, FnvTests 3/3 — done/Passed. Блокер юзабилити в фоне: focus-throttle
тест-раннера (сервер неотзывчив на время unfocused-прогона) → остаток no-throttle (TestRunnerNoThrottle)
+ orphan-таймаут (tasks/m2-run-tests/TASK.md AC-3). ВЕХА: автономная дев-петля собрана (self-recompile
+ self-test headless); ручные прогоны в Test Runner больше не нужны для не-reload тестов.

## [2026-06-24] milestone-checkpoint | M2 foundation
Готово: focus-independent re-spawn (bugfix INV-5), recompile-инструмент, T1 async-job, T7 run_tests
(механически). Self-recompile — чисто; self-test — механически (нужен no-throttle для чистоты в фоне).
Пауза перед no-throttle. Промпт следующей сессии — tasks/m2-run-tests/NEXT-SESSION.md.

## [2026-06-24] forward | T7 run_tests no-throttle + reload-spanning | m2-run-tests
Закрыт блокер юзабилити (focus-throttle) и AC-2/3/4 T7. `TestRunnerNoThrottle` (паттерн CoplayDev):
`EditorPrefs ApplicationIdleTime=0`+`InteractionMode=1` через приватный `UpdateInteractionModeSettings`
(рефлексия), **двухслойный** бэкап SessionState + `Library/`-marker (диск-слой для prefs — расхождение в
плюс: при крахе редактор иначе навсегда в no-throttle). Preemptive Apply в Invoke + restore в RunFinished
(marker-guard идемпотентен), `RecoverOnLoad` при reload. Orphan-таймаут (`SweepOrphan` с тика watchdog,
10мин). Guard дублей `ICallbacks` (single `_api` + DestroyImmediate). На первом полном сьюте всплыла
потеря live-job через reload (тест-гигиена: `JobStoreTests`/`GetJobToolTests` стирали live-ключ
`Shtl.Mcp.Jobs`, `ReloadSurvivalTests` форсит reload) → фикс: `JobStore` с инъектируемым `sessionKey`,
тесты на изолированных ключах. Новый код: `Editor/Tools/TestRunnerNoThrottle.cs`, `Editor/AssemblyInfo.cs`,
тесты `TestRunnerNoThrottleTests`/`RunTestsOrphanTests`. Верификация headless (Unity не в фокусе): полный
сьют **52/52 passed**, reload-spanning (17→18) — job доставлен, status отзывчив 0.05–0.18с (только ≈2с
DOWN = окно reload), прогон 7.5с (было >180с). Реализация интента F3/AC3.1+3.5, F4/AC4.2/4.5 — raw не менялся.

## [2026-06-24] forward | T2 6-asmdef split | m2-asmdef-split
Единый `Shtl.Mcp.Editor` разбит на 6 Editor-сборок (`Shtl.Mcp.{Transport,Dispatcher,Registry,Tools,
Lifecycle,UI}`) под папками `Editor/<сборка>/`; namespace'ы не менялись (папка ≠ namespace), usings
потребителей не трогались. Граф зависимостей (сабагент) выявил один цикл Lifecycle↔Tools — разорван:
(1) `TestRunCallbacks` получает `JobStore` через DI вместо `ShtlMcpServer.Instance` (убрано ребро
Tools→Lifecycle; мёртвый `ShtlMcpServer.Jobs` удалён); (2) `Logging` размещён в Dispatcher, не Lifecycle.
Тестовая asmdef переведена на 5 ссылок; `InternalsVisibleTo` (AssemblyInfo) → Tools. Ловушка: Write
.asmdef мимо AssetDatabase — нужен force `recompile` для реального импорта split. Верификация:
`ScriptAssemblies/` содержит 6 split-DLL (старая `Shtl.Mcp.Editor.dll` исчезла), 0 ошибок; полный сьют
**52/52** против реального split, reload-spanning (23→24) жив через границы сборок, status отзывчив
(max 0.10с). Характеризационный рефакторинг — поведение не менялось; реализация architecture.md
§Модульность (отклонения — имена/размещение Logging,Common,DispatchingToolInvoker — зафиксированы в architecture.md).

## [2026-06-24] forward | T3 set_play_mode + recompile-as-job | m2-play-recompile
Reload-триггерящие команды переведены на async-job (INV-1): обобщённый `ReloadJobs` (durable-маркер
`Shtl.Mcp.ReloadJob` + финализация после reload). `recompile` переписан с fire-and-forget на job (done
`{reloaded,status:recompiled}` по росту reloadCount; fail с ошибками компиляции из
`CompilationPipeline.assemblyCompilationFinished`; grace 5с → no-changes). `set_play_mode` новый
(`EnterPlaymode`/`ExitPlaymode`, done по `playModeStateChanged`). Проводка: подписки в EnsureStarted
(переживают reload), FinalizeOnTick в WatchdogTick. reloadCount как `Func<int>` — DAG сборок (T2) цел.
Верификация: 59/59 (7 новых ReloadJobs-тестов); e2e — recompile job доставлен ПОСЛЕ самотриггернутого
reload (1.6с, RED-gate), set_play_mode play(1.1с)/edit(0.5с), редактор вернулся в edit. Реализация
F3/AC3.1, F4/AC4.2 (lifecycle-and-reload §2 уже перечисляет эти тулы как async) — raw не менялся.

## [2026-06-24] forward | T4 assets (CRUD + clear_logs + refresh_assets) | m2-assets
7 тулов: `clear_logs` (LogBuffer.Clear), `refresh_assets` (job через ReloadJobs, делит канал с recompile),
`find_assets`/`read_asset`/`move_asset`/`delete_asset`/`create_folder` (обёртки AssetDatabase, main-thread).
read_asset — текст для texty <256KB иначе метаданные (File.Exists-guard). Размещение: ClearLogsTool,
RefreshAssetsTool, AssetTools (5 классов). Добавлен LogBuffer.Clear. Верификация: 63/63 (+4: clear,
CRUD round-trip на реальном AssetDatabase, ошибки); e2e через MCP — find/read(content)/clear ок,
refresh_assets job → done{no changes} после grace. Реализация F3/AC3.1, F4/AC4.2 — raw не менялся.

## [2026-06-24] forward | T5 prefabs | m2-prefabs
5 тулов (`PrefabTools.cs`): create_prefab (из scene-объекта или пустой), open/save/close (prefab-stage),
instantiate_prefab. Находка: Unity именует корень пустого prefab по basename ассета (не по arg `name`) —
зафиксировано. Верификация: 66/66 (3 prefab-теста), round-trip на реальном PrefabUtility/PrefabStage
(create→open→save→close→instantiate + ошибки). Реализация F3/AC3.1 — raw не менялся.

## [2026-06-24] forward | T6 scene-objects (T6a+T6b) | m2-scene-objects
12 тулов сцены. T6a (SceneObjectTools): хелпер SceneObjects (резолв по пути/имени вкл. неактивные) +
get_hierarchy/find_gameobject/gameobject_create/modify/destroy/set_parent. T6b (SceneEditTools): хелпер
SerializedValues + get_object/modify_object (SerializedObject), open_scene/save_scene, get/set_selection.
Верификация: 72/72 (+6 тестов: CRUD round-trip на реальной сцене, modify SerializedProperty, selection),
30 тулов. Реализация F3/AC3.1, AC4.5 — raw не менялся.

## [2026-06-24] forward | T9 screenshot | m2-screenshot
`screenshot(view=game|scene)` — рендер камеры в RenderTexture → PNG → base64 как MCP image-content.
McpRouter расширен конвенцией `_content` (тул отдаёт готовые content-элементы). Верификация: router
unit-тест на passthrough, 73/73, e2e — валидный PNG image-content (game/scene). Реализация F3/AC3.1 — raw не менялся.

## [2026-06-24] forward | T11 config + T8 escape hatches | m2-config, m2-escape-hatches
T11: ShtlMcpConfig расширен (port range, heartbeat-clamp, footgun AllowRunCsharp; EditorPrefs);
PortAllocator range-overloads; ResolvePort/Bootstrap.Tick читают конфиг; `get_config` (read-only, снимок
инжектируется Func'ом — Tools не зависит от Lifecycle). 76/76. T8: `execute_menu_item`; `run_csharp`
(footgun, gated AllowRunCsharp) — runtime-компиляция через CodeDom CSharpCodeProvider РАБОТАЕТ в этом Unity
(тест `40+2`→«42»). 79/79; e2e gate (off → disabled). Реализация F3/AC3.1, F2/AC2.1 — raw не менялся.

## [2026-06-24] forward | T10 control-flag | m2-control-flag
CheckControlFlag в watchdog-тике: атомарный read+delete `~/.unity-mcp/<serverName>.cmd`, `restart` →
RestartNow (пересоздать listener при зависшем HTTP, пока тикает главный поток). status получил поле
`recovery` (playbook, AC2.7). E2E: запись restart-флага → файл потреблён, listenerUptime сброшен,
reloadCount неизменен (рестарт листенера, не reload) = AC2.6. Полный F7 discoverability → M3.
Реализация F2/AC2.6-2.7 (lifecycle §4) — raw не менялся.

## [2026-06-24] review-fix | M2 quality pass | 3 ревью-агента (надёжность/корректность/безопасность)
Триаж: ложные срабатывания отсеяны (C1 гонка Dictionary — митигирована guard'ом `if(IsListening)return`
в EnsureStarted; H4 RunOnMain-timeout — опровергнут e2e; самоэскалация run_csharp — невозможна, флаг
не пишет ни один тул; path-traversal — отсечён GUID-гейтом до файлового чтения). Исправлены реальные
(12): SerializedValues.Write cast-guard (modify_object → structured {error}); read_asset try/catch;
set_selection non-string guard; save_scene untitled → {error} (иначе модальный диалог вешает поток);
find_gameobject cap 200; set_play_mode/recompile try/catch + ReloadJobs.AbortPending (иначе reload-канал
залочен на backstop); recompile grace учитывает isUpdating (ложный «no changes»); JobStore.Complete/Fail
только running→терминал (идемпотентность двойной финализации); screenshot tex в finally (утечка на
exception); TestRunnerNoThrottle.RecoverOnLoad из Init при Enabled=false (иначе редактор навсегда в
no-throttle после краша); ReattachIfPending hideFlags+DestroyImmediate; **HttpServer Host/Origin-фильтр**
(DNS-rebinding/CSRF — Origin→403, MCP-клиент не затронут). +2 регресс-теста. 81/81; e2e: Origin→403,
нормальный/MCP-клиент→200, set_play_mode happy-path цел.

## [2026-06-24] milestone-complete | M2
🎉 M2 завершён: T1–T11 done. 34 тула (полный Core-тулсет), 79 EditMode-тестов зелёные. Ключевое:
async-job + reload-spanning (run_tests/recompile/set_play_mode/refresh_assets переживают reload),
no-throttle (отзывчивость в фоне), 6-asmdef split (Transport/Dispatcher/Registry/Tools/Lifecycle/UI,
цикл разорван DI), AssetDatabase/prefab/scene-object/SerializedObject CRUD, screenshot (image-content),
config-бэкенд + footgun run_csharp (CodeDom), control-flag форс-рестарт. Все верифицированы headless e2e
автономной дев-петлёй. Долги → M3: INV-3 identity-инъекция, F7 discoverability, config UI, PlayMode
DisableDomainReload, прогресс-стриминг.
