using UnityEditor;

namespace ShtlMcp.Lifecycle
{
    [InitializeOnLoad]
    public static class ShtlMcpBootstrap
    {
        static double _lastTick;

        static ShtlMcpBootstrap() => EditorApplication.delayCall += Init;

        static void Init()
        {
            if (!ShtlMcpConfig.Enabled)
            {
                return;
            }

            ShtlMcpServer.Instance.EnsureStarted();

            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;

            AssemblyReloadEvents.afterAssemblyReload -= Init;
            AssemblyReloadEvents.afterAssemblyReload += Init;

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void OnBeforeReload() => ShtlMcpServer.Instance.StopListenerForReload();

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastTick < 1.0)
            {
                return;
            }

            _lastTick = now;
            ShtlMcpServer.Instance.WatchdogTick();
        }
    }
}
