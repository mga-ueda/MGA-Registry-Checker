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
    /// <summary>この差分が属する監視場所の Id。</summary>
    public required Guid LocationId { get; init; }
    public required DiffChange Change { get; init; }
    public DiffItemAction Action { get; init; }
}

public sealed class DiffDialogResult
{
    public DiffDecision Decision { get; init; } = DiffDecision.Cancel;
    public IReadOnlyList<DiffItemChoice> Items { get; init; } = [];
}
