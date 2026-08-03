namespace MgaRegistryChecker.Models;

public enum DiffDecision
{
    Cancel,
    Accept,
    Revert,
    Mixed
}

public enum DiffItemAction
{
    Ignore,
    Accept,
    Revert
}

public sealed class DiffItemChoice
{
    public required DiffChange Change { get; init; }
    public DiffItemAction Action { get; init; }
}

public sealed class DiffDialogResult
{
    public DiffDecision Decision { get; init; } = DiffDecision.Cancel;
    public IReadOnlyList<DiffItemChoice> Items { get; init; } = [];
}
