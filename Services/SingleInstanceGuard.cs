using System.Threading;
using System.Windows;

namespace MgaRegistryChecker.Services;

/// <summary>同一ユーザーセッション内の二重起動を防ぐ。</summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\MIYABI.MGA.RegistryChecker.SingleInstance";
    private const string ActivateEventName = @"Local\MIYABI.MGA.RegistryChecker.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateEvent;
    private CancellationTokenSource? _listenCts;
    private bool _ownsMutex;

    private SingleInstanceGuard(Mutex mutex, EventWaitHandle activateEvent, bool ownsMutex)
    {
        _mutex = mutex;
        _activateEvent = activateEvent;
        _ownsMutex = ownsMutex;
    }

    /// <summary>
    /// 初回起動ならガードを取得する。既に起動中なら既存プロセスへ前面表示を要求して null を返す。
    /// </summary>
    public static SingleInstanceGuard? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            try
            {
                using var existing = EventWaitHandle.OpenExisting(ActivateEventName);
                existing.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // 終了直後など、イベントがまだ無い場合は無視
            }

            return null;
        }

        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        return new SingleInstanceGuard(mutex, activate, ownsMutex: true);
    }

    /// <summary>他インスタンスからの前面表示要求を待ち、UI スレッドでコールバックする。</summary>
    public void StartActivateListener(Action onActivate)
    {
        ArgumentNullException.ThrowIfNull(onActivate);
        _listenCts?.Cancel();
        _listenCts = new CancellationTokenSource();
        var token = _listenCts.Token;

        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_activateEvent.WaitOne(500))
                    {
                        var dispatcher = Application.Current?.Dispatcher;
                        dispatcher?.BeginInvoke(onActivate);
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (AbandonedMutexException)
                {
                    break;
                }
            }
        }, token);
    }

    public static void ActivateWindow(Window? window)
    {
        if (window is null)
            return;

        if (!window.IsVisible)
            window.Show();

        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    public void Dispose()
    {
        _listenCts?.Cancel();
        _listenCts?.Dispose();
        _listenCts = null;

        _activateEvent.Dispose();

        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // 未所有時は無視
            }

            _ownsMutex = false;
        }

        _mutex.Dispose();
    }
}
