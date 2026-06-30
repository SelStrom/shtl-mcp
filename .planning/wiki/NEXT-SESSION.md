# Промпт следующей сессии (shtl-mcp) — v1 feature-complete

> Скопировать как стартовое сообщение следующей сессии (или прочитать как бриф).

---

Продолжаем shtl-mcp. **M1–M4 завершены** (на `main`). 🎯 **Вся спека F1–F7 реализована — v1
feature-complete** (`7a52d1f` feat + `38d10f7` docs — M4; финальный adversarial-ревью пройден,
0 blocker/major). Контекст: `CLAUDE.md`, `.planning/wiki/m4-plan.md`, `.planning/wiki/index.md`,
`.planning/wiki/log.md` (хвост — `M4 T3+T4 — M4 завершён`).

## Где мы

- **F1–F7 + INV-1..5 закрыты.** 35 тулов (+`ping`), **131/131 EditMode** зелёные, PlayMode-прогон e2e
  работает. M4: call-tail (AC5.5), host-крошка (AC7.4), idle-keepalive (AC4.10, opt-in best-effort);
  per-project config (T3) закрыт анализом (committed config = security-регресс для footgun).
- **Дискреционные долги (не блокеры):** bg-thread-«будилка» для idle (эскалация AC4.10, если тогл
  недостаточен на целевом LTS — нужен новый raw-AC); v2-тулы (профайлер/packages CRUD — F3 out-of-scope,
  escape-hatch-covered); get_job→✗ в call-tail при упавшем запрошенном job (working-as-design, опц. полировка).
- Новой вехи в raw нет — следующая работа начинается с явного запроса пользователя (фича/долг).

## ⚠️ ПЕРВЫЙ ШАГ — проверить, что Unity жив

```
curl -s --max-time 3 -X POST -d '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"status","arguments":{}}}' http://127.0.0.1:9730/
```
`health=ok` → продолжай. Молчит → recovery playbook (`CLAUDE.md §Recovery`): `cat ~/.unity-mcp/registry.json`
(свежий `lastHeartbeat`? есть `recovery`-блок), `kill -0 <pid>`. `ping` отвечает, а `status` — нет →
главный поток заблокирован (модал/компиляция); человек дисмиссит модал / фокусит окно Unity. pid мёртв →
**Unity моделью не запускаем** — попросить человека открыть Unity.

> **Важно про модалы (урок M3):** блокирующий модал (включая «Save scene?» от Test Runner на грязной сцене)
> вешает MCP — реактивно через MCP его не закрыть. `run_tests mode=PlayMode` теперь modal-free
> (`scenePolicy`: discard-дефолт/save/abort). Если ловишь зависание — `ping` отличит «модал» от «сервер мёртв».

## Дев-петля (автономно)

Новые тулы видны server-side после компиляции; MCP-клиент — после `/mcp`-reconnect (пользователь).
Headless через `curl ... http://127.0.0.1:9730/`. Новые `.cs`-файлы импортировать `refresh_assets` (не просто
`recompile` — он не подхватит новые файлы). Цикл: правка → `refresh_assets` → поллинг `status`
(isCompiling→false) → `get_logs(error)` → `run_tests`. Полный сьют через MCP безопасен. Хук brace-style:
`{}` без однострочных if/else и без `do...while`-форм.

## Задача: нет активной — ждём запрос пользователя

M4 (и вся спека) закрыты. Новой работы в raw не запланировано. Возможные направления, если попросят:
- **bg-thread idle-«будилка»** — эскалация AC4.10, ЕСЛИ keepalive-тогл окажется недостаточен против фонового
  троттла на целевом LTS (проверять расфокусом окна, не headless). Меняет поведение → forward-поток (новый AC).
- **v2-тулы** (профайлер, packages CRUD, материалы/шейдеры) — F3 out-of-scope, покрыты escape hatches; делать
  только под конкретную нужду.
- **Визуальная приёмка дашборда** человеком (Window/Shtl MCP): Recent calls, Host breadcrumb foldout, idle-
  keepalive тогл — UI-glue, тестов нет.
- Любая новая фича/долг — начинать с фиксации намерения в `raw/` (forward-поток).

## Инварианты (не нарушать) — `raw/domain/overview.md` INV-1..5; forward-поток raw→wiki→code для изменения поведения. run_csharp — human-only footgun (никакой тул не выставляет AllowRunCsharp). HttpServer — loopback-only + Host/Origin fail-closed.
