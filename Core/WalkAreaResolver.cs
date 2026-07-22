using System.Windows;

namespace DesktopPet.Core;

/// <summary>
/// Resolves <see cref="WalkAreaConfig"/> into a screen rectangle for autonomous movement.
/// </summary>
public static class WalkAreaResolver
{
    public static Rect Resolve(WalkAreaConfig config, Rect? workArea = null)
    {
        var work = workArea ?? SystemParameters.WorkArea;

        return config.Mode switch
        {
            WalkAreaMode.WorkArea => work,
            WalkAreaMode.CustomRect => ResolveCustom(config, work),
            _ => Inset(work, config.MarginLeft, config.MarginTop, config.MarginRight, config.MarginBottom),
        };
    }

    /// <summary>
    /// Picks a random window top-left so the window stays fully inside <paramref name="area"/>.
    /// </summary>
    public static System.Windows.Point PickRandomTopLeft(
        Rect area,
        System.Windows.Size windowSize,
        Random rng)
    {
        var maxLeft = area.Right - windowSize.Width;
        var maxTop = area.Bottom - windowSize.Height;

        if (maxLeft < area.Left || maxTop < area.Top)
        {
            // Window larger than area: pin to bottom-left of area as best effort.
            return new System.Windows.Point(area.Left, area.Bottom - windowSize.Height);
        }

        var left = area.Left + rng.NextDouble() * (maxLeft - area.Left);
        var top = area.Top + rng.NextDouble() * (maxTop - area.Top);
        return new System.Windows.Point(left, top);
    }

    public static void ClampTopLeft(
        ref double left,
        ref double top,
        System.Windows.Size windowSize,
        Rect area)
    {
        var maxLeft = Math.Max(area.Left, area.Right - windowSize.Width);
        var maxTop = Math.Max(area.Top, area.Bottom - windowSize.Height);
        left = Math.Clamp(left, area.Left, maxLeft);
        top = Math.Clamp(top, area.Top, maxTop);
    }

    private static Rect ResolveCustom(WalkAreaConfig config, Rect work)
    {
        var x = work.Left + config.CustomX;
        var y = work.Top + config.CustomY;
        var w = Math.Max(0, config.CustomWidth);
        var h = Math.Max(0, config.CustomHeight);
        var rect = new Rect(x, y, w, h);
        rect.Intersect(work);
        return rect.IsEmpty ? work : rect;
    }

    private static Rect Inset(Rect work, double left, double top, double right, double bottom)
    {
        var x = work.Left + Math.Max(0, left);
        var y = work.Top + Math.Max(0, top);
        var w = Math.Max(0, work.Width - Math.Max(0, left) - Math.Max(0, right));
        var h = Math.Max(0, work.Height - Math.Max(0, top) - Math.Max(0, bottom));
        return new Rect(x, y, w, h);
    }
}
