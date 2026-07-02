# Journal — m5-custom-tools (M5/v2, append-only)

## [2026-07-03] research → выбор → forward-поток → реализация (AC3.6–3.8)

**Research (workflow, 5 агентов: IvanMurzak / CoplayDev / офиц. C# SDK / полевой обзор → синтез).** Итог:
- Весь зрелый C#-Unity-MCP (IvanMurzak `[AiToolType]/[AiTool]`, CoplayDev `[McpForUnityTool]`, официальный
  Unity `com.unity.ai.assistant` `[McpTool]`) сходится на «атрибут на типе + reflection/`TypeCache`-скан
  Editor-сборок, без форка, без ручной регистрации». Ekосистема это подтверждает: у IvanMurzak расширяемость
  провалидирована семейством first-party add-on-пакетов.
- Развилка по схеме: **метод-атрибут + авто-схема из сигнатуры** (IvanMurzak/офиц. SDK — лучшая эргономика,
  автор пишет метод, схема выводится) **vs ручная схема**. Авто-схема требует reflection-генератора
  JSON-Schema + param-биндинга/маршалинга. Официальный SDK берёт это из Microsoft.Extensions.AI/generic-host/
  System.Text.Json — **всё несовместимо с нашим .NET Standard 2.1 / Mono / Newtonsoft / HttpListener**
  (architecture.md прямо отверг SDK по этой причине); IvanMurzak сам катает свой converters-стек на SignalR.
- CoplayDev-шейп ближе к нам (класс-атрибут + `HandleCommand(JObject)` + TypeCache-скан), но их `HandleCommand`
  у нас уже есть в типобезопасном виде — `ITool.Invoke`.

**Выбор — hybrid (approach #3):** сохранить контракт `ITool` (автор пишет `JObject InputSchema` вручную, как
все 35 встроенных) + маркер-атрибут `[McpTool]` + дискавери `TypeCache.GetTypesWithAttribute<McpToolAttribute>`.
Почему лучший ДЛЯ НАС: переиспользует весь конвейер (ITool/ToolRegistry/DispatchingToolInvoker/McpRouter без
изменений), не строит несовместимый со стеком авто-генератор схемы (INV-2, пропорциональность), закрывает drift
(`[McpTool]` в raw был, в коде — нет). Метод-атрибут+авто-схема и DI-контекст — осознанно v2 (аддитивно).

**Forward-поток:** raw F3 (+AC3.6–3.8) + `domain/overview.md` (Tool = контракт `ITool`; `[McpTool]` — механизм
кастомных) → wiki `command-set.md` (§Кастомные инструменты) + `architecture.md` (Tools-сборка) → code.

**Код:** `McpToolAttribute` (маркер класса, Tools) + `ToolDiscovery` (Tools: тестируемое `RegisterFrom` —
валидация ITool/не-abstract/parameterless-ctor/непустой Name/не-занятое-имя, per-type try/catch; live
`DiscoverAndRegister` через TypeCache) + `ToolRegistry.Contains` (приоритет встроенных) + проводка в
`ShtlMcpServer.EnsureStarted` ПОСЛЕ встроенных. Всё в сборке Tools → **DAG цел** (TypeCache легален на
Editor-платформе; никакого Tools→Lifecycle).

**Верификация:**
- Unit `ToolDiscoveryTests` (7): валидный тул; **RED-gate изоляции** (битый ctor не мешает соседу);
  no-parameterless-ctor; пустой Name; не-ITool; встроенный побеждает при коллизии; кастом-vs-кастом (первый
  побеждает). Тест-тулы БЕЗ `[McpTool]` → живой сервер их не подхватывает (без загрязнения). **138/138.**
- **E2E живой дискавери:** пример `[McpTool] GreetTool : ITool` в отдельной Editor-сборке
  `TestProject~/Assets/Editor/HostMcpTools` (реф Shtl.Mcp.Tools+Newtonsoft) → после рекомпиляции `tools/list`
  = 36 (35+greet), ручная схема отдаётся корректно, вызов `greet{name:Unity}` → `{greeting:"Hello, Unity!"}` +
  авто `projectName`/`recoveryHint`. Полное подтверждение: host добавил тул без правок shtl-mcp. Пример оставлен
  как постоянный demo/регрессия.

## [2026-07-03] финальный adversarial-ревью + ремедиация

Workflow (3 измерения × find→refute, 16 агентов): **0 BLOCKER, 0 MAJOR, 8 MINOR** (после верификации; часть —
дубли между измерениями). Исправлено 6 из 6 уникальных:

- **Изоляция геттеров контракта (AC3.7):** try/catch оборачивал только ctor, а `tool.Name`/`Description`/
  `InputSchema` читались вне try → битый геттер пробивал наружу и ронял старт + отравлял `tools/list`. Фикс:
  весь per-candidate body в try; **прогрев контракта** (Name+Description+InputSchema) на обнаружении → битый
  тул пропускается там же, `tools/list` защищён. +тесты: throwing-Name, throwing-InputSchema (сосед выживает).
- **Недетерминизм custom-vs-custom:** `TypeCache`-порядок undefined, а спека обещала «детерминированный».
  Фикс: `DiscoverAndRegister` сортирует по `FullName` (Ordinal) → лексикографически первый побеждает
  детерминированно. wiki/AC-формулировка выровнена.
- **Спам-warnings на bind-retry:** discovery повторялся каждый watchdog-тик (порт занят → EnsureStarted тем же
  инстансом) → «имя занято» на каждый кастомный тул. Фикс: флаг `_customToolsDiscovered` (один раз на инстанс;
  новый инстанс на reload сбросит).
- **Hanging/heavy ctor:** first-party trust → **доком** (McpToolAttribute + wiki: ctor на главном потоке,
  обязан быть дешёвым; таймаута на ctor нет).
- **Мислейбл «(INV-5)»:** INV-5 = самовосстановление (watchdog), не изоляция дискавери. Убран тег в AC3.7+wiki.
- **Тест-пробелы:** добавлены abstract-тул (ветка `IsAbstract`) + throwing-Name/InputSchema.

Верификация: компиляция чистая, `greet` дискаверится (36 тулов), **141/141 EditMode** (+3). Фича подтверждена.
