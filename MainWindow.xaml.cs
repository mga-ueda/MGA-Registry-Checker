using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MgaRegistryChecker.Models;
using MgaRegistryChecker.Presentation;
using MgaRegistryChecker.Services;
using MgaRegistryChecker.ViewModels;

namespace MgaRegistryChecker;

public partial class MainWindow : Window
{
    private readonly SnapshotStore _store = new();
    private readonly DiffSession _diffSession;
    private readonly AppState _state = new();
    private readonly ObservableCollection<WatchedLocationItem> _items = [];
    private bool _startupDiffChecked;
    private bool _inputValid;
    private bool _suppressStartupToggle;

    private static readonly Brush OkBrush = new SolidColorBrush(Color.FromRgb(0x34, 0xA8, 0x53));
    private static readonly Brush NgBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x30, 0x25));
    private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0xA0, 0xA6));

    public MainWindow()
    {
        InitializeComponent();
        _diffSession = new DiffSession(_store, new WpfDiffPresenter());
        Title = UiText.MainWindowTitle(AppVersion.GetDisplayVersion());
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

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        // コンストラクタで読んだ状態を一覧へ反映（再読込で位置設定を上書きしない）
        RefreshList();
        StatusText.Text = UiText.StatusStateFile(_store.FilePath);
        SyncStartupCheckBox();
        ValidateInput();
        UpdateSelectionActions();

        if (_startupDiffChecked)
            return;

        _startupDiffChecked = true;
        Dispatcher.BeginInvoke(CheckAllDifferences, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void SyncStartupCheckBox()
    {
        _suppressStartupToggle = true;
        try
        {
            StartupCheckBox.IsChecked = StartupRegistration.IsEnabled();
        }
        finally
        {
            _suppressStartupToggle = false;
        }
    }

    private void StartupCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressStartupToggle || StartupCheckBox is null)
            return;

        var enable = StartupCheckBox.IsChecked == true;
        try
        {
            StartupRegistration.SetEnabled(enable);
            StatusText.Text = enable
                ? UiText.StatusStartupEnabled
                : UiText.StatusStartupDisabled;
        }
        catch (Exception ex)
        {
            AppDialog.Error(this, UiText.MsgStartupToggleFailed(ex.Message));
            SyncStartupCheckBox();
        }
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
            location.Keys = RegistrySnapshotService.Capture(location);
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
            loc.Keys = RegistrySnapshotService.Capture(loc);
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

        CheckLocations([.. _state.Locations]);
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
        if (DependencyObjectTree.FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not null)
            return;

        ClearLocationSelection();
        e.Handled = true;
    }

    private void WatchedEmpty_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DependencyObjectTree.FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null)
            return;
        if (DependencyObjectTree.FindAncestor<CheckBox>(e.OriginalSource as DependencyObject) is not null)
            return;
        if (DependencyObjectTree.FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not null)
            return;

        ClearLocationSelection();
    }

    private void ClearLocationSelection()
    {
        LocationList.SelectedItem = null;
        UpdateSelectionActions();
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
}
