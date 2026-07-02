using System;

namespace Shtl.Mcp.Tools
{
    /// F3/AC3.6: маркер класса-инструмента host-проекта. Класс с `[McpTool]`, реализующий `ITool`,
    /// обнаруживается рефлексией (`TypeCache`) и регистрируется без правок shtl-mcp и без ручного вызова.
    /// Это МАРКЕР КЛАССА (не метод-атрибут): схема пишется вручную как `JObject` в `ITool.InputSchema`
    /// (метод-авто-схема намеренно не делалась — несовместима со стеком, см. wiki/command-set). Требуется
    /// public parameterless-конструктор. ⚠ Конструктор исполняется на ГЛАВНОМ потоке при старте сервера и после
    /// каждого domain reload — он обязан быть дешёвым и без побочных эффектов (без I/O, без циклов); всю работу
    /// делай в `Invoke`. Тяжёлый/зависший ctor застопорит старт сервера (таймаута на ctor нет — только на Invoke).
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class McpToolAttribute : Attribute
    {
    }
}
