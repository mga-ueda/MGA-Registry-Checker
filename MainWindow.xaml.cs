using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MGA_RegistryChecker.Models;
using MGA_RegistryChecker.Presentation;
using MGA_RegistryChecker.Services;
using MGA_RegistryChecker.ViewModels;

namespace MGA_RegistryChecker;

public partial class MainWindow : Window
{
    private readonly SnapshotStore _store = new();
    private readonly RegistrySnapshotService _registry = new();
    private readonly DiffSession _diffSession;
    private AppState _state = new();
    private readonly ObservableCollection<WatchedLocationItem> _items = [];
    private bool _startupChecked;
    private bool _inputValid;

    private static readonly Brush OkBrush = new SolidColorBrush(Color.FromRgb(0x34, 0xA8, 0x53));
    private static readonly Brush NgBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x30, 0x25));
    private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0xA0, 0xA6));

    public MainWindow()
    {
        InitializeComponent();
        var apply = new DiffApplyService(_registry, _store);
        _diffSession = new DiffSession(_registry, apply, new WpfDiffPresenter());
        Title = UiText.MainWindowTitle(GetAppVersion());
        LocationList.ItemsSource = _items;
        OkBrush.Freeze();
        NgBrush.Freeze();
        MutedBrush.Freeze();
        UpdateSelectionActions();
        DarkTitleBar.Apply(this);

        // 位置復元は表示前に行う（初回は画面中央）
        _state = _store.Load();
        WindowPlacement.Apply(this, _state.MainWindowBounds);
    }

    private static string GetAppVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            var plus = info.IndexOf('+', StringComparison.Ordinal);
            return plus >= 0 ? info[..plus] : info;
        }

        var ver = asm.GetName().Version;
        return ver is null ? "1.0.0" : $"{ver.Major}.{ver.Minor}.{ver.Build}";
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        // コンストラクタで読んだ状態を一覧へ反映（再読込で位置設定を上書きしない）
        RefreshList();
        StatusText.Text = UiText.StatusStateFile(_store.FilePath);
#if DEBUG
        if (SimulateDiffButton is not null)
            SimulateDiffButton.Visibility = Visibility.Visible;
#endif
        ValidateInput();
        UpdateSelectionActions();

        if (_startupChecked)
            return;

        _startupChecked = true;
        Dispatcher.BeginInvoke(CheckAllDifferences, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void Window_OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            PersistMainWindowBounds();
        }
        catch (IOException)
        {
            // 終了時の保存失敗は握りつぶす
        }
        catch (UnauthorizedAccessException)
        {
            // 終了時の保存失敗は握りつぶす
        }
    }

    private void PersistMainWindowBounds()
    {
        _state.MainWindowBounds = WindowPlacement.Capture(this);
        _store.Save(_state);
    }

    private void RefreshList()
    {
        var selectedId = (LocationList.SelectedItem as WatchedLocationItem)?.Id;
        _items.Clear();
        foreach (var loc in _state.Locations.OrderBy(l => l.Path, StringComparer.OrdinalIgnoreCase))
            _items.Add(new WatchedLocationItem(loc));
        EmptyHint.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (selectedId is Guid id)
            LocationList.SelectedItem = _items.FirstOrDefault(i => i.Id == id);

        UpdateSelectionActions();
    }

    private void SaveState()
    {
        _state.MainWindowBounds = WindowPlacement.Capture(this);
        _store.Save(_state);
        RefreshList();
    }

    private void Input_OnChanged(object sender, TextChangedEventArgs e) => ValidateInput();

    private void ValidateInput()
    {
        if (ValidationMessage is null || AddButton is null)
            return;

        var result = WatchInputValidator.Validate(PathBox.Text, ValueBox.Text);
        _inputValid = result.IsOk;
        AddButton.IsEnabled = result.IsOk;

        if (result.IsIdle)
        {
            ValidationMessage.Text = result.Message;
            ValidationMessage.Foreground = MutedBrush;
            AddButton.IsEnabled = false;
            _inputValid = false;
            return;
        }

        ValidationMessage.Text = result.Message;
        ValidationMessage.Foreground = result.IsOk ? OkBrush : NgBrush;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        ValidateInput();
        if (!_inputValid)
        {
            AppDialog.Warning(this, UiText.MsgInputInvalid, UiText.TitleConfirm);
            return;
        }

        var path = RegistryPathHelper.Normalize(PathBox.Text.Trim());
        var watchValue = !string.IsNullOrWhiteSpace(ValueBox.Text);
        string? valueName = watchValue ? ValueBox.Text.Trim() : null;
        var mode = watchValue ? WatchMode.SingleValue : WatchMode.KeyOnly;

        if (_state.Locations.Any(l =>
                string.Equals(l.Path, path, StringComparison.OrdinalIgnoreCase)
                && l.Mode == mode
                && string.Equals(l.ValueName ?? "", valueName ?? "", StringComparison.OrdinalIgnoreCase)))
        {
            AppDialog.Info(this, UiText.MsgAlreadyWatched, UiText.TitleConfirm);
            return;
        }

        try
        {
            StatusText.Text = UiText.StatusCapturing;
            var location = new WatchedLocation
            {
                Path = path,
                Mode = mode,
                ValueName = valueName,
                CapturedAt = DateTime.Now
            };
            location.Keys = _registry.Capture(location);
            _state.Locations.Add(location);
            SaveState();

            StatusText.Text = mode == WatchMode.SingleValue
                ? UiText.StatusAddedSingleValue(path, valueName)
                : UiText.StatusAddedKeyOnly(path, location.Keys.Count);
            PathBox.Clear();
            ValueBox.Clear();
            ValidateInput();
        }
        catch (Exception ex)
        {
            AppDialog.Error(this, UiText.MsgCaptureFailed(ex.Message));
            StatusText.Text = UiText.StatusAddFailed;
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (LocationList.SelectedItem is not WatchedLocationItem item)
            return;

        var result = AppDialog.Confirm(this, UiText.MsgConfirmStopWatch(item.DisplayPath));
        if (result != MessageBoxResult.Yes)
            return;

        var index = _items.IndexOf(item);
        _state.Locations.RemoveAll(l => l.Id == item.Id);
        SaveState();
        StatusText.Text = UiText.StatusRemoved(item.DisplayPath);

        if (_items.Count > 0)
        {
            var nextIndex = Math.Clamp(index, 0, _items.Count - 1);
            LocationList.SelectedItem = _items[nextIndex];
            LocationList.Focus();
            if (LocationList.ItemContainerGenerator.ContainerFromIndex(nextIndex) is ListBoxItem listItem)
                listItem.Focus();
        }
        else
        {
            LocationList.SelectedItem = null;
        }

        UpdateSelectionActions();
    }

    private void Recapture_Click(object sender, RoutedEventArgs e)
    {
        if (LocationList.SelectedItem is not WatchedLocationItem item)
            return;

        var loc = _state.Locations.First(l => l.Id == item.Id);
        try
        {
            loc.Keys = _registry.Capture(loc);
            loc.CapturedAt = DateTime.Now;
            SaveState();
            StatusText.Text = UiText.StatusRecaptured(item.DisplayPath);
        }
        catch (Exception ex)
        {
            AppDialog.Error(this, UiText.MsgRecaptureFailed(ex.Message));
        }
    }

    private void CheckNow_Click(object sender, RoutedEventArgs e)
    {
        if (LocationList.SelectedItem is not WatchedLocationItem item)
            return;

        var loc = _state.Locations.FirstOrDefault(l => l.Id == item.Id);
        if (loc is null)
            return;

        CheckLocations([loc]);
    }

    private void CheckAllDifferences()
    {
        if (_state.Locations.Count == 0)
        {
            StatusText.Text = UiText.StatusNoWatches;
            return;
        }

        CheckLocations(_state.Locations.ToList());
    }

    private void CheckLocations(IReadOnlyList<WatchedLocation> locations)
    {
        _diffSession.Process(
            _state,
            locations,
            ownerWindow: this,
            setStatus: text => StatusText.Text = text,
            silent: false);
        RefreshList();
    }

    private void LocationList_OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateSelectionActions();

    private void UpdateSelectionActions()
    {
        var hasSelection = LocationList?.SelectedItem != null;
        if (CheckNowButton is not null)
            CheckNowButton.IsEnabled = hasSelection;
        if (RecaptureButton is not null)
            RecaptureButton.IsEnabled = hasSelection;
        if (RemoveButton is not null)
            RemoveButton.IsEnabled = hasSelection;
    }

    private void LocationList_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not null)
            return;

        ClearLocationSelection();
        e.Handled = true;
    }

    private void WatchedEmpty_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null)
            return;
        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not null)
            return;

        ClearLocationSelection();
    }

    private void ClearLocationSelection()
    {
        LocationList.SelectedItem = null;
        UpdateSelectionActions();
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            if (Keyboard.FocusedElement is TextBox)
                return;

            if (RemoveButton is { IsEnabled: true })
            {
                Remove_Click(RemoveButton, new RoutedEventArgs());
                e.Handled = true;
            }
        }
    }

    private void SimulateDiff_Click(object sender, RoutedEventArgs e)
    {
#if DEBUG
        var diff = DiffSimulator.CreateRandom(_state.Locations);
        var dlg = new DiffWindow(diff, simulateOnly: true) { Owner = this };
        dlg.ShowDialog();

        StatusText.Text = dlg.Result.Decision switch
        {
            DiffDecision.Accept => UiText.StatusSimAcceptAll(diff.Changes.Count),
            DiffDecision.Revert => UiText.StatusSimRevertAll(diff.Changes.Count),
            DiffDecision.Mixed => UiText.StatusSimMixed(
                dlg.Result.Items.Count(x => x.Action == DiffItemAction.Accept),
                dlg.Result.Items.Count(x => x.Action == DiffItemAction.Revert)),
            _ => UiText.StatusSimCancel(diff.Changes.Count)
        };
#endif
    }
}
