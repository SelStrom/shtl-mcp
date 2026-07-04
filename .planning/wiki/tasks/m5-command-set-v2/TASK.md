# TASK — m5-command-set-v2 (M5 — murzak-parity для догфудинга)

## Цель
Закрыть gaps тулсета, выявленные при попытке заменить `com.ivanmurzak.unity.mcp` (murzak) на shtl-mcp
в боевом проекте PerfectWar (аудит — `.planning/M5-murzak-parity-prompt.md`). Ключевое: модель не могла
**писать код** через MCP (не было записи текстовых ассетов) и работать с **жизненным циклом компонентов**.

## Привязка
- Фича/AC: **F3 / AC3.9–3.14** (новые — forward-поток raw→wiki→code). Reflection-API поднят из
  «Out of scope (v2+)» — M5 и есть command-set v2.
- Системы: `command-set.md` (Core-таблицы + сокращение §v2).
- Инварианты: INV-1 (write_asset компилируемых → reload-job-канал recompile), философия F3 сохранена
  (только частые операции; материалы/шейдеры/packages/type-schema остались на escape hatches, §6 промпта).

## Решения человека (§7 промпта, зафиксированы до code-diff)
1. `write_asset` и `call_method` — **обычные тулы, не footgun** (запись кода ≠ ad-hoc исполнение;
   вызов существующего метода безопаснее компиляции произвольного кода).
2. Именование — **`write_asset`** (обобщённый, парный к `read_asset`), не узкий `write_script`.
3. Объём — **P0 + P1** (полный паритет для догфудинга).
4. `AllowRunCsharp` в PerfectWar — **не включать**; потребности закрывают целевые тулы.

## Реализация
- **P0** `write_asset` (`WriteAssetTool`, DI JobStore) — запись под `Assets/`; компилируемые
  (`.cs/.asmdef/.asmref`) при `refresh` идут через reload-job-канал recompile (jobId, ошибки компиляции
  в `get_job`), остальные синхронно; `createFolders`; guard'ы (вне Assets, `..`, занятый канал — без
  побочных эффектов).
- **P0** `add_component`/`remove_component` (`ComponentTools` + общий `TypeResolve`) — резолв типа
  (полное имя → уникальное короткое → подсказки), пре-чеки abstract/`DisallowMultipleComponent`/
  `RequireComponent` (кто требует — в ошибке), `index` при дубликатах, Transform неудаляем.
- **P0** расширение `get_object`/`modify_object` (`SceneEditTools` + общий `ObjectRefs`) —
  target: scene-GO / asset-path / instanceId; `modify_object`: bulk `changes[]` + вложенные пути
  (`m_Size.x`) транзакционно (resolve всё → write всё → apply; ошибка = ничего не применено),
  правки ассетов персистятся (`SaveAssetIfDirty`); `get_object`: вложенность до `maxDepth` с бюджетом.
- **P1** `call_method`/`find_method` (`ReflectionTools`) — static/instance/private, перегрузки через
  `parameterTypes`, UnityEngine.Object-аргументы как ref (path/instanceId), структурированные ошибки
  (ambiguous → список сигнатур; исключение метода → тип+сообщение).
- **P1** multi-scene (`MultiSceneTools`) — `list_scenes`/`create_scene`/`unload_scene`/`set_active_scene`;
  ловушка Unity: вторая untitled-сцена запрещена → честная ошибка.
- **P1** `screenshot` + `camera` (имя/путь GO; приоритетнее `view`).
- Регистрация в `ShtlMcpServer.EnsureStarted` (44 встроенных тула).

## Acceptance
- AC3.9: write→read round-trip; компилируемый путь e2e через реальный reload (jobId → `get_job` после
  reload → `recompiled`).
- AC3.10: add/remove с readback реальными компонентами; required/disallow/abstract/index-ошибки.
- AC3.11: bulk+nested транзакционность; asset-target (SO) по path и instanceId; вложенное чтение.
- AC3.12: static/private/instance (instanceId и GO→компонент), перегрузки, ошибки.
- AC3.13: create→set_active→unload lifecycle, ровно одна активная в list_scenes.
- AC3.14: кадр именованной камеры как MCP image-content.

## Статус
✅ Done — 46 новых EditMode-тестов (184/184), включая reload-spanning e2e write_asset.
Верификация — см. journal.
