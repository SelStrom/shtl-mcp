---
entity: lifecycle-and-reload
content_class: intent-derived
source_refs:
  - raw/features/F2-config-and-restart.md
  - raw/features/F4-play-edit-reload.md
compiled_at_commit: pending
epoch: 001
status: active
needs_review: false
---

# Жизненный цикл и выживание при domain reload

Это **ядро надёжности**. Domain reload (перекомпиляция, вход/выход Play при
Reload Domain ON) выгружает C#-домен → managed-состояние и сетевой listener
гибнут. У конкурентов это рвёт мост (CoderGamester советует отключать Reload
Domain; CoplayDev имеет баг утечки TcpListener после reload). Решаем управляемо,
**без нативного кода** — тремя механизмами вместе.

## 1. Re-spawn listener вокруг reload
- `[InitializeOnLoad]` static ctor → точка входа после каждой загрузки домена
  (старт Editor, после reload). Поднимает сервер, если он должен работать.
- `AssemblyReloadEvents.beforeAssemblyReload` → чистое закрытие `HttpListener`
  и сокетов (иначе утечка/занятый порт, см. баг конкурента).
- `AssemblyReloadEvents.afterAssemblyReload` → переподнятие.
- Состояние (выбранный порт, флаг «сервер должен работать», активные jobs) — в
  `SessionState` (переживает domain reload в пределах сессии Editor).
- Окно недоступности ~секунды — клиент ретраит; это допустимо.
- **Наблюдаемость re-spawn (AC4.7).** `beforeAssemblyReload` инкрементит durable
  `reloadCount` (`SessionState`); каждый успешный re-spawn (`EnsureStarted`) ставит
  свежий `listenerUptimeSeconds` (in-domain, не persisted → естественно сбрасывается).
  Оба — в `status`. Durable `uptimeSeconds` (от `StartedTicks`, ставится раз за
  сессию) один re-spawn не отличает; новые счётчики делают reload/rebind видимыми
  по live-каналу и фальсифицируемыми вручную.

## 2. Async-job модель (для долгих/reload-команд)
Команды, которые сами триггерят reload или идут долго, **нельзя** ждать на одном
HTTP-соединении (домен умрёт вместе с запросом). Поэтому:

- `set_play_mode`, `recompile`, `run_tests`, тяжёлый `refresh_assets` —
  немедленно возвращают `{ "jobId": "..." , "status": "running" }`.
- Job регистрируется в **JobStore** (сериализуется в `SessionState`) → переживает
  reload.
- После reload сервер поднимается, job продолжает/завершается, результат пишется
  в JobStore.
- Модель опрашивает `get_job(jobId)` → `running | done | failed` + payload/ошибки.
- Идемпотентность: `get_job` по неизвестному id → понятная ошибка, не падение.

**Focus-throttle во время прогонов (`run_tests`).** Unfocused Unity троттлит editor update → тест-прогон
ползёт, главный поток голодает, async-опрос (`status`/`get_job`) висит всю длительность. Снимается
`TestRunnerNoThrottle` (паттерн CoplayDev): на время прогона `EditorPrefs ApplicationIdleTime=0` +
`InteractionMode=1`, впечатанные приватным `EditorApplication.UpdateInteractionModeSettings()`. Бэкап
исходных значений — **двухслойный**: `SessionState` (переживает reload) + marker-файл в `Library/`
(переживает краш — `EditorPrefs` durable, иначе редактор остаётся в no-throttle навсегда). Restore — в
`RunFinished`; `RecoverOnLoad` при reload держит full-rate для in-flight прогона и откатывает осиротевший
снимок. Orphan-таймаут (зависший running-job → авто-fail + restore) — с тика watchdog. Реализация —
`Editor/Tools/{TestRunnerNoThrottle,RunTestsTool,TestRunCallbacks}.cs`; механика — `tasks/m2-run-tests/`.

## 3. Рекомендация Enter Play Mode → Reload Domain OFF
- Отключение Reload Domain убирает domain reload при входе в Play (главный
  источник разрывов у конкурентов) — listener просто продолжает жить.
- Делается **только с согласия пользователя**: дашборд показывает текущий статус
  настройки и кнопку «применить рекомендуемое (OFF)» (AC4.4). Принудительно не
  меняем.
- Сервер обязан работать **и при ON, и при OFF** (механизмы 1–2 закрывают ON).

## Watchdog (самовосстановление, INV-5 / F2)
- Тик в `EditorApplication.update`: если сервер должен работать, но `HttpListener`
  мёртв/не слушает — переподнять. Покрывает «listener wedged при живом Unity».
- Heartbeat реестра (`multi-instance.md`) идёт тем же тиком.

## 4. Control-channel (LLM-инициируемый форс-рестарт)
Watchdog — единственный всегда-живой компонент (тикает независимо от listener'а),
поэтому он же исполняет внешние команды от модели:

- Модель через Bash пишет флаг `~/.unity-mcp/<serverName>.cmd` со значением
  (напр. `restart`).
- На ближайшем тике watchdog читает флаг → форс-пересоздаёт listener (даже если
  тот завис) → удаляет флаг.
- Работает, когда HTTP-сервер недоступен (канал — файл, как реестр); ноль внешних
  процессов, Unity не запускается.
- Ограничение: требует тикающего главного потока. Hard-frozen Unity = «Unity
  мёртв» → человек (вне зоны MCP).
- Чтение флага — атомарное (read+delete за один тик), чтобы не исполнить дважды.
- Операционное дерево для использующей модели — `CLAUDE.md §Recovery`.

## Главный поток (AC4.6)
- HTTP-поток **никогда** не трогает Unity API. Только `enqueue` в
  `MainThreadDispatcher`, исполнение — в `EditorApplication.update`.
- Синхронные команды: фоновый поток ждёт результат с таймаутом; async — сразу
  `jobId`.

## bg-liveness главного потока (AC4.8)
Блокирующий модал (`EditorUtility.DisplayDialog`/save-scene-промпт), тяжёлая операция
или компиляция занимают главный поток → `dispatcher.Drain` не исполняется →
main-thread-инструменты (`status`) виснут на таймауте `RunOnMain`. Листенер на фоне
жив (`tools/list` отвечает), но MCP-канал к Unity «мёртв». Реактивно закрыть модал
через MCP **нельзя** — канал убит ровно этим модалом.
- `MainThreadDispatcher` штампует `LastDrainUtc` на каждом `Drain` (главный поток).
- `ping`-инструмент (**`NeedsMainThread=false`** — исполняется на фоновом потоке,
  отвечает даже при заблокированном главном) отдаёт `mainThreadAgeSeconds` (now −
  LastDrainUtc) + `listenerUptimeSeconds`. Большой возраст при живом ответе = «главный
  поток завис (модал/компиляция/тяжёлая операция)», а не «сервер мёртв».
- Это диагностический сигнал; восстановление модала — программно (AC4.9, не звать
  промптящие API) либо человек/внешний канал для неожиданных модалов.

## idle-keepalive главного потока (AC4.10)
Тот же корень, что у bg-liveness, но **другой триггер — фоновый idle** (не модал).
В фоне (окно не в фокусе + простой) Unity троттлит `EditorApplication.update` вплоть
до секунд-минут между тиками. Так как и `dispatcher.Drain` (main-thread-инструменты),
и `WatchdogTick` (control-flag `.cmd`, heartbeat, re-spawn) — **единственные два
подписчика** `update`, глубокий idle заклинивает оба: `ping` (AC4.8) детектит
(`mainThreadAgeSeconds` растёт), но НИ авто-восстановление, НИ `.cmd`-рестарт (AC2.6)
не срабатывают — оба сами на затроттленном тике.
- **`IdleKeepAlive` (opt-in, default OFF)** — пока сервер включён и тогл активен,
  держит редактор в No-Throttling (`ApplicationIdleTime=0`/`InteractionMode=1`),
  переиспользуя prefs+`ForceApply` `TestRunnerNoThrottle`. `Reconcile(wanted)` зовётся
  с каждого `WatchdogTick` + `EnsureStarted` + тогла дашборда; **во время тест-прогона
  не вмешивается** (no-throttle держит `TestRunnerNoThrottle`), иначе приводит prefs к
  желаемому (wanted→full-rate, !wanted→Default) — поэтому переживает per-run Restore
  (на следующем тике переустанавливается).
- **Best-effort:** No-Throttling снимает foreground idle-cap; подавление именно
  ФОНОВОГО троттла **версионно-зависимо** (на патченных LTS 2022.3.54f1+/6000.x фон
  может троттлиться даже под No-Throttling) и не гарантируется. Источник истины о
  фактическом затыке — `ping` (AC4.8). Не строили bg-thread-«будилку» (`SignalTick`
  via reflection — internal/undocumented, хрупко; `QueuePlayerLoopUpdate`/`delayCall`
  не thread-safe) — отложено как эскалация, если тогл окажется недостаточен на целевом LTS.
- Компромисс: full-rate update в фоне = выше idle-CPU/расход батареи → тултип + default OFF.
- Реализация — `Editor/Tools/IdleKeepAlive.cs`, конфиг `ShtlMcpConfig.IdleKeepAlive`
  (machine-local), проводка — `ShtlMcpServer.{WatchdogTick,EnsureStarted,SyncKeepAlive}`.

## Play vs Edit для инструментов (AC4.5)
- Осмысленные в Play (`get_hierarchy`, `find_gameobject`, `screenshot`,
  `get_logs`, `run_csharp`, `get_object`…) — работают в Play.
- Editor-only (создание префаба-ассета, операции AssetDatabase, осмысленные вне
  Play) при вызове в неподходящем режиме → понятная ошибка в ответе, не краш.

## Точки верификации
- **Реализован** (EditMode `[UnityTest]`, таск `m1-reload-survival-test`): листенер переживает
  форсированный domain reload (`EditorUtility.RequestScriptReload` + `WaitForDomainReload`) и
  watchdog-rebind (`StopListenerForReload` + `WatchdogTick`) — проба `status` round-trip на том же
  порту. RED-gate подтверждён (без механизма 1 / без watchdog-ветки оба теста краснеют).
- **M2** (jobs): соединение/job переживают play→edit-переход; JobStore сериализуется/восстанавливается
  через эмулированный reload (`SessionState` round-trip).
