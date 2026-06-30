# TASK — m4-host-breadcrumb (M4/T2)

## Цель
Закрыть **F7/AC7.4**: дашборд **предлагает** (opt-in) добавить в host-проект однострочный recovery-указатель
(в его `CLAUDE.md`). По умолчанию — **ничего** (INV-2). Закрывает cold-start: свежая LLM-сессия, сервер уже
мёртв, модель ни разу не подключалась → крошка в host-`CLAUDE.md` праймит её при старте.

## Привязка
- Фича/AC: **F7 / AC7.4**. Реализация зафиксированного raw — forward-поток не нужен.
- Системы: `recovery-discoverability.md` (Канал 4 — host-крошка), `dashboard.md`.
- Инварианты: **INV-2** (self-contained: по умолчанию НИЧЕГО, только opt-in), INV-4 (единственное окно).

## Подход (outward-facing — осторожно)
- `HostBreadcrumb` (Lifecycle) — чистые: `Text()` (точный markdown-блок с маркером), `IsPresent(content)`
  (идемпотентность по маркеру), `TargetPath(root)` (`<root>/CLAUDE.md`); + `AddTo(path)` (IO: дописать,
  создать если нет; no-op если маркер уже есть).
- `DashboardWindow` — свёрнутый foldout «Host recovery breadcrumb»: пояснение (opt-in, зачем), целевой путь,
  **превью точного текста** (read-only), кнопка «Add to host CLAUDE.md» → `EditorUtility.DisplayDialog`
  подтверждение (human-initiated модал — НЕ MCP-freeze) → запись. Если маркер уже есть → «✓ already present».
- Host-проект = корень Unity-проекта (`parent(Application.dataPath)`), где живёт host-`CLAUDE.md`.

## Acceptance
- По умолчанию ничего не пишется (кнопка только предлагает; запись — после явного подтверждения).
- Превью показывает ТОЧНЫЙ текст до записи; целевой путь виден.
- Идемпотентно: повтор не дублирует (детект по маркеру); создаёт `CLAUDE.md` если нет.
- DAG цел; регресс зелёный; unit-тесты на чистые функции + `AddTo` против temp-файла (реальная ФС).

## Шаги
1. `HostBreadcrumb.cs` (Lifecycle) + тесты (Text/IsPresent/TargetPath/AddTo append+идемпотентность+создание).
2. `DashboardWindow`: foldout + превью + кнопка + confirm + render-состояние.
3. refresh_assets → компиляция → сьют. **Живую запись в CLAUDE.md НЕ триггерить автономно** (outward-facing,
   за человеком); проверка — unit-тесты против temp-файлов.

## Статус
✅ Done — HostBreadcrumb + дашборд-foldout (превью + confirm). 126/126. Запись за человеком (не триггерил).
