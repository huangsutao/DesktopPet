using System.Windows;
using System.Windows.Media;

namespace DesktopPet.UI;

public partial class SplashWindow : Window
{
    private const double BottomRightMargin = 24;

    public SplashWindow()
    {
        InitializeComponent();
        Halo.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        Halo.RenderTransform = new ScaleTransform(1, 1);
        Core.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        Core.RenderTransform = new ScaleTransform(1, 1);
        PlaceAtBottomRight();
    }

    /// <summary>与 MainWindow 初始落点一致：工作区右下角留边。</summary>
    public void PlaceAtBottomRight()
    {
        var work = SystemParameters.WorkArea;
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        Left = work.Right - width - BottomRightMargin;
        Top = work.Bottom - height - BottomRightMargin;
    }
}
