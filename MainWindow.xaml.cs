using System.Windows;
using System.Windows.Input;

namespace DesktopPet;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ClampToWorkingArea();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
            ClampToWorkingArea();
        }
    }

    private void ClampToWorkingArea()
    {
        var work = SystemParameters.WorkArea;
        if (Left < work.Left)
        {
            Left = work.Left;
        }

        if (Top < work.Top)
        {
            Top = work.Top;
        }

        if (Left + ActualWidth > work.Right)
        {
            Left = work.Right - ActualWidth;
        }

        if (Top + ActualHeight > work.Bottom)
        {
            Top = work.Bottom - ActualHeight;
        }
    }
}
