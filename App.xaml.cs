using System.Windows;
using MgaRegistryChecker.Presentation;
using MgaRegistryChecker.Services;

namespace MgaRegistryChecker;

public partial class App : Application
{
    private static readonly TimeSpan UpdateCheckTimeout = TimeSpan.FromSeconds(5);

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

        // メイン表示後に非同期で確認（オフラインでも起動をブロックしない）
        Dispatcher.BeginInvoke(
            async () => await PromptIfUpdateAvailableAsync(main).ConfigureAwait(true),
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// メインウィンドウを出さず差分チェックのみ。差分がなければ何も表示せず終了。
    /// 新バージョンがある場合は差分チェック前にダイアログを出す。
    /// </summary>
    private void RunCheckOnly()
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            PromptIfUpdateAvailableBlocking(ownerWindow: null);

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

    private static void PromptIfUpdateAvailableBlocking(Window? ownerWindow)
    {
        var release = UpdateChecker.TryGetNewerReleaseBlocking(UpdateCheckTimeout);
        OfferUpdate(ownerWindow, release);
    }

    private static async Task PromptIfUpdateAvailableAsync(Window? ownerWindow)
    {
        using var cts = new CancellationTokenSource(UpdateCheckTimeout);
        var release = await UpdateChecker.TryGetNewerReleaseAsync(cts.Token).ConfigureAwait(true);
        OfferUpdate(ownerWindow, release);
    }

    private static void OfferUpdate(Window? ownerWindow, UpdateChecker.ReleaseInfo? release)
    {
        if (release is null)
            return;

        if (ownerWindow is { IsLoaded: false })
            ownerWindow = null;

        var current = AppVersion.GetDisplayVersion();
        var answer = AppDialog.Confirm(
            ownerWindow,
            UiText.MsgUpdateAvailable(current, release.DisplayVersion),
            UiText.TitleUpdate);

        if (answer != MessageBoxResult.Yes)
            return;

        try
        {
            UpdateChecker.OpenReleasePage(release.HtmlUrl);
        }
        catch (Exception ex)
        {
            AppDialog.Error(ownerWindow, UiText.MsgOpenReleasePageFailed(ex.Message));
        }
    }
}
