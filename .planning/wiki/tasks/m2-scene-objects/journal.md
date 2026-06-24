# Journal — m2-scene-objects (T6, append-only)

## [2026-06-24] T6a + T6b реализация + верификация

**T6a** (`SceneObjectTools.cs`): хелпер `SceneObjects` (резолв по пути/имени вкл. неактивные, PathOf,
Describe, обход дерева с бюджетом 2000) + 6 тулов (get_hierarchy, find_gameobject, gameobject_create/
modify/destroy, set_parent). 69/69 (+3). Round-trip на реальной сцене: create parent+child → modify
позиции → find → hierarchy → set_parent в корень (parent==null) → destroy; примитив→MeshFilter.

**T6b** (`SceneEditTools.cs`): хелпер `SerializedValues` (Read/Write int/bool/float/string/enum/Vector/
Color) + 6 тулов (get_object, modify_object, open_scene, save_scene, get_selection, set_selection).
Ловушка хука: `do...while` заблокирован brace-guard (видит `while(...);`) → переписал в обычный `while`
с флагом more. 72/72 (+3): modify BoxCollider.m_IsTrigger → применилось + get_object отражает; missing
component → error; set→get selection (Selection снимается/возвращается).

Всего 30 тулов. Реализация интента F3/AC3.1, AC4.5 — raw не менялся.
