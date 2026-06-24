# TASK: T10 — control-flag (LLM-инициируемый форс-рестарт)

**Status:** done (e2e: флаг потреблён + листенер пересоздан; 79/79)
**Привязка:** F2/AC2.6 (control-flag), AC2.7 (recovery-подсказка). Системы: lifecycle-and-reload.md §4.

## Реализация

- **Control-channel** (`ShtlMcpServer.CheckControlFlag`, с тика watchdog): читает
  `~/.unity-mcp/<serverName>.cmd`, **атомарно** (read+delete за один тик → не исполнить дважды); значение
  `restart` → `RestartNow()` (пересоздать listener). Работает, когда HTTP завис, но главный поток тикает.
  Канал — файл (как реестр), ноль внешних процессов.
- **AC2.7 (recovery-подсказка)**: `status` несёт поле `recovery` с playbook (написать `restart` в
  `<serverName>.cmd`; watchdog исполнит). **Полная F7-discoverability** (recoveryHint во всех ответах,
  durable recovery-блок в реестре, host-крошка) — **M3** (вне M2 по объёму вехи).

## Верификация

- E2E (recovery-playbook): listenerUptime подрос (29с) → `printf restart > ~/.unity-mcp/unity-shtl-mcp.cmd`
  → через ~1.5с: cmd-файл удалён (потреблён), listenerUptimeSeconds сброшен в 1 (listener пересоздан),
  reloadCount неизменен (рестарт листенера, НЕ domain reload). AC2.6 ✓.
- `status.recovery` присутствует (AC2.7). Полный сьют 79/79 (контрол-флаг — интеграционный механизм,
  верифицирован e2e, как reload-survival).

## Долги
- F7 discoverability (recoveryHint в tool-ошибках, durable recovery-блок, host-крошка) → M3.
