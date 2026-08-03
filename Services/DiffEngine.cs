using MgaRegistryChecker.Models;

namespace MgaRegistryChecker.Services;

public static class DiffEngine
{
    public static List<DiffChange> Compare(
        IReadOnlyList<RegistryKeySnapshot> expected,
        IReadOnlyList<RegistryKeySnapshot> actual)
    {
        var changes = new List<DiffChange>();
        var expectedMap = expected.ToDictionary(k => k.Path, StringComparer.OrdinalIgnoreCase);
        var actualMap = actual.ToDictionary(k => k.Path, StringComparer.OrdinalIgnoreCase);

        foreach (var path in expectedMap.Keys.Except(actualMap.Keys, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new DiffChange
            {
                Kind = DiffChangeKind.KeyRemoved,
                KeyPath = path
            });
        }

        foreach (var path in actualMap.Keys.Except(expectedMap.Keys, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new DiffChange
            {
                Kind = DiffChangeKind.KeyAdded,
                KeyPath = path
            });
        }

        foreach (var path in expectedMap.Keys.Intersect(actualMap.Keys, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            CompareValues(expectedMap[path], actualMap[path], changes);
        }

        return changes;
    }

    private static void CompareValues(RegistryKeySnapshot expected, RegistryKeySnapshot actual, List<DiffChange> changes)
    {
        var exp = expected.Values.ToDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);
        var act = actual.Values.ToDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var name in exp.Keys.Except(act.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var v = exp[name];
            changes.Add(new DiffChange
            {
                Kind = DiffChangeKind.ValueRemoved,
                KeyPath = expected.Path,
                ValueName = name,
                OldValue = RegistryValueDisplay.Format(v.Data, v.Kind),
                OldKind = v.Kind.ToString()
            });
        }

        foreach (var name in act.Keys.Except(exp.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var v = act[name];
            changes.Add(new DiffChange
            {
                Kind = DiffChangeKind.ValueAdded,
                KeyPath = expected.Path,
                ValueName = name,
                NewValue = RegistryValueDisplay.Format(v.Data, v.Kind),
                NewKind = v.Kind.ToString()
            });
        }

        foreach (var name in exp.Keys.Intersect(act.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var oldV = exp[name];
            var newV = act[name];
            if (oldV.Kind == newV.Kind && string.Equals(oldV.Data, newV.Data, StringComparison.Ordinal))
                continue;

            changes.Add(new DiffChange
            {
                Kind = DiffChangeKind.ValueModified,
                KeyPath = expected.Path,
                ValueName = name,
                OldValue = RegistryValueDisplay.Format(oldV.Data, oldV.Kind),
                NewValue = RegistryValueDisplay.Format(newV.Data, newV.Kind),
                OldKind = oldV.Kind.ToString(),
                NewKind = newV.Kind.ToString()
            });
        }
    }
}
