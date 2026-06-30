# TASK — m4-per-project-config (M4/T3, опц.)

## Цель (исходная)
Рассмотреть per-project config: ProjectSettings-provider, зеркалящий EditorPrefs-конфиг, чтобы настройки
ехали с проектом (F2/AC2.1 «ProjectSettings-asset ИЛИ EditorPrefs»).

## Итог: НЕ строим — committed per-project config неверен для этих настроек

Анализ показал, что per-project (committed) хранение — антипаттерн здесь:

- **`AllowRunCsharp` (footgun)** — перенос в committable/project-asset = **security-регресс**: клон чужого
  проекта молча включил бы «исполнение произвольного Editor-C#». Footgun обязан быть machine-local +
  off-by-default + human-set. (Закреплено комментарием-инвариантом в `ShtlMcpConfig`.)
- **`Enabled` (авто-старт)** — выбор каждого разработчика (его редактор/машина), не команды. Machine-local
  лучше: твой OFF — твой, не коммитится команде.
- **PortRangeStart/Count** — избегание коллизий портов на конкретной машине → machine-семантика.
- **HeartbeatSeconds** — перф-тюнинг → machine.

**EditorPrefs (machine-local) — правильный дом для всех.** AC2.1 явно допускает EditorPrefs → **уже
удовлетворён**. SettingsProvider тоже отклонён: над EditorPrefs он навязывает ложную модель «project-scoped»
(пользователь ждёт, что Project Settings едет с VC, а это machine-local) + второй config-surface противоречит
духу INV-4 (одно окно — дашборд). Дашборд уже редактирует весь конфиг (AC2.1 «редактируется из дашборда»).

## Сделано
- Явный security-инвариант в `ShtlMcpConfig`: footgun обязан оставаться machine-local; класс-doc объясняет
  machine-vs-project решение.
- Это решение зафиксировано (раз нет committed-config — нет и forward-потока: поведение не меняется).

## Статус
✅ Closed by analysis — не строим (committed per-project = security-регресс для footgun + неверная семантика
для остальных). AC2.1 удовлетворён EditorPrefs. Если команда захочет шарить настройки — обсудить отдельно
(но НЕ footgun).
