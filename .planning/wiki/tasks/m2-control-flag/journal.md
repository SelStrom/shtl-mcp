# Journal — m2-control-flag (T10, append-only)

## [2026-06-24] control-channel + recovery-подсказка
CheckControlFlag в watchdog-тике: атомарный read+delete `~/.unity-mcp/<serverName>.cmd`, `restart` →
RestartNow. status получил поле `recovery` (playbook). Верификация e2e: listenerUptime 29 → запись
restart-флага → файл потреблён, listenerUptime сброшен в 1, reloadCount неизменен (рестарт листенера,
не domain reload) = AC2.6. status.recovery = AC2.7. 79/79. Полный F7 discoverability → M3.
Реализация F2/AC2.6-2.7 (lifecycle §4 уже описывает механизм) — raw не менялся.
