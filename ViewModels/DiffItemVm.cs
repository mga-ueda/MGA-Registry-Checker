using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using MgaRegistryChecker.Models;

namespace MgaRegistryChecker.ViewModels;

public sealed class DiffItemVm : INotifyPropertyChanged
{
    private DiffItemAction _action = DiffItemAction.Ignore;
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
            : UiText.DisplayValueLabel(change.ValueName);
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

    public DiffItemAction Action
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
        get => Action == DiffItemAction.Accept;
        set
        {
            if (_updatingChecks)
                return;
            Action = value ? DiffItemAction.Accept : DiffItemAction.Ignore;
        }
    }

    public bool IsRevert
    {
        get => Action == DiffItemAction.Revert;
        set
        {
            if (_updatingChecks)
                return;
            Action = value ? DiffItemAction.Revert : DiffItemAction.Ignore;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
