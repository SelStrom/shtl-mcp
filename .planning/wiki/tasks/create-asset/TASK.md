# create-asset — создание бинарных ассетов

- **Статус:** done (ожидает ревью/мержа)
- **raw:** `F3-command-set.md` → AC3.15
- **wiki:** `systems/command-set.md` (строка `create_asset`)
- **code:** `Editor/Tools/AssetTools.cs` (`CreateAssetTool`), регистрация в `Lifecycle/ShtlMcpServer.cs`,
  `Tests/Editor/AssetToolsTests.cs`

## Цель

`write_asset` умеет только текстовые файлы, а материал или `ScriptableObject` руками не собрать —
их надо создавать через `AssetDatabase.CreateAsset`. Пока такого тула нет, модели, которой нужен
новый материал, приходится держать Editor-скрипт в host-проекте: ровно так в PerfectWar появился
`CreateLootGhostPrefabsTool` (генератор плейсхолдеров лута) — он создавал три материала и три
префаба, и всё остальное в нём уже покрывалось MCP.

Продолжение `objref-write`: там закрыли запись ссылок (`ObjectReference`), здесь — создание самого
ассета, на который ссылаются.

## Acceptance

- `Material` создаётся с обязательным `shader` (по имени, `Shader.Find`); нет имени или шейдер
  не найден → внятная ошибка.
- Наследник `ScriptableObject` создаётся через `CreateInstance`.
- Прочие `UnityEngine.Object` — только с конструктором без параметров; иначе ошибка с именем типа.
- Путь обязан быть под `Assets/` и с расширением.
- Существующий путь → ошибка, если не передан `overwrite: true`.
- Поля созданного ассета правятся `modify_object` — тул только создаёт.

## Вне scope

- Типы, требующие аргументов конструктора (`Texture2D`, `RenderTexture`) — размеры и формат
  выходят за «создать пустой ассет».
- Материалы/шейдеры-специфика (keywords, render queue как отдельные параметры): свойства пишутся
  через `modify_object` по сериализованным путям.

## Шаги

1. `CreateAssetTool` в `AssetTools.cs`: валидация пути → проверка существующего → резолв типа через
   `TypeResolve.Find` (база `UnityEngine.Object`) → инстанс (Material / ScriptableObject / ctor) →
   `CreateAsset` + `SaveAssets`.
2. Регистрация в `ShtlMcpServer` рядом с `create_folder`.
3. EditMode-тесты: материал + правка поля через `modify_object`, ScriptableObject, existing/overwrite,
   материал без шейдера, путь без расширения.
