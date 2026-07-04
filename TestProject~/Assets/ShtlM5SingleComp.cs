using UnityEngine;

/// Фикстура пакетных ComponentToolsTests (add_component, пре-чек DisallowMultipleComponent):
/// нужен runtime-компонент с managed-атрибутом — у built-in (Rigidbody и т.п.) единственность
/// enforce'ится нативно, а MonoBehaviour из Editor-сборки компонентом не добавляется.
[DisallowMultipleComponent]
public sealed class ShtlM5SingleComp : MonoBehaviour
{
}
