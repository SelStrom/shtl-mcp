using UnityEngine;

namespace Shtl.Mcp.Editor.Tests
{
    /// Мишень asset-target'а get_object/modify_object (AC3.11): SO с плоским и вложенным полем.
    /// Отдельный файл с именем класса — иначе у созданного CreateAsset'ом ассета битый script-ref.
    public class ShtlM5TestConfig : ScriptableObject
    {
        public int number;
        public Vector3 vec;

        // Ссылочное поле под запись ObjectReference. Только ассет: ссылку на объект сцены
        // Unity в ассет не сериализует — сценовые ссылки проверяются на ShtlObjRefTestBehaviour.
        public Material material;
    }
}
