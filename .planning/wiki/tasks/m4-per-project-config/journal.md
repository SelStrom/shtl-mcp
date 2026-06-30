# Journal — m4-per-project-config (M4/T3, append-only)

## [2026-07-01] исследование → закрыто без постройки

Рассматривал per-project (committed) config поверх текущего EditorPrefs. Вывод: для ЭТИХ настроек это
антипаттерн, EditorPrefs (machine-local) — правильный дизайн.

Ключевой аргумент — **security footgun**: `AllowRunCsharp` в committable/project-asset → клон чужого проекта
молча включает исполнение произвольного Editor-C#. Footgun обязан быть machine-local + off-by-default +
human-set. Остальные настройки тоже machine-семантичны: `Enabled` — выбор разработчика (не команды),
`PortRange` — избегание коллизий на машине, `Heartbeat` — перф.

AC2.1 («ProjectSettings-asset ИЛИ EditorPrefs, редактируется из дашборда») **уже удовлетворён** EditorPrefs +
дашбордом. SettingsProvider отклонён (ложная project-scoped семантика над machine-local EditorPrefs + второй
config-surface против духа INV-4).

**Сделано:** закрепил security-границу в `ShtlMcpConfig` (комментарий-инвариант: footgun обязан остаться
machine-local; класс-doc объясняет machine-vs-project). Поведение не менялось → forward-поток не нужен.
**T3 closed by analysis.**
