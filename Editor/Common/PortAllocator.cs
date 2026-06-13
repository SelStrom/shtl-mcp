using System;

namespace Shtl.Mcp.Common
{
    /// Детерминированный аллокатор портов: вычисляет предпочтительный порт из пути проекта
    /// через FNV-1a; при коллизии обходит диапазон по кругу до первого свободного.
    public static class PortAllocator
    {
        public const int Base = 9700;
        public const int Range = 100;

        public static int Preferred(string projectPath)
            => Base + (int)(Fnv.Hash32(projectPath) % (uint)Range);

        /// Пробуем preferred, затем по кругу диапазона до первого свободного.
        public static int Allocate(string projectPath, Func<int, bool> isFree)
        {
            int start = Preferred(projectPath);
            for (int i = 0; i < Range; i++)
            {
                int port = Base + (((start - Base) + i) % Range);
                if (isFree(port))
                {
                    return port;
                }
            }
            throw new InvalidOperationException("No free port in range");
        }
    }
}
