# TASK: T1 — modal-free scene ops + bg-liveness

**Status:** done (T1a bg-liveness ✅; T1c sceneDirty ✅; T1b dirtyScene-политика — СНЯТА по итогу аудита)
**Привязка:** F4/AC4.8 (bg-liveness), F4/AC4.9 (modal-free scene ops). Forward-поток: raw F4 (AC4.8/4.9
добавлены) → wiki (lifecycle-and-reload §bg-liveness) → code. Интент задан человеком (инцидент M2: блокирующий
«Save Scene?» вешал MCP).

## Контекст (инцидент M2)

Блокирующий модал крутит вложенный run-loop на главном потоке → `EditorApplication.update` не тикает →
main-thread-тулы виснут (листенер на фоне жив). **Реактивно закрыть модал через MCP нельзя** — канал убит
этим же модалом. Значит: (1) уметь это ДЕТЕКТИТЬ (bg-liveness), (2) не давать модалу появиться (programmatic
dirtyScene-решение по политике LLM).

## T1a — bg-liveness (AC4.8) ✅

- `MainThreadDispatcher.LastDrainUtc` — метка последнего дренажа (Interlocked, читается с фонового потока).
- `PingTool` (`ping`, **NeedsMainThread=false**): `{alive, mainThreadAgeSeconds, listenerUptimeSeconds,
  mainThreadResponsive, note?}`. Большой age при живом ответе = главный поток завис (модал/компиляция/тяжёлая
  операция), а не сервер мёртв. Инжекция `Func<DateTime>`/`Func<double>` (Tools не зависит от Lifecycle).
- Тесты (`PingToolTests`, 4): Drain обновляет метку; ping NeedsMainThread=false; recent→responsive без note;
  stale(30с)→wedged+note. **85/85**; e2e ping responsive.

## T1c — sceneDirty наружу (AC4.9) ✅

`get_hierarchy` отдаёт `sceneDirty` (`Scene.isDirty`) + `scenePath` (пусто = untitled). LLM проактивно
решает: сохранить через `save_scene` перед разрушающей операцией или продолжить с потерей. Тривиальный
passthrough — без отдельного теста (e2e: `{scene:'', scenePath:'', sceneDirty:false}`).

## T1b — dirtyScene-политика (AC4.9) — СНЯТА (аудит)

**Аудит промптящих операций:** наши scene-рушащие тулы зовут API НАПРЯМУЮ, а они **не промптят**:
- `EditorSceneManager.OpenScene(path, Single)` — не зовёт save-промпт (его даёт menu-обёртка
  `SaveCurrentModifiedScenesIfUserWantsTo`, которую меню вызывает ДО OpenScene; наш `open_scene` — нет).
- `EditorApplication.EnterPlaymode()` — не промптит (играет с in-memory сценой).
- `AssetDatabase.Refresh()` / domain reload — грязную сцену сохраняют в памяти без промпта.
- `save_scene` untitled — уже `{error}`, не диалог (M2-фикс).

Вывод: блокирующего модала наши тулы НЕ открывают → программная `dirtyScene`-политика лечила бы
несуществующую болезнь. Модал инцидента M2 был **инцидентным** (merge-чехарда M2→M1→M2 + force-recompile),
не от наших тулов. Реальное покрытие AC4.9: bg-liveness (детект любого модала) + sceneDirty (проактивное
решение). Тулы используют непромптящие API — это и есть «modal-free».

## Acceptance

- AC4.8 (bg-liveness): ✅ — `ping` отвечает при заблокированном главном потоке, отличает wedged от dead.
- AC4.9 (modal-free): ✅ — наши scene-тулы используют непромптящие API (аудит); `sceneDirty` наружу для
  проактивного решения LLM; блокирующего модала тулы не открывают.
