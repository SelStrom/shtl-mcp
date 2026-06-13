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
    }
}
