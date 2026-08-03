using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MGA_RegistryChecker.Models;
using MGA_RegistryChecker.Services;

namespace MGA_RegistryChecker;

public partial class DiffWindow : Window
{
    public enum DiffDecision
    {
        Cancel,
        Accept,
        Revert,
        Mixed
    }

    public enum ItemAction
    {
        Ignore,
        Accept,
        Revert
    }

    public DiffDecision Decision { get; private set; } = DiffDecision.Cancel;
    public IReadOnlyList<(DiffChange Change, ItemAction Action)> ItemResults { get; private set; } = [];

    private readonly LocationDiff _diff;
    private readonly RegistrySnapshotService _registry = new();
    private readonly bool _simulateOnly;
    private readonly ObservableCollection<DiffItemVm> _items = [];
    private bool _updatingHeaders;

    private enum PaintColumn
    {
        None,
        Accept,
        Revert
    }

    private PaintColumn _paintColumn = PaintColumn.None;
    private bool _paintValue;
    private int _lastPaintedIndex = -1;

    public DiffWindow(LocationDiff diff, bool simulateOnly = false)
    {
        InitializeComponent();
        _diff = diff;
        _simulateOnly = simulateOnly;

        foreach (var change in diff.Changes)
        {
            var item = new DiffItemVm(change);
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(DiffItemVm.Action)
                    or nameof(DiffItemVm.IsAccept)
                    or nameof(DiffItemVm.IsRevert))
                    UpdateApplyEnabled();
            };
            _items.Add(item);
        }

        ChangeGrid.ItemsSource = _items;
        UpdateApplyEnabled();

        if (_simulateOnly)
        {
            Title = UiText.TitleDiffSimulation;
            TitleText.Text = UiText.DiffDetectedSim(diff.Location.Path);
            SubText.Text = UiText.DiffSubTextSim(diff.Changes.Count);
        }
        else
        {
            Title = UiText.TitleDiff;
            TitleText.Text = UiText.DiffDetected(diff.Location.Path);
            SubText.Text = UiText.DiffSubText(diff.Changes.Count);
        }
    }

    private void ChangeGrid_OnLoaded(object sender, RoutedEventArgs e)
    {
        OptimizeColumnWidths();
    }

    private void OptimizeColumnWidths()
    {
        ChangeGrid.UpdateLayout();

        // Type / ACCEPT / REVERT は見出しとセルから
        foreach (var column in new DataGridColumn[] { TypeColumn, AcceptColumn, RevertColumn })
        {
            column.Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader);
            ChangeGrid.UpdateLayout();
            var headerWidth = column.ActualWidth;

            column.Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
            ChangeGrid.UpdateLayout();
            var cellsWidth = column.ActualWidth;

            var width = Math.Max(headerWidth, cellsWidth);
            if (!double.IsNaN(column.MinWidth) && column.MinWidth > 0)
                width = Math.Max(width, column.MinWidth);

            column.Width = new DataGridLength(Math.Ceiling(width));
        }

        // Value / Old / New は全行の文字幅を測って切れないようにする
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var typeface = new Typeface(ChangeGrid.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        const double cellPadding = 20;

        ValueColumn.Width = new DataGridLength(Math.Ceiling(Math.Max(
            MeasureHeader(UiText.ColumnValue, dpi),
            Math.Max(ValueColumn.MinWidth, MaxContentWidth(i => i.ValueName, typeface, dpi) + cellPadding))));

        OldColumn.Width = new DataGridLength(Math.Ceiling(Math.Max(
            MeasureHeader(UiText.ColumnOld, dpi),
            Math.Max(OldColumn.MinWidth, MaxContentWidth(i => i.OldText, typeface, dpi) + cellPadding))));

        NewColumn.Width = new DataGridLength(Math.Ceiling(Math.Max(
            MeasureHeader(UiText.ColumnNew, dpi),
            Math.Max(NewColumn.MinWidth, MaxContentWidth(i => i.NewText, typeface, dpi) + cellPadding))));

        // 余った幅は Key に融通（足りなければ横スクロール）
        KeyColumn.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
    }

    private double MaxContentWidth(Func<DiffItemVm, string> selector, Typeface typeface, double dpi)
    {
        var max = 0.0;
        foreach (var item in _items)
        {
            var text = selector(item) ?? "";
            if (text.Length == 0)
                continue;
            max = Math.Max(max, MeasureText(text, typeface, dpi));
        }

        return max;
    }

    private double MeasureHeader(string header, double dpi)
    {
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        return MeasureText(header, typeface, dpi) + 24;
    }

    private static double MeasureText(string text, Typeface typeface, double dpi)
    {
        var ft = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            12,
            Brushes.Black,
            dpi);
        return ft.WidthIncludingTrailingWhitespace;
    }

    private void ChangeGrid_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindTaggedCheckBox(e.OriginalSource as DependencyObject) is not { } hit)
            return;

        // 見出しチェックは従来どおり（ドラッグ塗り対象外）
        if (hit.CheckBox == AcceptHeaderCheck || hit.CheckBox == RevertHeaderCheck)
            return;

        if (hit.CheckBox.DataContext is not DiffItemVm item)
            return;

        _paintColumn = hit.Column;
        _paintValue = hit.Column switch
        {
            PaintColumn.Accept => !item.IsAccept,
            PaintColumn.Revert => !item.IsRevert,
            _ => false
        };
        _lastPaintedIndex = -1;
        PaintThrough(item);
        ChangeGrid.CaptureMouse();
        e.Handled = true;
    }

    private void ChangeGrid_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_paintColumn == PaintColumn.None || e.LeftButton != MouseButtonState.Pressed)
            return;

        if (FindTaggedCheckBox(e.OriginalSource as DependencyObject) is { } hit
            && hit.Column == _paintColumn
            && hit.CheckBox.DataContext is DiffItemVm taggedItem)
        {
            PaintThrough(taggedItem);
            return;
        }

        // キャプチャ中は OriginalSource がグリッド側になることがあるため、座標から行を拾う
        if (FindRowItemUnder(e.GetPosition(ChangeGrid)) is { } rowItem)
            PaintThrough(rowItem);
    }

    private void ChangeGrid_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_paintColumn == PaintColumn.None)
            return;

        EndPaint();
        e.Handled = true;
    }

    private void ChangeGrid_OnLostMouseCapture(object sender, MouseEventArgs e) => EndPaint();

    private void PaintThrough(DiffItemVm item)
    {
        if (_paintColumn == PaintColumn.None)
            return;

        var index = _items.IndexOf(item);
        if (index < 0)
            return;

        if (_lastPaintedIndex < 0)
        {
            ApplyPaintAt(index);
            _lastPaintedIndex = index;
            return;
        }

        // 高速ドラッグで行を飛ばしても、直前行〜現在行の間を塗る
        var from = Math.Min(_lastPaintedIndex, index);
        var to = Math.Max(_lastPaintedIndex, index);
        for (var i = from; i <= to; i++)
            ApplyPaintAt(i);
        _lastPaintedIndex = index;
    }

    private void ApplyPaintAt(int index)
    {
        var item = _items[index];
        if (_paintColumn == PaintColumn.Accept)
        {
            if (_paintValue)
                item.Action = ItemAction.Accept;
            else if (item.Action == ItemAction.Accept)
                item.Action = ItemAction.Ignore;
        }
        else if (_paintColumn == PaintColumn.Revert)
        {
            if (_paintValue)
                item.Action = ItemAction.Revert;
            else if (item.Action == ItemAction.Revert)
                item.Action = ItemAction.Ignore;
        }
    }

    private void EndPaint()
    {
        if (_paintColumn == PaintColumn.None)
            return;

        _paintColumn = PaintColumn.None;
        _lastPaintedIndex = -1;
        if (ChangeGrid.IsMouseCaptured)
            ChangeGrid.ReleaseMouseCapture();
        SyncHeaderChecks();
        UpdateApplyEnabled();
    }

    private void SyncHeaderChecks()
    {
        _updatingHeaders = true;
        try
        {
            AcceptHeaderCheck.IsChecked = _items.Count > 0 && _items.All(i => i.IsAccept);
            RevertHeaderCheck.IsChecked = _items.Count > 0 && _items.All(i => i.IsRevert);
        }
        finally
        {
            _updatingHeaders = false;
        }
    }

    private void UpdateApplyEnabled()
    {
        ApplyButton.IsEnabled = _items.Count > 0
            && _items.All(i => i.Action is ItemAction.Accept or ItemAction.Revert);
    }

    private DiffItemVm? FindRowItemUnder(Point position)
    {
        var result = VisualTreeHelper.HitTest(ChangeGrid, position);
        for (var current = result?.VisualHit as DependencyObject;
             current != null;
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is DataGridRow row && row.Item is DiffItemVm item)
                return item;
        }

        return null;
    }

    private static (CheckBox CheckBox, PaintColumn Column)? FindTaggedCheckBox(DependencyObject? source)
    {
        for (var current = source; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is not CheckBox cb)
                continue;

            var tag = cb.Tag as string;
            if (tag == "Accept")
                return (cb, PaintColumn.Accept);
            if (tag == "Revert")
                return (cb, PaintColumn.Revert);
        }

        return null;
    }

    private void AcceptHeader_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingHeaders || sender is not CheckBox cb)
            return;

        _updatingHeaders = true;
        try
        {
            if (cb.IsChecked == true)
            {
                RevertHeaderCheck.IsChecked = false;
                foreach (var item in _items)
                    item.Action = ItemAction.Accept;
            }
            else
            {
                foreach (var item in _items.Where(i => i.Action == ItemAction.Accept))
                    item.Action = ItemAction.Ignore;
            }
        }
        finally
        {
            _updatingHeaders = false;
            UpdateApplyEnabled();
        }
    }

    private void RevertHeader_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingHeaders || sender is not CheckBox cb)
            return;

        _updatingHeaders = true;
        try
        {
            if (cb.IsChecked == true)
            {
                AcceptHeaderCheck.IsChecked = false;
                foreach (var item in _items)
                    item.Action = ItemAction.Revert;
            }
            else
            {
                foreach (var item in _items.Where(i => i.Action == ItemAction.Revert))
                    item.Action = ItemAction.Ignore;
            }
        }
        finally
        {
            _updatingHeaders = false;
            UpdateApplyEnabled();
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!_items.All(i => i.Action is ItemAction.Accept or ItemAction.Revert))
            return;

        var distinct = _items.Select(i => i.Action).Distinct().ToList();
        var decision = distinct.Count == 1
            ? distinct[0] switch
            {
                ItemAction.Accept => DiffDecision.Accept,
                ItemAction.Revert => DiffDecision.Revert,
                _ => DiffDecision.Cancel
            }
            : DiffDecision.Mixed;

        Finish(decision);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Decision = DiffDecision.Cancel;
        ItemResults = [];
        DialogResult = false;
        Close();
    }

    private void Finish(DiffDecision decision)
    {
        ItemResults = _items.Select(i => (i.Change, i.Action)).ToList();

        if (_simulateOnly)
        {
            Decision = decision;
            DialogResult = true;
            Close();
            return;
        }

        try
        {
            if (decision == DiffDecision.Revert)
            {
                _registry.Revert(_diff.Location);
            }
            else if (decision == DiffDecision.Mixed)
            {
                var toRevert = ItemResults
                    .Where(x => x.Action == ItemAction.Revert)
                    .Select(x => x.Change)
                    .ToList();
                if (toRevert.Count > 0)
                    _registry.RevertChanges(_diff.Location, toRevert);
            }

            Decision = decision;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            AppDialog.Error(this, UiText.MsgRestoreFailed(ex.Message), UiText.TitleRestoreError);
        }
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Decision = DiffDecision.Cancel;
            DialogResult = false;
            Close();
            e.Handled = true;
        }
    }

    public sealed class DiffItemVm : INotifyPropertyChanged
    {
        private ItemAction _action = ItemAction.Ignore;
        private bool _updatingChecks;

        public DiffItemVm(DiffChange change)
        {
            Change = change;
            KindLabel = change.Kind switch
            {
                DiffChangeKind.KeyAdded => UiText.KindKeyAdded,
                DiffChangeKind.KeyRemoved => UiText.KindKeyRemoved,
                DiffChangeKind.ValueAdded => UiText.KindValueAdded,
                DiffChangeKind.ValueRemoved => UiText.KindValueRemoved,
                DiffChangeKind.ValueModified => UiText.KindValueModified,
                _ => UiText.KindUnknown
            };
            KindBrush = change.Kind switch
            {
                DiffChangeKind.KeyAdded or DiffChangeKind.ValueAdded =>
                    new SolidColorBrush(Color.FromRgb(0x2E, 0xC4, 0xB6)),
                DiffChangeKind.KeyRemoved or DiffChangeKind.ValueRemoved =>
                    new SolidColorBrush(Color.FromRgb(0xD9, 0x30, 0x25)),
                DiffChangeKind.ValueModified =>
                    new SolidColorBrush(Color.FromRgb(0xE6, 0xA2, 0x3C)),
                _ => Brushes.White
            };
            KindBrush.Freeze();
            KeyPath = change.KeyPath;
            ValueName = change.Kind is DiffChangeKind.KeyAdded or DiffChangeKind.KeyRemoved
                ? ""
                : DisplayName(change.ValueName);
            OldText = change.OldValue ?? "";
            NewText = change.NewValue ?? "";
        }

        public DiffChange Change { get; }
        public string KindLabel { get; }
        public Brush KindBrush { get; }
        public string KeyPath { get; }
        public string ValueName { get; }
        public string OldText { get; }
        public string NewText { get; }

        public ItemAction Action
        {
            get => _action;
            set
            {
                if (_action == value)
                    return;
                _action = value;
                OnPropertyChanged();
                if (_updatingChecks)
                    return;
                _updatingChecks = true;
                OnPropertyChanged(nameof(IsAccept));
                OnPropertyChanged(nameof(IsRevert));
                _updatingChecks = false;
            }
        }

        public bool IsAccept
        {
            get => Action == ItemAction.Accept;
            set
            {
                if (_updatingChecks)
                    return;
                Action = value ? ItemAction.Accept : ItemAction.Ignore;
            }
        }

        public bool IsRevert
        {
            get => Action == ItemAction.Revert;
            set
            {
                if (_updatingChecks)
                    return;
                Action = value ? ItemAction.Revert : ItemAction.Ignore;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private static string DisplayName(string? name) =>
            UiText.DisplayValueLabel(name);
    }
}
