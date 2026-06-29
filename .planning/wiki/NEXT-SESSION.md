# Промпт следующей сессии (shtl-mcp) — старт M4

> Скопировать как стартовое сообщение следующей сессии (или прочитать как бриф).

---

Продолжаем shtl-mcp. **M1–M3 завершены** (на `main`), code-review M3 + ремедиация закоммичены
(`faf2262` fix + `bc99a77` docs). Контекст: `CLAUDE.md`, `.planning/wiki/m4-plan.md`,
`.planning/wiki/index.md`, `.planning/wiki/log.md` (хвост — `forward | PlayMode e2e + code-review M3`).

## Где мы

- **Вся спека F1–F7 почти закрыта.** Остались ДВА невыполненных AC (см. `m4-plan.md`):
  **AC5.5** (дашборд: хвост последних N вызовов) и **AC7.4** (opt-in host-крошка). Закрыть их = v1
  feature-complete. Остальное (per-project config, idle-keepalive, v2-тулы) — опц./дискреционно.
- 35 тулов (+`ping`), **112/112 EditMode** зелёные, PlayMode-прогон e2e работает.

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

## Задача: исполнять M4 по волнам (`m4-plan.md`)

1. **T1 `m4-call-tail`** (AC5.5) — ring-buffer последних N вызовов (метод/статус/длительность/время) в единой
   точке (`McpRouter.Handle`/`DispatchingToolInvoker`) + рендер в дашборде. Реализация F5, raw есть.
2. **T2 `m4-host-breadcrumb`** (AC7.4) — opt-in кнопка дашборда: с явного согласия пишет recovery-указатель
   в host-`CLAUDE.md`. По умолчанию ничего (INV-2). **Outward-facing — показать текст до записи, согласие в
   моменте.** Реализация F7, raw есть.
3. (опц.) **T3** per-project config, **T4** idle-keepalive (T4 — forward-поток, raw сначала; интент по
   recovery-gap человеком пока не задан — согласовать постановку).

## Инварианты (не нарушать) — `raw/domain/overview.md` INV-1..5; forward-поток raw→wiki→code для изменения поведения. run_csharp — human-only footgun (никакой тул не выставляет AllowRunCsharp). HttpServer — loopback-only + Host/Origin fail-closed.
