using System.Windows;
using MgaRegistryChecker.Presentation;
using MgaRegistryChecker.Services;

namespace MgaRegistryChecker;

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
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            var store = new SnapshotStore();
            var registry = new RegistrySnapshotService();
            var apply = new DiffApplyService(registry, store);
            var session = new DiffSession(registry, apply, new WpfDiffPresenter());
            var state = store.Load();
            var result = session.Process(
                state,
                state.Locations.ToList(),
                ownerWindow: null,
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
