# Journal — breadcrumb-target-repo-root

## [2026-07-10] Репорт из host-проекта

PerfectWar: дашборд предлагает крошку в `Client/PerfectWar/CLAUDE.md`, хотя канонический
`CLAUDE.md` — в корне репозитория. Диагноз: `TargetPath` = `<UnityProjectRoot>/CLAUDE.md` без
учёта вложенности Unity-проекта в репо. Raw (F7/AC7.4) целевой путь не фиксирует — «CLAUDE.md
host-проекта», значит багфикс детали реализации, forward-поток raw-диффа не нужен.

## [2026-07-10] Реализация

- `HostBreadcrumb.TargetPath` → `ResolveTargetPath` (обход вверх до `.git`, приоритеты:
  маркер → существующий ближе к git-корню → git-корень → без git корень Unity-проекта).
- `DashboardWindow`: оба call-site переведены на `ResolveTargetPath`.
- Тест `TargetPath_IsClaudeMdAtRoot` заменён шестью тестами резолвинга (реальная ФС, temp-деревья).
- wiki `recovery-discoverability.md` §Реализовано — описание разрешения пути.

## [2026-07-10] Верификация

- EditMode-сьют через Unity CLI 2022.3.62f3 на TestProject~: **209/209 passed** (в т.ч. 6 новых
  Resolve_*-тестов и прежние AddTo/IsPresent/Text). AC-1..AC-4 закрыты.
