using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MGA_RegistryChecker.Models;
using MGA_RegistryChecker.Services;

namespace MGA_RegistryChecker;

public partial class MainWindow : Window
{
    private readonly SnapshotStore _store = new();
    private readonly RegistrySnapshotService _registry = new();
    private AppState _state = new();
    private readonly ObservableCollection<LocationItem> _items = [];
    private bool _startupChecked;
    private bool _inputValid;

    private static readonly Brush OkBrush = new SolidColorBrush(Color.FromRgb(0x34, 0xA8, 0x53));
    private static readonly Brush NgBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x30, 0x25));
    private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0xA0, 0xA6));

    public MainWindow()
    {
        InitializeComponent();
        Title = UiText.MainWindowTitle(GetAppVersion());
        LocationList.ItemsSource = _items;
        OkBrush.Freeze();
        NgBrush.Freeze();
        MutedBrush.Freeze();
        UpdateSelectionActions();
    }

    private static string GetAppVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            // ビルドメタデータ (+hash 等) があれば除去
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }

        var ver = asm.GetName().Version;
        return ver is null ? "1.0.0" : $"{ver.Major}.{ver.Minor}.{ver.Build}";
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadState();
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

    private void LoadState()
    {
        _state = _store.Load();
        RefreshList();
        StatusText.Text = UiText.StatusStateFile(_store.FilePath);
    }

    private void RefreshList()
    {
        var selectedId = (LocationList.SelectedItem as LocationItem)?.Id;
        _items.Clear();
        foreach (var loc in _state.Locations.OrderBy(l => l.Path, StringComparer.OrdinalIgnoreCase))
            _items.Add(new LocationItem(loc));
        EmptyHint.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (selectedId is Guid id)
            LocationList.SelectedItem = _items.FirstOrDefault(i => i.Id == id);

        UpdateSelectionActions();
    }

    private void SaveState()
    {
        _store.Save(_state);
        RefreshList();
    }

    private void Input_OnChanged(object sender, TextChangedEventArgs e) => ValidateInput();

    private void ValidateInput()
    {
        if (ValidationMessage is null || AddButton is null)
            return;

        var path = PathBox.Text.Trim();
        var valueText = ValueBox.Text;
        var watchValue = !string.IsNullOrWhiteSpace(valueText);
        string? valueName = watchValue ? valueText.Trim() : null;

        var result = RegistryPathHelper.Validate(path, valueName);
        _inputValid = result.IsOk;
        AddButton.IsEnabled = result.IsOk;

        if (string.IsNullOrWhiteSpace(path) && !watchValue)
        {
            ValidationMessage.Text = UiText.ValidationHintIdle;
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

            var label = mode == WatchMode.SingleValue
                ? $"{path} → {valueName}"
                : $"{path}（{location.Keys.Count} キー）";
            StatusText.Text = UiText.StatusAdded(label);
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
        if (LocationList.SelectedItem is not LocationItem item)
            return;

        var result = AppDialog.Confirm(this, UiText.MsgConfirmStopWatch(item.DisplayPath));
        if (result != MessageBoxResult.Yes)
            return;

        var index = _items.IndexOf(item);
        _state.Locations.RemoveAll(l => l.Id == item.Id);
        SaveState();
        StatusText.Text = UiText.StatusRemoved(item.DisplayPath);

        // 次の行（末尾なら前の行）を選択し、続けて Del できるようにする
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
        if (LocationList.SelectedItem is not LocationItem item)
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
        if (LocationList.SelectedItem is not LocationItem item)
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
        var anyDiff = false;
        foreach (var loc in locations)
        {
            LocationDiff diff;
            try
            {
                StatusText.Text = UiText.StatusChecking(loc.Path);
                diff = _registry.Compare(loc);
            }
            catch (Exception ex)
            {
                AppDialog.Error(this, UiText.MsgCompareFailed(loc.Path, ex.Message));
                continue;
            }

            if (diff.Changes.Count == 0)
                continue;

            anyDiff = true;
            var dlg = new DiffWindow(diff) { Owner = this };
            dlg.ShowDialog();

            switch (dlg.Decision)
            {
                case DiffWindow.DiffDecision.Accept:
                    loc.Keys = diff.CurrentSnapshot;
                    loc.CapturedAt = DateTime.Now;
                    SaveState();
                    StatusText.Text = UiText.StatusAccepted(loc.Path);
                    break;
                case DiffWindow.DiffDecision.Revert:
                    try
                    {
                        loc.Keys = _registry.Capture(loc);
                        loc.CapturedAt = DateTime.Now;
                        SaveState();
                        StatusText.Text = UiText.StatusReverted(loc.Path);
                    }
                    catch (Exception ex)
                    {
                        AppDialog.Warning(this, UiText.MsgRecaptureAfterRevertFailed(ex.Message));
                    }
                    break;
                case DiffWindow.DiffDecision.Mixed:
                    try
                    {
                        var accepted = dlg.ItemResults
                            .Where(x => x.Action == DiffWindow.ItemAction.Accept)
                            .Select(x => x.Change)
                            .ToList();
                        RegistrySnapshotService.AcceptChangesIntoSnapshot(loc, diff, accepted);
                        SaveState();
                        StatusText.Text = UiText.StatusMixedApplied(
                            loc.Path,
                            accepted.Count,
                            dlg.ItemResults.Count(x => x.Action == DiffWindow.ItemAction.Revert));
                    }
                    catch (Exception ex)
                    {
                        AppDialog.Warning(this, UiText.MsgMixedApplyFailed(ex.Message));
                    }
                    break;
                default:
                    StatusText.Text = UiText.StatusSkipped(loc.Path);
                    break;
            }
        }

        if (!anyDiff)
            StatusText.Text = UiText.StatusNoDifferences;
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
            // 入力欄での文字削除は通常どおり
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

        StatusText.Text = dlg.Decision switch
        {
            DiffWindow.DiffDecision.Accept => UiText.StatusSimAcceptAll(diff.Changes.Count),
            DiffWindow.DiffDecision.Revert => UiText.StatusSimRevertAll(diff.Changes.Count),
            DiffWindow.DiffDecision.Mixed => UiText.StatusSimMixed(
                dlg.ItemResults.Count(x => x.Action == DiffWindow.ItemAction.Accept),
                dlg.ItemResults.Count(x => x.Action == DiffWindow.ItemAction.Revert)),
            _ => UiText.StatusSimCancel(diff.Changes.Count)
        };
#endif
    }

    private sealed class LocationItem
    {
        public LocationItem(WatchedLocation location)
        {
            Id = location.Id;
            DisplayPath = location.Mode == WatchMode.SingleValue
                ? UiText.SingleValueDisplayPath(location.Path, location.ValueName)
                : location.Path;
            ModeLabel = location.Mode switch
            {
                WatchMode.Recursive => UiText.ModeRecursive,
                WatchMode.KeyOnly => UiText.ModeKeyOnly,
                WatchMode.SingleValue => UiText.ModeSingleValue,
                _ => location.Mode.ToString()
            };
            KeyCount = location.Mode == WatchMode.SingleValue
                ? UiText.CountOneValue
                : UiText.CountKeys(location.Keys.Count);
            CapturedAtText = location.CapturedAt.ToString("yyyy/MM/dd HH:mm");
        }

        public Guid Id { get; }
        public string DisplayPath { get; }
        public string ModeLabel { get; }
        public string KeyCount { get; }
        public string CapturedAtText { get; }
    }
}
