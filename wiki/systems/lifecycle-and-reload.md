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

## Главный поток (AC4.6)
- HTTP-поток **никогда** не трогает Unity API. Только `enqueue` в
  `MainThreadDispatcher`, исполнение — в `EditorApplication.update`.
- Синхронные команды: фоновый поток ждёт результат с таймаутом; async — сразу
  `jobId`.

## Play vs Edit для инструментов (AC4.5)
- Осмысленные в Play (`get_hierarchy`, `find_gameobject`, `screenshot`,
  `get_logs`, `run_csharp`, `get_object`…) — работают в Play.
- Editor-only (создание префаба-ассета, операции AssetDatabase, осмысленные вне
  Play) при вызове в неподходящем режиме → понятная ошибка в ответе, не краш.

## Точки верификации
- PlayMode-тест: соединение/job переживают play→edit-переход (RED без механизма 1–2).
- EditMode-тест: JobStore сериализуется/восстанавливается через эмулированный
  reload (`SessionState` round-trip).
