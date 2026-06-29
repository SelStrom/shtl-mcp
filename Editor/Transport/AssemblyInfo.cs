using System.Runtime.CompilerServices;

// Тестовая сборка видит internal-члены (Host/Origin-фильтр HttpServer.IsRequestAllowed).
[assembly: InternalsVisibleTo("Shtl.Mcp.Editor.Tests")]
