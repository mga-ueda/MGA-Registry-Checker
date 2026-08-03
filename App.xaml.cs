using System.Windows;
using MGA_RegistryChecker.Services;

namespace MGA_RegistryChecker;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (AppLaunchArgs.IsCheckOnly(e.Args))
        {
            RunCheckOnly();
            return;
        }

        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }

    /// <summary>
    /// メインウィンドウを出さず差分チェックのみ。差分がなければ何も表示せず終了。
    /// </summary>
    private void RunCheckOnly()
    {
        // DiffWindow が閉じたタイミングでアプリが落ちないようにする
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            var store = new SnapshotStore();
            var registry = new RegistrySnapshotService();
            var state = store.Load();
            var processor = new WatchDiffProcessor(registry, store);
            var result = processor.Process(
                state,
                state.Locations.ToList(),
                owner: null,
                setStatus: null,
                silent: true);

            Shutdown(result.HadErrors ? 1 : 0);
        }
        catch (Exception)
        {
            Shutdown(1);
        }
    }
}
