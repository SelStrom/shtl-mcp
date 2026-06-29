using System.Runtime.CompilerServices;

// Тестовая сборка видит internal-члены (тест-хук JobStore.BackdateForTest для orphan-таймаута).
[assembly: InternalsVisibleTo("Shtl.Mcp.Editor.Tests")]
