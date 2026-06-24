# TASK: T6 — scene-objects (T6a hierarchy/gameobjects + T6b scene/selection/SerializedObject)

**Status:** done (12 тулов; 72/72 + round-trip на реальной сцене)
**Привязка:** F3/AC3.1 (тулсет), AC4.5 (Play/Edit-корректность). Разбит на T6a+T6b (как предусмотрено планом).

## T6a — иерархия и GameObjects (`SceneObjectTools.cs`)

Общий хелпер `SceneObjects`: резолв по пути (`A/B/C`) или имени (вкл. неактивные через обход
`GetRootGameObjects` + transform-дети), `PathOf`, `Describe`, обход дерева с бюджетом (≤2000 узлов).

| Тул | Что |
|---|---|
| `get_hierarchy` | дерево активной сцены (опц. `root`, `maxDepth`=5), бюджет узлов |
| `find_gameobject` | по точному имени (вкл. неактивные) → path/active/components |
| `gameobject_create` | новый/примитив (cube…quad), опц. parent |
| `gameobject_modify` | name/active/position/rotation/scale ([x,y,z], local) |
| `gameobject_destroy` | удалить объект + детей |
| `set_parent` | переродитель (пусто → корень), worldPositionStays |

## T6b — scene/selection/SerializedObject (`SceneEditTools.cs`)

Хелпер `SerializedValues` (Read/Write общих типов: int/bool/float/string/enum/Vector2-4/Color/objref).

| Тул | Что |
|---|---|
| `get_object` | компоненты + top-level serialized-свойства (≤40/компонент, без m_Script) |
| `modify_object` | запись serialized-свойства (target+component+property+value) |
| `open_scene` | `EditorSceneManager.OpenScene` (Single; несохранённое теряется) |
| `save_scene` | сохранить активную (опц. save-as path) |
| `get_selection` | текущее выделение (пути) |
| `set_selection` | выделение из путей/имён (`targets[]` или `target`) |

## Верификация

- T6a (`SceneObjectToolsTests`, 3): create→modify(pos)→find→hierarchy→set_parent(в корень)→destroy;
  modify несуществующего → error; примитив cube → MeshFilter. ✅
- T6b (`SceneEditToolsTests`, 3): modify_object BoxCollider.m_IsTrigger=true → isTrigger применён +
  get_object отражает; modify несуществующего компонента → error; set→get_selection. Selection
  снимается/возвращается в SetUp/TearDown. ✅
- Полный сьют: **72/72 passed** (66 + 6). 30 тулов в tools/list.

## Долги

- `get_object`/`modify_object` — top-level свойства общих типов; вложенные пути/массивы/objref-запись по
  ссылке — не сделано (расширяемо). INV-3 identity-инъекция — общий долг M2.
