using UnityEngine;

namespace Shtl.Mcp.Editor.Tests
{
    /// Мишень abstract-guard'а add_component: built-in базовые классы (Collider, Renderer)
    /// в C#-API НЕ abstract — Unity отказывает нативно, guard на них не срабатывает.
    public abstract class ShtlM5AbstractComp : MonoBehaviour
    {
    }
}
