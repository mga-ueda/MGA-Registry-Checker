using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MgaRegistryChecker.Models;
using MgaRegistryChecker.Services;
using MgaRegistryChecker.ViewModels;

namespace MgaRegistryChecker;

public partial class DiffWindow : Window
{
    public DiffDialogResult Result { get; private set; } = new();

    private readonly Func<DiffDialogResult, bool>? _tryCommit;
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

    public DiffWindow(
        LocationDiff diff,
        Func<DiffDialogResult, bool>? tryCommit = null,
        bool simulateOnly = false)
    {
        InitializeComponent();
        _tryCommit = tryCommit;
        _simulateOnly = simulateOnly;

        // メインの位置に関係なく、常にプライマリディスプレイ中央
        SourceInitialized += (_, _) => WindowPlacement.CenterOnPrimary(this);
        DarkTitleBar.Apply(this);

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

        // Type / ACCEPT / REVERT は見出しとセルから幅を決める
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

        // Value / Old / New は文字幅を測り、切れないようにする
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

        // 残り幅を Key に割り当て（必要なら横スクロール）
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

        // 見出しチェックは通常のトグル（ドラッグ塗り対象外）
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

        // チェック以外のセル上では、座標から行を特定する
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

        // 高速ドラッグで行を飛ばしても、直前〜現在の間をすべて塗る
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
                item.Action = DiffItemAction.Accept;
            else if (item.Action == DiffItemAction.Accept)
                item.Action = DiffItemAction.Ignore;
        }
        else if (_paintColumn == PaintColumn.Revert)
        {
            if (_paintValue)
                item.Action = DiffItemAction.Revert;
            else if (item.Action == DiffItemAction.Revert)
                item.Action = DiffItemAction.Ignore;
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
            && _items.All(i => i.Action is DiffItemAction.Accept or DiffItemAction.Revert);
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
                    item.Action = DiffItemAction.Accept;
            }
            else
            {
                foreach (var item in _items.Where(i => i.Action == DiffItemAction.Accept))
                    item.Action = DiffItemAction.Ignore;
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
                    item.Action = DiffItemAction.Revert;
            }
            else
            {
                foreach (var item in _items.Where(i => i.Action == DiffItemAction.Revert))
                    item.Action = DiffItemAction.Ignore;
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
        if (!_items.All(i => i.Action is DiffItemAction.Accept or DiffItemAction.Revert))
            return;

        var distinct = _items.Select(i => i.Action).Distinct().ToList();
        var decision = distinct.Count == 1
            ? distinct[0] switch
            {
                DiffItemAction.Accept => DiffDecision.Accept,
                DiffItemAction.Revert => DiffDecision.Revert,
                _ => DiffDecision.Cancel
            }
            : DiffDecision.Mixed;

        Finish(decision);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = new DiffDialogResult { Decision = DiffDecision.Cancel };
        DialogResult = false;
        Close();
    }

    private void Finish(DiffDecision decision)
    {
        var dialogResult = new DiffDialogResult
        {
            Decision = decision,
            Items = _items.Select(i => new DiffItemChoice
            {
                Change = i.Change,
                Action = i.Action
            }).ToList()
        };

        if (_simulateOnly)
        {
            Result = dialogResult;
            DialogResult = true;
            Close();
            return;
        }

        if (_tryCommit is not null && !_tryCommit(dialogResult))
            return;

        Result = dialogResult;
        DialogResult = true;
        Close();
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Result = new DiffDialogResult { Decision = DiffDecision.Cancel };
            DialogResult = false;
            Close();
            e.Handled = true;
        }
    }
}

