using System.Windows;
using MgaRegistryChecker.Presentation;
using MgaRegistryChecker.Services;

namespace MgaRegistryChecker;

public partial class App : Application
{
    private SingleInstanceGuard? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = SingleInstanceGuard.TryAcquire();
        if (_singleInstance is null)
        {
            Shutdown(0);
            return;
        }

        Exit += (_, _) =>
        {
            _singleInstance?.Dispose();
            _singleInstance = null;
        };

        if (AppLaunchArgs.IsCheckOnly(e.Args))
        {
            RunCheckOnly();
            return;
        }

        var main = new MainWindow();
        MainWindow = main;
        _singleInstance.StartActivateListener(() => SingleInstanceGuard.ActivateWindow(MainWindow));
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
            var apply = new DiffApplyService(store);
            var session = new DiffSession(apply, new WpfDiffPresenter());
            var state = store.Load();
            var result = session.Process(
                state,
                [.. state.Locations],
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
