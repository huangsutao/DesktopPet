using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DesktopPet.Core;
using DesktopPet.Services;
using DesktopPet.Spine;

namespace DesktopPet;

public partial class MainWindow : Window
{
    private const double BottomRightMargin = 24;
    private const double DragThreshold = 5;
    private const float RenderBottomPadding = 8f;
    private const float RenderEdgePadding = 8f;

    private readonly SpineRuntimeHost _runtime = new();
    private readonly WpfSkeletonRenderer _renderer = new();
    private readonly PetStateMachine _stateMachine = new();
    private SettingsService? _settings;
    private TimeSpan _lastRenderTime;
    private bool _rendering;
    private float[]? _boundsVertexBuffer;
    private double _fittedWidth = 120;
    private double _fittedHeight = 120;

    private System.Windows.Point _mouseDownInWindow;
    private bool _dragStarted;
    private bool _mouseCaptured;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Closed += (_, _) => Cleanup();
        _stateMachine.StateChanged += OnPetStateChanged;
        _runtime.AnimationCompleted += OnAnimationCompleted;
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
            _stateMachine.Reset();
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

    private void OnPetStateChanged(PetState previous, PetState next)
    {
        switch (next)
        {
            case PetState.Idle:
                _runtime.PlayIdle();
                if (previous is PetState.Clicked)
                {
                    ShrinkToFittedSize();
                }

                break;
            case PetState.Clicked:
                _runtime.PlayClick();
                break;
        }
    }

    private void OnAnimationCompleted()
    {
        Dispatcher.Invoke(() =>
        {
            if (_stateMachine.Current == PetState.Clicked)
            {
                _stateMachine.EndClick();
            }
        });
    }

    private void FitWindowToSkeleton()
    {
        if (_runtime.Skeleton is null || _runtime.SkeletonData is null)
        {
            return;
        }

        var scale = (float)(_settings?.Config.Scale ?? 0.25);
        _runtime.Skeleton.UpdateWorldTransform(global::Spine.Physics.Update);
        _runtime.Skeleton.GetBounds(out _, out _, out var bw, out var bh, ref _boundsVertexBuffer);

        if (IsValidBounds(bw, bh))
        {
            // 脚底锚点：余量加在头顶方向，脚下贴底，不产生任务栏空隙
            const float pad = 12f;
            const float jumpHeadroomFactor = 0.45f;
            _fittedWidth = Math.Max(120, bw * scale + pad * 2);
            _fittedHeight = Math.Max(120, bh * scale * (1f + jumpHeadroomFactor) + pad * 2);
        }
        else
        {
            var data = _runtime.SkeletonData;
            _fittedWidth = Math.Max(120, data.Width * scale * 1.1 + 16);
            _fittedHeight = Math.Max(120, data.Height * scale * 1.35 + 16);
        }

        Width = _fittedWidth;
        Height = _fittedHeight;
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

        var scale = (float)(_settings?.Config.Scale ?? 0.25);
        var pixelW = Math.Max(1, (int)Math.Ceiling(ActualWidth));
        var pixelH = Math.Max(1, (int)Math.Ceiling(ActualHeight));

        // 固定脚底锚点；渲染循环只扩不缩，避免扩/缩振荡卡死 UI
        var offsetX = pixelW * 0.5f;
        var offsetY = pixelH - RenderBottomPadding;
        if (ExpandWindowIfClipped(_runtime.Skeleton, scale, offsetX, offsetY))
        {
            pixelW = Math.Max(1, (int)Math.Ceiling(ActualWidth));
            pixelH = Math.Max(1, (int)Math.Ceiling(ActualHeight));
            offsetX = pixelW * 0.5f;
            offsetY = pixelH - RenderBottomPadding;
        }

        _renderer.EnsureSize(pixelW, pixelH);
        _renderer.Render(_runtime.Skeleton, scale, offsetX, offsetY);

        if (!ReferenceEquals(PetImage.Source, _renderer.ImageSource))
        {
            PetImage.Source = _renderer.ImageSource;
        }
    }

    /// <summary>
    /// 仅在包围盒越出窗口时扩窗；向上扩展并保持窗口底边不动。
    /// </summary>
    private bool ExpandWindowIfClipped(global::Spine.Skeleton skeleton, float scale, float offsetX, float offsetY)
    {
        skeleton.GetBounds(out var bx, out var by, out var bw, out var bh, ref _boundsVertexBuffer);
        if (!IsValidBounds(bw, bh))
        {
            return false;
        }

        var screenLeft = bx * scale + offsetX;
        var screenRight = (bx + bw) * scale + offsetX;
        var screenTop = -(by + bh) * scale + offsetY;

        var growTop = Math.Max(0, RenderEdgePadding - screenTop);
        var growLeft = Math.Max(0, RenderEdgePadding - screenLeft);
        var growRight = Math.Max(0, screenRight - (ActualWidth - RenderEdgePadding));

        if (growTop < 2 && growLeft < 2 && growRight < 2)
        {
            return false;
        }

        var newW = Math.Max(_fittedWidth, ActualWidth + growLeft + growRight);
        var newH = Math.Max(_fittedHeight, ActualHeight + growTop);
        return ResizeKeepingBottomCenter(newW, newH);
    }

    private void ShrinkToFittedSize()
    {
        ResizeKeepingBottomCenter(_fittedWidth, _fittedHeight);
    }

    private bool ResizeKeepingBottomCenter(double newW, double newH)
    {
        if (Math.Abs(newW - Width) < 1 && Math.Abs(newH - Height) < 1)
        {
            return false;
        }

        var bottom = Top + ActualHeight;
        var centerX = Left + ActualWidth / 2;
        Width = newW;
        Height = newH;
        Left = centerX - newW / 2;
        Top = bottom - newH;
        ClampToWorkingArea();
        return true;
    }

    private static bool IsValidBounds(float bw, float bh) =>
        bw > 1 && bh > 1 && bw < 5000 && bh < 5000;

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _mouseDownInWindow = e.GetPosition(this);
        _dragStarted = false;
        _mouseCaptured = CaptureMouse();
        e.Handled = true;
    }

    private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_mouseCaptured || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        var dx = current.X - _mouseDownInWindow.X;
        var dy = current.Y - _mouseDownInWindow.Y;

        if (!_dragStarted && (Math.Abs(dx) > DragThreshold || Math.Abs(dy) > DragThreshold))
        {
            _dragStarted = true;
            // 拖拽只移动窗口，不切换状态、不播动作
        }

        if (!_dragStarted)
        {
            return;
        }

        var screen = PointToScreen(e.GetPosition(this));
        Left = screen.X - _mouseDownInWindow.X;
        Top = screen.Y - _mouseDownInWindow.Y;
        ClampToWorkingArea();
        e.Handled = true;
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_mouseCaptured)
        {
            return;
        }

        ReleaseMouseCapture();
        _mouseCaptured = false;

        if (_dragStarted)
        {
            ClampToWorkingArea();
        }
        else
        {
            _stateMachine.TryClick();
        }

        _dragStarted = false;
        e.Handled = true;
    }

    private void Window_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragStarted)
        {
            ClampToWorkingArea();
        }

        _mouseCaptured = false;
        _dragStarted = false;
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
        _stateMachine.StateChanged -= OnPetStateChanged;
        _runtime.AnimationCompleted -= OnAnimationCompleted;
        if (_settings is not null)
        {
            _settings.Changed -= OnSettingsChanged;
        }

        _renderer.Dispose();
        _runtime.Dispose();
    }
}
