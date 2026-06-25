# TASK: T3 — F7 discoverability

**Status:** done (AC7.1-7.3; AC7.4 host-крошка отложена — нужно согласие + дашборд T4)
**Привязка:** F7 (AC7.1-7.4). Реализация существующего raw — raw не менялся.

## Реализация

- **AC7.1 durable recovery-блок:** `RecoveryInfo` (controlFlagPath, registryPath, steps[5], restartCommand)
  в `InstanceEntry.Recovery`; `ShtlMcpServer.BuildRecovery(pid)` заполняет, `Heartbeat` пишет в
  `registry.json` (пакет, не listener → переживает падение). Шаги покрывают: read registry → kill -0 pid →
  restart-флаг → pid мёртв (человек) → `ping` отвечает но status виснет (модал/компиляция, человек).
- **AC7.2 пре-брифинг:** `initialize.instructions` усилены (registry recovery-блок + `ping` отличает
  wedged от dead). `status.recovery` (из M2/T10) — на месте.
- **AC7.3 recoveryHint:** `McpRouter` инжектит терсный `recoveryHint` (указатель на registry) в text-ответы
  (рядом с `projectName`).
- **AC7.4 host-крошка:** opt-in, требует явного согласия + UI (дашборд) → с T4/после; автономно не делаем (INV-2).

## Верификация

- Unit: `McpRouterTests.ToolsCall_InjectsRecoveryHint_F7`. 88/88.
- E2e: registry.json `recovery` (controlFlagPath + restartCommand + 5 steps); `recoveryHint` в ответах
  (ping/get_logs); initialize.instructions усилены.
