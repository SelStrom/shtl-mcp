# TASK — m5-custom-tools (M5/v2 — extensibility)

## Цель
Дать host-проекту добавлять свои MCP-инструменты **без форка** shtl-mcp (F3/AC3.6–3.8). До этого реестр был
захардкожен в приватном `ShtlMcpServer.EnsureStarted` — расширение требовало правки пакета.

## Привязка
- Фича/AC: **F3 / AC3.6–3.8** (новые — forward-поток raw→wiki→code). Закрывает drift: `domain/overview.md`
  уже упоминал атрибут `[McpTool]`, которого в коде не было.
- Системы: `command-set.md` (§Кастомные инструменты), `architecture.md` (Tools-сборка).
- Инварианты: INV-2 (чистый C#, без нового subsystem), INV-5 (битый тул не роняет старт), footgun не затронут.

## Решение (research 5 популярных Unity-MCP + офиц. C# SDK → выбор)
Консенсус экосистемы: «атрибут на типе + reflection/TypeCache-скан Editor-сборок, без форка/ручной регистрации».
Развилка — авто-схема из сигнатуры метода (IvanMurzak/SDK, лучшая эргономика) vs ручная схема. Метод-авто-схема
требует reflection-генератора+param-биндинга, **несовместимого с нашим стеком** (.NET Standard/Mono/Newtonsoft;
SDK отвергнут в architecture.md). Выбран **hybrid**: сохранить `ITool` (ручная `JObject`-схема, как у 35
встроенных) + **маркер `[McpTool]`** + `TypeCache`-дискавери. Ноль новой машинерии, переиспользует весь конвейер.

## Реализация
- `McpToolAttribute` (Tools) — маркер класса.
- `ToolDiscovery` (Tools): `RegisterFrom(candidates, registry, onSkip)` (тестируемое ядро — валидация ITool/
  не-abstract/parameterless-ctor/непустой Name/не-занятое-имя, per-type try/catch) + `DiscoverAndRegister`
  (live: `TypeCache.GetTypesWithAttribute<McpTool>`).
- `ToolRegistry.Contains` — для приоритета встроенных при коллизии.
- Проводка: `ShtlMcpServer.EnsureStarted` зовёт `DiscoverAndRegister(_tools)` ПОСЛЕ встроенных.

## Acceptance
- Host кладёт `[McpTool] : ITool` в свою Editor-сборку (реф Shtl.Mcp.Tools+Newtonsoft) → тул в `tools/list`,
  вызывается, с авто-`projectName`/`recoveryHint`. Без правок shtl-mcp.
- Битый тул пропущен + warning, старт не падает; коллизия имени с встроенным → встроенный побеждает.
- DAG цел (всё в Tools; TypeCache — Editor-платформа). Регресс зелёный.

## Статус
✅ Done — реализовано, 7 unit-тестов (RegisterFrom), **e2e живой дискавери подтверждён** (пример `greet` в
TestProject~/Assets/Editor/HostMcpTools → tools/list=36, вызов работает). Метод-атрибут+авто-схема и DI-контекст
— v2 (аддитивно). Финальный adversarial-ревью — см. journal.
