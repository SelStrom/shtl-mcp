---
content_class: intent-derived
epoch: 001
status: active
---

# F4 — Play/Edit и выживание при domain reload

**Требование #4.** Весь функционал работает и в play, и в edit mode.

Это **ядро надёжности**: domain reload убивает сетевой listener (подтверждено в
проде у конкурентов — мост рвётся при входе в Play). Решается управляемо, без
нативного кода. Механика — `wiki/systems/lifecycle-and-reload.md`.

## Acceptance criteria

- **AC4.1** — listener закрывается чисто на `AssemblyReloadEvents.beforeAssemblyReload`
  и переподнимается на `afterAssemblyReload` / `[InitializeOnLoad]`; порт и
  состояние сервера — в `SessionState` (переживает reload).
- **AC4.2** — Job, запущенный до reload, доступен по `get_job` после reload
  (состояние job переживает reload).
- **AC4.3** — Вход/выход Play mode не теряет сервер; работает при Reload Domain
  **ON и OFF**.
- **AC4.4** — Дашборд показывает статус настройки *Enter Play Mode → Reload Domain*
  и кнопку «применить рекомендуемое (OFF)».
- **AC4.5** — Все Core-инструменты, осмысленные в Play, работают в Play
  (`get_hierarchy`, `find_gameobject`, `screenshot`, `get_logs`, `run_csharp`…);
  Editor-only операции (напр. создание префаба-ассета), вызванные в неподходящем
  контексте, возвращают понятную ошибку, а не падают.
- **AC4.6** — Unity API вызывается **только** в главном потоке (через Dispatcher);
  фоновый HTTP-поток никогда не трогает Unity API напрямую.
- **AC4.7** — Re-spawn **наблюдаем** через `status`: поля `reloadCount` (число
  пережитых domain reload за сессию Unity; durable в `SessionState`) и
  `listenerUptimeSeconds` (время жизни текущего listener-инстанса; сбрасывается при
  каждом re-spawn — reload / watchdog-rebind / форс-рестарт). Дополняют durable
  `uptimeSeconds` (логическое время инстанса). Делают AC4.1/INV-5 верифицируемыми и
  по live-каналу, а не только тестом (durable `uptimeSeconds` один re-spawn не
  отличает). Разница сигналов различает reload (растёт `reloadCount` + сброс
  `listenerUptimeSeconds`) и watchdog-rebind (только сброс `listenerUptimeSeconds`).

## Out of scope
- Нативный (C) прокси, переживающий reload (отвергнут ради лёгкости).
- Принудительное отключение Reload Domain без согласия пользователя (только
  рекомендация + кнопка).
