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
            => Preferred(projectPath, Base, Range);

        public static int Preferred(string projectPath, int rangeStart, int rangeCount)
            => rangeStart + (int)(Fnv.Hash32(projectPath) % (uint)rangeCount);

        /// Пробуем preferred, затем по кругу диапазона до первого свободного.
        public static int Allocate(string projectPath, Func<int, bool> isFree)
            => Allocate(projectPath, isFree, Base, Range);

        public static int Allocate(string projectPath, Func<int, bool> isFree, int rangeStart, int rangeCount)
        {
            int start = Preferred(projectPath, rangeStart, rangeCount);
            for (int i = 0; i < rangeCount; i++)
            {
                int port = rangeStart + (((start - rangeStart) + i) % rangeCount);
                if (isFree(port))
                {
                    return port;
                }
            }
            throw new InvalidOperationException("No free port in range");
        }
    }
}
