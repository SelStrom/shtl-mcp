---
entity: recovery-discoverability
content_class: intent-derived
source_refs:
  - raw/features/F7-recovery-discoverability.md
  - raw/features/F2-config-and-restart.md
compiled_at_commit: pending
epoch: 001
status: active
needs_review: false
---

# Доставка знания о восстановлении использующей модели

Проблема: recovery playbook бесполезен, если использующая модель его не видит.
Она работает в host-проекте (не в репо shtl-mcp), а при мёртвом сервере MCP-канал
недоступен. Решение — defense-in-depth по трём каналам + опциональная host-крошка.

## Две аудитории — не путать
- **Разработчик shtl-mcp** — видит `CLAUDE.md §Recovery` этого репо. Это для
  написания/тестирования.
- **Использующая модель** — host-проект, dev-репо не видит. Получает знание
  только по каналам ниже.

## Канал 1 — самодокументируемый реестр (durable, primary)
`~/.unity-mcp/registry.json` несёт верхнеуровневый блок `recovery`, который пишет
пакет при старте (переживает падение listener):

```json
{
  "recovery": {
    "controlFlagPath": "~/.unity-mcp/<serverName>.cmd",
    "steps": [
      "1. найди свой инстанс в массиве instances[] по projectPath",
      "2. kill -0 <pid>: жив ли Unity",
      "3a. pid жив  → printf 'restart' > <controlFlagPath>; подождать ~2с; claude mcp reconnect",
      "3b. pid мёртв → Unity закрыт: попроси человека открыть Unity (вне зоны MCP)"
    ]
  },
  "instances": [ { "...": "..." } ]
}
```
Это файл, который модель и так читает первым шагом → инструкция оказывается прямо
под рукой. Опц. дубль — `~/.unity-mcp/RECOVERY.md` (для `ls`-обнаружения и людей).

## Канал 2 — пре-брифинг через MCP (covers основной кейс)
- `initialize.instructions` (MCP отдаёт при подключении) и описание инструмента
  `status` содержат однострочник: «если стану недоступен → читай
  `~/.unity-mcp/registry.json` → `recovery`».
- Праймит любую модель, которая хоть раз подключилась. Это **основной** сценарий:
  работала с MCP → он упал при recompile/crash → модель помнит, куда смотреть.

## Канал 3 — recoveryHint в ответах
- Пока сервер жив, каждый ответ инструмента несёт `recoveryHint` со ссылкой на
  реестр (см. `command-set.md` §Контракт).

## Cold-start: честный остаток и opt-in
Свежая сессия, сервер уже мёртв, модель ни разу не подключалась → каналы 2–3 не
сработали, остаётся только канал 1 (если модель догадается заглянуть в
`~/.unity-mcp/`) и видимость в конфиге Claude Code отключённого `unity-<project>`.

Полное закрытие — **opt-in host-крошка** (AC7.4), только с явного согласия:
- (а) однострочный указатель в `CLAUDE.md` host-проекта:
  «Unity MCP `unity-*` недоступен? → `~/.unity-mcp/registry.json` → `recovery`».
- (б) либо минимальный recovery-скилл в host-проекте.

Предлагается через дашборд/онбординг. По умолчанию — ничего (INV-2): без согласия
в папку llm не пишется ни байта.

## Связь
- Механизм рестарта — `lifecycle-and-reload.md` §4 (control-channel).
- Контракт ответов (`recoveryHint`) — `command-set.md`.
- Дерево для разработчика — `CLAUDE.md §Recovery`.
