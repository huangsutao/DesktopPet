using System.Windows;
using System.Windows.Media.Animation;

namespace DesktopPet.UI;

public partial class SpeechBubbleView : System.Windows.Controls.UserControl
{
    private Action? _onHidden;

    public SpeechBubbleView()
    {
        InitializeComponent();
    }

    public bool IsShowing => Visibility == Visibility.Visible && Opacity > 0.05;

    public void Show(string message)
    {
        MessageText.Text = message;
        Visibility = Visibility.Visible;
        BeginAnimation(OpacityProperty, null);
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        BeginAnimation(OpacityProperty, fadeIn);
    }

    public void Hide(Action? onHidden = null)
    {
        if (Visibility != Visibility.Visible)
        {
            onHidden?.Invoke();
            return;
        }

        _onHidden = onHidden;
        BeginAnimation(OpacityProperty, null);
        var fadeOut = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        fadeOut.Completed += (_, _) =>
        {
            Visibility = Visibility.Collapsed;
            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            var cb = _onHidden;
            _onHidden = null;
            cb?.Invoke();
        };
        BeginAnimation(OpacityProperty, fadeOut);
    }

    public void HideImmediate()
    {
        BeginAnimation(OpacityProperty, null);
        _onHidden = null;
        Visibility = Visibility.Collapsed;
        Opacity = 0;
    }
}
