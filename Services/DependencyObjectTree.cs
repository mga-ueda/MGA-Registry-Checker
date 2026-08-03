using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace MgaRegistryChecker.Services;

/// <summary>Visual / 非 Visual の両方を安全に辿る親要素取得。</summary>
public static class DependencyObjectTree
{
    public static DependencyObject? GetParent(DependencyObject? current)
    {
        if (current is null)
            return null;

        // VisualTreeHelper.GetParent は Visual / Visual3D 以外で InvalidOperationException になる
        if (current is Visual or Visual3D)
            return VisualTreeHelper.GetParent(current);

        if (current is FrameworkContentElement fce)
            return fce.Parent;

        return LogicalTreeHelper.GetParent(current);
    }
}
