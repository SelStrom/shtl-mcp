using System.Runtime.CompilerServices;

// Тестовая сборка видит internal-члены (тест-хуки no-throttle: ClearBackup/HasBackup/DropSessionLayer).
[assembly: InternalsVisibleTo("Shtl.Mcp.Editor.Tests")]
