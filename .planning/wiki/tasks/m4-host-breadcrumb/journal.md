# Journal — m4-host-breadcrumb (M4/T2, append-only)

## [2026-07-01] реализация (AC7.4)

Opt-in host-крошка — последний кусок F7.

- **`HostBreadcrumb`** (Lifecycle) — чистые: `Text()` (точный markdown-блок: маркер
  `<!-- shtl-mcp-recovery -->` + однострочный recovery-указатель с `~/.unity-mcp/registry.json` и про `ping`),
  `IsPresent(content)` (детект по маркеру), `TargetPath(root)` → `<root>/CLAUDE.md`; + `AddTo(path)` (IO:
  идемпотентно дописать, создать файл если нет, корректный разделитель по хвосту существующего).
- **`DashboardWindow`** — свёрнутый foldout «Host recovery breadcrumb»: пояснение (opt-in, cold-start, INV-2
  «по умолчанию ничего»), целевой путь (`parent(Application.dataPath)/CLAUDE.md`), **превью точного текста**
  (read-only TextField), кнопка → `EditorUtility.DisplayDialog`-подтверждение → `AddTo`. Уже добавлено →
  «✓ already present» (детект на рендере). Запись только по клику+подтверждению.

**Outward-facing — дисциплина:** живую запись в CLAUDE.md автономно НЕ триггерил. Модал подтверждения —
human-инициированный (клик по кнопке), это не MCP-freeze (тот про автономные модалы во время MCP-вызовов).
Двойная точка согласия: превью текста + confirm-диалог с целевым путём.

**Тесты:** `HostBreadcrumbTests` (5: Text содержит маркер/registry/ping; IsPresent +/−/пусто/null; TargetPath
= CLAUDE.md у корня; AddTo append+идемпотентность с сохранением исходного; AddTo создаёт отсутствующий файл) —
против реальных temp-файлов. **126/126 EditMode зелёные**, компиляция чистая. Реализация F7/AC7.4 — raw не
менялся; wiki [[recovery-discoverability]] §Cold-start обновлён (вариант «а» реализован, «б» не нужен).

**T2 done. → Спека F1–F7 реализована полностью (v1 feature-complete).**
