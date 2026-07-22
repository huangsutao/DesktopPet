using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DesktopPet.Services;
using DesktopPet.Spine;

namespace DesktopPet;

public partial class MainWindow : Window
{
    private const double BottomRightMargin = 24;

    private readonly SpineRuntimeHost _runtime = new();
    private readonly WpfSkeletonRenderer _renderer = new();
    private SettingsService? _settings;
    private TimeSpan _lastRenderTime;
    private bool _rendering;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Closed += (_, _) => Cleanup();
    }

    public void AttachSettings(SettingsService settings)
    {
        if (_settings is not null)
        {
            _settings.Changed -= OnSettingsChanged;
        }

        _settings = settings;
        _settings.Changed += OnSettingsChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PlaceAtBottomRight();
        ClampToWorkingArea();
        ReloadPet();
        StartRendering();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => StopRendering();

    private void OnSettingsChanged()
    {
        Dispatcher.Invoke(() =>
        {
            Topmost = _settings?.Config.Topmost ?? Topmost;
            var petName = _settings?.Config.PetName ?? "default";
            if (!string.Equals(_runtime.LoadedPetName, petName, StringComparison.OrdinalIgnoreCase))
            {
                ReloadPet();
            }
            else
            {
                FitWindowToSkeleton();
                ClampToWorkingArea();
            }
        });
    }

    private void ReloadPet()
    {
        var petName = _settings?.Config.PetName ?? "default";
        try
        {
            _runtime.LoadPet(petName);
            FitWindowToSkeleton();
            PlaceAtBottomRight();
            ClampToWorkingArea();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"加载形象失败：{petName}\n{ex.Message}",
                "DesktopPet",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void FitWindowToSkeleton()
    {
        if (_runtime.SkeletonData is null)
        {
            return;
        }

        var scale = (float)(_settings?.Config.Scale ?? 0.25);
        var data = _runtime.SkeletonData;
        // Skeleton data size is in spine units; atlas scale for examples is often 0.5.
        var width = Math.Max(120, data.Width * scale + 24);
        var height = Math.Max(120, data.Height * scale + 24);
        Width = width;
        Height = height;
    }

    private void StartRendering()
    {
        if (_rendering)
        {
            return;
        }

        _rendering = true;
        _lastRenderTime = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopRendering()
    {
        if (!_rendering)
        {
            return;
        }

        CompositionTarget.Rendering -= OnRendering;
        _rendering = false;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_runtime.IsLoaded || _runtime.Skeleton is null)
        {
            return;
        }

        var args = (RenderingEventArgs)e;
        if (_lastRenderTime == TimeSpan.Zero)
        {
            _lastRenderTime = args.RenderingTime;
            return;
        }

        var delta = (float)(args.RenderingTime - _lastRenderTime).TotalSeconds;
        _lastRenderTime = args.RenderingTime;
        if (delta <= 0 || delta > 0.1f)
        {
            delta = 1f / 60f;
        }

        _runtime.Update(delta);

        var pixelW = Math.Max(1, (int)Math.Ceiling(ActualWidth));
        var pixelH = Math.Max(1, (int)Math.Ceiling(ActualHeight));
        _renderer.EnsureSize(pixelW, pixelH);

        var scale = (float)(_settings?.Config.Scale ?? 0.25);
        // Origin near bottom-center of the window (after Y flip in renderer).
        var offsetX = pixelW * 0.5f;
        var offsetY = pixelH * 0.85f;
        _renderer.Render(_runtime.Skeleton, scale, offsetX, offsetY);

        if (!ReferenceEquals(PetImage.Source, _renderer.ImageSource))
        {
            PetImage.Source = _renderer.ImageSource;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
            ClampToWorkingArea();
        }
    }

    private void PlaceAtBottomRight()
    {
        var work = SystemParameters.WorkArea;
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        Left = work.Right - width - BottomRightMargin;
        Top = work.Bottom - height - BottomRightMargin;
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

    private void Cleanup()
    {
        StopRendering();
        if (_settings is not null)
        {
            _settings.Changed -= OnSettingsChanged;
        }

        _renderer.Dispose();
        _runtime.Dispose();
    }
}
