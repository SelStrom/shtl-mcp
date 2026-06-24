# Journal — m1-reload-survival-test (append-only)

## [2026-06-14] старт | brainstorming → spec

Контекст: ручная reload-survival приёмка M1 упёрлась в потолок наблюдаемости — `status`+`get_logs`
не отличают re-spawn (uptime durable: `ShtlMcpServer.Uptime` от `SessionState` `StartedKey`, ставится
раз за сессию; pid/port не меняются; нет `reloadCount`). Live-проверка показала: play→edit пережит
(mode прошёл edit→play→edit, сервис не прерывался; в проекте `m_EnterPlayModeOptionsEnabled: 0` →
вход в Play = полный domain reload), Restart-кнопка re-spawn'ит листенер, но всё это ненаблюдаемо в
деталях. Вывод: нужен автотест как RED-gate.

Исследование (сабагент, исходники UTF 1.1.33): EditMode `[UnityTest]` штатно переживает domain reload
через `yield return new WaitForDomainReload()` (namespace `UnityEngine.TestTools`, публичный в 1.1.33);
раннер-`ScriptableObject` сериализует program counter итератора → возобновление на той же строке.
Восстанавливается только позиция; локалы теряются; поля fixture выживают лишь `JsonUtility`-сериализуемые
(`[SerializeField]`/public, не static/не property). Источник истины — распакованный пакет UTF 1.1.33.

Решения (через AskUserQuestion):
- Scope тестов: **domain reload (recompile)** + **watchdog re-bind**; play→edit — нет (подмножество).
- Триггер reload: **`RequestScriptReload()` + `WaitForDomainReload()`** (чистый reload без правки файлов).
- Проба «жив»: **полный JSON-RPC `status` round-trip** (проверка `projectName`), не TCP-connect.

Критичная находка при сверке кода: `StatusTool.NeedsMainThread == true` → синхронный HTTP из главного
потока теста = deadlock (та же ловушка, что отмечал журнал M1). Решение: HTTP-проба на фоновом Task,
`[UnityTest]` yield-ит до завершения, чтобы `EditorApplication.update` дренил диспетчер.

Спека: `TASK.md` (этот таск). Аддитивный тест → raw-diff не требуется. Локация кода — `Tests/Editor/`
(asmdef уже EditMode + TestRunner). Дальше: реализация по плану шагов (watchdog-тест первым).

## [2026-06-14] реализация | код тестов

Подключение подтверждено: `TestProject~/Packages/manifest.json` → `com.shtl.mcp: file:../../`,
`testables: ["com.shtl.mcp"]`; `Shtl.Mcp.Editor.Tests.dll` уже собирается. Локальный `file:`-пакет
редактируем — новые файлы в `Tests/Editor/` подхватятся.

Написано:
- `Tests/Editor/McpProbe.cs` — фоновый HTTP JSON-RPC клиент (`CallStatusAsync` через Task,
  `TryGetProjectName` парсит `result.content[0].text`). Запуск только с пула потоков (анти-deadlock).
- `Tests/Editor/ReloadSurvivalTests.cs` — два `[UnityTest]`:
  `Watchdog_Rebinds_AfterListenerDeath` (StopListenerForReload + WatchdogTick → poll) и
  `Listener_Respawns_AfterDomainReload` `[Category("DomainReload")]` (RequestScriptReload +
  WaitForDomainReload → poll; порт/project в `[SerializeField]`-полях, переживают reload).

Саморевью кода до прогона поймало баг: исходно `PollStatus` пампился через `yield return Drive(...)`
— вложенный `yield return IEnumerator`, авто-нестинг которого EditMode-раннер не гарантирует.
Переписано на ручной пампинг на верхнем уровне (`while (e.MoveNext()) { yield return e.Current; }`),
где раннеру отдаётся только `null`/`WaitForDomainReload`. Хук проекта `brace-style-guard` дополнительно
потребовал блочный стиль — применён.

Дальше: верификация компиляции через get_logs(error) после рекомпиляции, затем прогон в Test Runner.

## [2026-06-14] верификация | компиляция + GREEN прогон

Компиляция: `Shtl.Mcp.Editor.Tests.dll` пересобран (02:23:31), в Editor.log оба файла импортированы,
ни одной `error CS` / «Compilation failed». Код собрался.

Прогон в Unity Test Runner (EditMode, пользователь): **оба теста зелёные** —
`Watchdog_Rebinds_AfterListenerDeath` и `Listener_Respawns_AfterDomainReload`.
`get_logs(error)` пуст; сервер пережил форсированный domain reload из теста 1 (соединение
восстановилось, uptime растёт от того же StartedKey, pid неизменный). AC-1, AC-2, AC-4 — выполнены.

Осталось AC-3 (RED-gate): подтвердить, что тесты падают на сломанной версии (иначе тавтология).

## [2026-06-14] RED-gate | совмещённый слом применён

Временно отключены (помечены `RED-GATE TEMP (AC-3) … ОТКАТИТЬ`):
- `ShtlMcpServer.WatchdogTick` — ветка `if (!IsListening) EnsureStarted();` (для теста 2);
- `ShtlMcpBootstrap.Init` — вызов `ShtlMcpServer.Instance.EnsureStarted();` (для теста 1).
`EnsureStarted` не тронут → arrange обоих тестов поднимает листенер сам; краснеют именно на
пост-reload/пост-kill проверке. Ожидание: оба теста RED. После подтверждения — откат + рекомпиляция
(live-сервер оживёт). На время слома live MCP лежит (опора на отчёт Test Runner + Editor.log).

## [2026-06-14] RED-gate | подтверждён + откат

Прогон сломанной версии (пользователь, Test Runner): **оба теста КРАСНЫЕ** — подтверждает, что
тесты ловят отсутствие re-spawn-логики (нетавтологичны). AC-3 выполнен. Editor.log интерактивных
результатов не пишет — источник истины — отчёт Test Runner.

Слом откачен (оба файла вернулись в исходное; `grep RED-GATE TEMP` пуст, строки на местах).
Осталось: рекомпиляция (live-сервер оживает бутстрапом) + финализация (index.md, log.md, статус таска).

## [2026-06-14] финализация | done

Live-сервер восстановлен рекомпиляцией (status ok на 9730, pid неизменный, ошибок нет). Зелёная
версия компилируется чисто. Обновлены: `wiki/index.md` (запись таска), `wiki/log.md` (forward-строка),
статус TASK.md → done. Все AC (1–4) выполнены. Открытый пункт из плана — batch-mode respawn (шаг 7) —
интерактивно не проверялся (тесты гонялись в Test Runner); при выходе на CI прогнать
`-runTests -testPlatform EditMode` и при провале пометить тест 1 `[Explicit]`/категорией.
