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
`~/.unity-mcp/registry.json` — это **массив записей-инстансов**; каждая запись несёт
**собственный** блок `recovery` (с конкретными pid/путями этого инстанса), который
пишет heartbeat-пакет (переживает падение listener — пишется не из listener-потока):

```json
[
  {
    "projectName": "shtl-mcp", "serverName": "unity-shtl-mcp",
    "port": 9730, "pid": 64351, "mode": "edit", "...": "...",
    "recovery": {
      "controlFlagPath": "~/.unity-mcp/<serverName>.cmd",
      "registryPath":   "~/.unity-mcp/registry.json",
      "steps": [
        "MCP call failed? This registry names the instance (pid, port).",
        "Unity alive? kill -0 <pid>.",
        "alive but listener wedged → write 'restart' to controlFlagPath, wait ~2s, reconnect, retry.",
        "pid dead → a human must reopen Unity (out of MCP scope).",
        "ping answers but main-thread tools time out → main thread blocked (modal/compiling); a human may need to dismiss the modal."
      ],
      "restartCommand": "printf 'restart' > '<controlFlagPath>'"
    }
  }
]
```
Recovery — per-instance (а не верхнеуровневый): шаги несут pid/путь именно этого
инстанса, поэтому модель, нашедшая свою запись по `port`/`projectPath`, сразу
получает готовую инструкцию без подстановки. Это файл, который модель и так читает
первым шагом. Опц. дубль — `~/.unity-mcp/RECOVERY.md` (для `ls`-обнаружения и людей).

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

**Реализовано (M4/T2, вариант «а»):** `HostBreadcrumb` (Lifecycle) — `Text()` (точный markdown-блок с
маркером `<!-- shtl-mcp-recovery -->`), `IsPresent`/`AddTo` (идемпотентно, создаёт `CLAUDE.md` если нет),
`ResolveTargetPath` (выбор целевого `CLAUDE.md`). Unity-проект часто вложен в репозиторий
(`repo/Client/<Project>/`), а Claude Code при старте грузит `CLAUDE.md` от корня репозитория — поэтому
кандидаты собираются на каждом уровне от git-корня (директория с `.git`; и dir, и file — worktree)
вниз до корня Unity-проекта. Приоритет: файл с уже добавленным маркером (идемпотентность между
версиями) → существующий `CLAUDE.md`, ближайший к git-корню → git-корень (файл будет создан);
Unity-проект вне git — корень Unity-проекта.
Дашборд: свёрнутый foldout с пояснением, разрешённым целевым путём, **превью точного
текста** и кнопкой → `EditorUtility.DisplayDialog`-подтверждение → запись. Запись ТОЛЬКО по клику+подтверждению
(human-инициированный модал — не MCP-freeze). Статус виден без открытия foldout'а — в его заголовке:
«✓» когда крошка уже в host-`CLAUDE.md`, иначе ненавязчивое «— recommended» с tooltip-объяснением (cold-start).
Вариант «б» (recovery-скилл) не делали — однострочной крошки достаточно для cold-start.

## Связь
- Механизм рестарта — `lifecycle-and-reload.md` §4 (control-channel).
- Контракт ответов (`recoveryHint`) — `command-set.md`.
- Дерево для разработчика — `CLAUDE.md §Recovery`.
