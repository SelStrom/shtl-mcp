# Journal — m3-modal-free-scene (T1, append-only)

## [2026-06-25] T1a bg-liveness (AC4.8)

Мотивация — инцидент конца M2: блокирующий «Save Scene?»-модал завесил MCP. Диагностика (sample стека):
главный поток парковался в `CFRunLoopRun`/`mach_msg` (вложенный modal-loop), `EditorApplication.update`
не тикал, листенер на фоне жив. Не отличить было «модал-блок» от «сервер мёртв» — это и закрываем.

Forward-поток: raw F4 (+AC4.8 bg-liveness, +AC4.9 modal-free) → wiki lifecycle-and-reload (§bg-liveness) →
код. `MainThreadDispatcher.LastDrainUtc` (Interlocked long, штамп на каждом Drain). `PingTool` (`ping`,
NeedsMainThread=false → отвечает на фоновом потоке): mainThreadAgeSeconds + responsive-флаг + note при
зависании. Инжекция Func'ами (DAG сборок цел). Верификация: 85/85 (+4 PingToolTests), e2e ping responsive
(age 0.0). 

T1b (dirtyScene-политика) / T1c (sceneDirty наружу) — следующими. Открытый вопрос: точный триггер модала
в инциденте не установлен (merge-чехарда + force-recompile + сьют) — перед T1b аудит/репро промптящих
операций. raw F4/AC4.8-4.9 — изменение намерения (интент задан человеком), forward атомарно для T1a.

## [2026-06-25] T1c sceneDirty + аудит → T1b снят

T1c: `get_hierarchy` отдаёт `sceneDirty`(`Scene.isDirty`)+`scenePath`. E2e: `{scene:'',scenePath:'',
sceneDirty:false}`. Тривиальный passthrough — без unit-теста. 85/85.

**Аудит T1b:** наши scene-рушащие тулы зовут API напрямую, а они НЕ промптят — `OpenScene(Single)`,
`EnterPlaymode`, `Refresh`/reload, `save_scene` untitled (уже `{error}`). Промпт даёт только menu-обёртка
`SaveCurrentModifiedScenesIfUserWantsTo`, которую наши тулы не зовут. Значит блокирующего модала тулы НЕ
открывают → `dirtyScene`-политика лечила бы несуществующую болезнь. **T1b снят.** Модал инцидента M2 был
инцидентным (не от наших тулов). AC4.9 покрыт: непромптящие API (modal-free по факту) + bg-liveness (детект)
+ sceneDirty (проактивное решение LLM). **T1 done.**
