using System;

namespace Shtl.Mcp.Registry
{
    public sealed class InstanceEntry
    {
        public string ProjectName;
        public string ProjectPath;
        public string UnityVersion;
        public string ServerName;
        public int Port;
        public int Pid;
        public string Mode;          // "edit" | "play"
        public bool Compiling;
        public DateTime StartedAt;
        public DateTime LastHeartbeat;
        public RecoveryInfo Recovery; // F7/AC7.1: durable самоописываемое восстановление (переживает падение)
    }

    /// Самоописываемый recovery-блок в registry.json — модель читает его при недоступности сервера
    /// (когда MCP-канал мёртв, доставить инструкцию можно только через ФС). F7/AC7.1.
    public sealed class RecoveryInfo
    {
        public string ControlFlagPath; // ~/.unity-mcp/<serverName>.cmd — записать сюда "restart"
        public string RegistryPath;
        public string[] Steps;
        public string RestartCommand;
    }
}
