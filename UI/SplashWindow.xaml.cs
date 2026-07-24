using System.Windows;
using System.Windows.Media;

namespace DesktopPet.UI;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Halo.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        Halo.RenderTransform = new ScaleTransform(1, 1);
        Core.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        Core.RenderTransform = new ScaleTransform(1, 1);
    }
}
