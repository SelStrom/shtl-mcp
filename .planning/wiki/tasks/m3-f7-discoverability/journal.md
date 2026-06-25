# Journal — m3-f7-discoverability (T3, append-only)

## [2026-06-25] реализация
F7 discoverability через durable-ФС + пре-брифинг + recoveryHint. `RecoveryInfo` в InstanceEntry (registry
сериализует camelCase) → recovery-блок переживает падение сервера (его и так читают первым). Шаги включают
новый кейс из M3/T1: `ping` отвечает, а main-thread-тулы виснут → модал/компиляция, нужен человек.
initialize.instructions переписаны (registry + ping). recoveryHint — терсный указатель, инжектится в
McpRouter (как projectName). AC7.4 host-крошка отложена (opt-in + дашборд T4, INV-2). 88/88; e2e все три
канала. Реализация F7 — raw не менялся.
