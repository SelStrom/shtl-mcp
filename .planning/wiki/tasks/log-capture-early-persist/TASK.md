# TASK: Ранняя подписка на лог + выживание буфера через domain reload

**Status:** in-review (код+тесты+intent написаны; EditMode-прогон в TestProject~ — на приёмке)
**Привязка:** raw **F4/AC4.11** (новый — reload-survival лог-буфера + ранний захват),
F4/AC4.5 (`get_logs` работает в Play). Поддерживает INV-5 (наблюдаемость восстановления).
Системы: `wiki/systems/lifecycle-and-reload.md` (§1 + захват лога).

## Контекст (зачем)

На живом инстансе воспроизведено: Console редактора показывает записи
(deprecation, «Editor Extensions загружены», StyleSheet-warnings), а `get_logs`
возвращает пустой буфер. Причина — две:
1. **Поздняя подписка.** `Application.logMessageReceivedThreaded += OnLog` жил в
   `ShtlMcpServer.EnsureStarted`, а тот стартует через `delayCall`/`afterAssemblyReload`
   (первый тик update). Логи, эмитнутые до первого тика (старт редактора, окно после
   reload), в буфер не попадали.
2. **Обнуление на reload.** `LogBuffer` был полем экземпляра сервера, а `_instance` —
   статик, гибнущий на domain reload. Новый домен = пустой буфер.

Это контр-интуитивно для использующей модели: она видит непустую Console и ждёт того
же от `get_logs`. Prior art: CoplayDev/unity-mcp читает реальную Console через рефлексию
`UnityEditor.LogEntries` (видит историю, но хрупко — ломается на бампах Unity, напр.
issue #761); мы остаёмся на официальном `logMessageReceivedThreaded`, но закрываем оба
разрыва — ранней подпиской и персистом (стабильно, без internal API).

## Изменение поведения

- Подписка на лог навешивается рано — из `[InitializeOnLoad]`-ctor'а `ShtlMcpBootstrap`
  (`LogCapture.Install`), синхронно на каждой загрузке домена, до подъёма listener'а.
- Буфер (`LogCapture.Buffer`, статик, cap 500) сериализуется в `SessionState`
  (`Shtl.Mcp.LogBuffer`) в `beforeAssemblyReload` и восстанавливается в `Install`.
- Захват — только в главном редакторе (guard `IsAssetImportWorkerProcess` в bootstrap).
- Сервер логами больше не владеет: `get_logs`/`clear_logs` получают `LogCapture.Buffer`.

## Forward-поток (атомарно)

- **raw:** F4/AC4.11 (новый AC).
- **wiki:** `lifecycle-and-reload.md` (§1 SessionState-состояние +буфер логов, подсекция
  «Захват лога», точки верификации).
- **code:** новый `Editor/Lifecycle/LogCapture.cs`; `LogBuffer.Snapshot()`;
  `ShtlMcpServer` (убраны поле `_logs`, подписка, `OnLog`; тулы ← `LogCapture.Buffer`);
  `ShtlMcpBootstrap` (вызов `LogCapture.Install` после worker-guard).
- **tests:** `LogCaptureTests` (round-trip serialize/deserialize; garbage→empty;
  restore-into-buffer совпадает с `Get`).
- **Проверка консистентности:** конфликта с INV-1..5 нет; поддерживает INV-5/AC4.1.

## Acceptance

- AC-1: `get_logs` после рекомпиляции содержит записи, эмитнутые ДО reload.
- AC-2: стартовые логи редактора (до первого тика update) видны в `get_logs`.
- AC-3: `LogCaptureTests` зелёный (round-trip + garbage-guard + restore-equivalence).
- AC-4: захват не активируется в AssetImportWorker (registry-война не воскресает).
- AC-5 (RED-gate): без персиста тест restore-equivalence на пустом снимке отличается от
  исходного `Get`; без ранней подписки live-AC-2 краснеет (стартовые логи отсутствуют).

## Шаги

1. raw+wiki+code+тесты — сделано.
2. Компиляция: `get_logs(error)` чисто после рекомпиляции (в TestProject~).
3. EditMode Test Runner: `LogCaptureTests` + существующие `LogBufferTests`/`GetLogsToolTests` зелёные.
4. Live: перекомпилировать → `get_logs` держит до-reload записи; на свежем старте
   редактора стартовые warnings присутствуют.
5. Финал: index/log, CHANGELOG (Unreleased), статус → done.

## Заметка

Первая рекомпиляция (внедрение правки) буфер НЕ спасёт — `beforeAssemblyReload` на ней
исполняет ещё старый код (без `LogCapture.Persist`). Персист начинает работать со
следующего reload; ранний захват — с первого InitializeOnLoad нового домена.
