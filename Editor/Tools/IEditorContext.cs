namespace Shtl.Mcp.Tools
{
    public interface IEditorContext
    {
        string ProjectName { get; }
        string ProjectPath { get; }
        string UnityVersion { get; }
        string ServerName { get; }
        int Port { get; }
        int Pid { get; }
        bool IsPlaying { get; }
        bool IsCompiling { get; }
        double UptimeSeconds { get; }
        int ClientCount { get; }
    }
}
