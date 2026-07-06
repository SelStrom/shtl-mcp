using UnityEditor;
using Shtl.Mcp.Tools;

namespace Shtl.Mcp.Lifecycle
{
    [InitializeOnLoad]
    public static class ShtlMcpBootstrap
    {
        static double _lastTick;

        // Подписки на reload-события — в static ctor (InitializeOnLoad): вооружаются на КАЖДОЙ загрузке
        // домена синхронно, как часть reload-последовательности, НЕЗАВИСИМО от фокуса редактора.
        // Баг до этого: afterAssemblyReload подписывался внутри Init, а Init запускался только через
        // delayCall (тик EditorApplication.update). В фоне (окно не в фокусе) update не тикает → после
        // domain reload сервер не переподнимался (chicken-egg: нет листенера → нечем разбудить update).
        // Канонический паттерн Unity: подписка afterAssemblyReload в [InitializeOnLoad]-ctor (INV-5).
        static ShtlMcpBootstrap()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;

            AssemblyReloadEvents.afterAssemblyReload -= Init;
            AssemblyReloadEvents.afterAssemblyReload += Init;

            EditorApplication.delayCall += Init; // первичный старт редактора (+ подстраховка)
        }

        static void Init()
        {
            if (!ShtlMcpConfig.Enabled)
            {
                // Сервер не поднимаем (выключен), но осиротевшие снимки (краш во время прогона) откатываем:
                // EditorPrefs/ProjectSettings durable, иначе редактор останется с форсированными настройками.
                // Прогон не может быть in-flight при выключенном сервере → runPending=false.
                TestRunnerNoThrottle.RecoverOnLoad(false);
                PlayModeOptionsGuard.RecoverOnLoad(false);
                // idle-keepalive пишет No-Throttling БЕЗ бэкапа → RecoverOnLoad его не откатит. При выключенном
                // сервере watchdog (единственный, кто зовёт Reconcile) не подписан → без явного revert редактор
                // застрял бы в No-Throttling на всю сессию. Отпускаем здесь (AC4.10: выключен → троттлинг к Default).
                IdleKeepAlive.Reconcile(false);
                return;
            }

            ShtlMcpServer.Instance.EnsureStarted();

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void OnBeforeReload()
        {
            ShtlMcpServer.Instance.NoteReloadStarting(); // durable reloadCount++ (AC4.7)
            ShtlMcpServer.Instance.StopListenerForReload();
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastTick < ShtlMcpConfig.HeartbeatSeconds)
            {
                return;
            }

            _lastTick = now;
            // Главпоточный watchdog остаётся для main-only задач (job finalize, orphan sweep, keepalive).
            // Ре-бинд listener'а и control-flag теперь дублирует фоновый RecoveryWatchdog (оба идемпотентны),
            // поэтому восстановление больше НЕ зависит от того, тикает ли update.
            ShtlMcpServer.Instance.WatchdogTick();
        }
    }
}
