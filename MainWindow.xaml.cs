using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DesktopPet.Core;
using DesktopPet.Services;
using DesktopPet.Spine;
using DesktopPet.UI;

namespace DesktopPet;

public partial class MainWindow : Window
{
    private const double BottomRightMargin = 24;
    private const double DragThreshold = 5;
    private const float RenderBottomPadding = 8f;
    private const float RenderEdgePadding = 8f;
    private const double WalkSpeed = 90;
    private const double RunSpeed = 150;
    private const double RunDistanceThreshold = 280;
    private const double ArriveEpsilon = 2;

    private readonly SpineRuntimeHost _runtime = new();
    private readonly WpfSkeletonRenderer _renderer = new();
    private readonly PetStateMachine _stateMachine = new();
    private readonly AutonomyScheduler _autonomy = new();
    private readonly SpeechBubbleScheduler _bubbles = new();
    private readonly Random _rng = new();
    private SettingsService? _settings;
    private TimeSpan _lastRenderTime;
    private bool _rendering;
    private float[]? _boundsVertexBuffer;
    private double _fittedWidth = 120;
    private double _fittedHeight = 120;

    private System.Windows.Point _mouseDownInWindow;
    private bool _dragStarted;
    private bool _mouseCaptured;

    private bool _autonomyAction;
    private string? _pendingNamedAction;
    private bool _contextMenuOpen;
    private bool _hoverIdleOverride;
    private bool _preferRunWalk;
    private bool _hasWalkTarget;
    private double _walkTargetLeft;
    private double _walkTargetTop;
    private double _walkSpeed = WalkSpeed;
    private int _walkPullbackCount;
    private double _walkLastDistance = double.MaxValue;
    private SleepConfig _sleepConfig = SleepConfig.CreateDefault();
    private double _secondsSinceUserInteraction;
    private bool _readyRaised;

    /// <summary>Fired once after first pet load attempt (success or fail), for splash dismissal.</summary>
    public event Action? Ready;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
        Closed += (_, _) => Cleanup();
        _stateMachine.StateChanged += OnPetStateChanged;
        _runtime.AnimationCompleted += OnAnimationCompleted;
        _autonomy.RequestWalk += OnAutonomyRequestWalk;
        _autonomy.RequestAct += OnAutonomyRequestAct;
        _bubbles.RequestShow += OnBubbleRequestShow;
        _bubbles.RequestHide += OnBubbleRequestHide;
    }

    public void AttachSettings(SettingsService settings)
    {
        if (_settings is not null)
        {
            _settings.Changed -= OnSettingsChanged;
            LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        }

        _settings = settings;
        _settings.Changed += OnSettingsChanged;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        _autonomy.ApplyConfig(_settings.Config.Autonomy);
        _sleepConfig = SleepConfig.Normalize(_settings.Config.Sleep);
        ApplyBubbleSettings();
        NoteUserInteraction();
    }

    private void OnLanguageChanged()
    {
        RunOnUi(() =>
        {
            var petName = _settings?.Config.PetName ?? _runtime.LoadedPetName ?? "default";
            _bubbles.SetLines(PetAnimationMap.GetBubbleLines(petName));
            ApplyBubbleSettings();
        });
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PlaceAtBottomRight();
        ClampToWorkingArea();
        ReloadPet();
        ApplyClickThrough();
        if (IsVisible)
        {
            StartRendering();
        }

        RaiseReadyOnce();
    }

    private void RaiseReadyOnce()
    {
        if (_readyRaised)
        {
            return;
        }

        _readyRaised = true;
        Ready?.Invoke();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => StopRendering();

    /// <summary>
    /// 托盘「隐藏」后窗口不可见：停渲染、停自主/睡眠、取消气泡与进行中的 AI/天气请求。
    /// </summary>
    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            ResumeFromHidden();
        }
        else
        {
            PauseWhileHidden();
        }
    }

    private void PauseWhileHidden()
    {
        InterruptAutonomyForUser(wakeIfSleeping: false);
        _bubbles.TryGetAiLineAsync = null;
        _bubbles.Interrupt();
        HideSpeechBubbleImmediate();
        StopRendering();
    }

    private void ResumeFromHidden()
    {
        ApplyBubbleSettings();
        _bubbles.Reset();
        NoteUserInteraction();
        StartRendering();
    }
    private void OnSettingsChanged()
    {
        RunOnUi(() =>
        {
            Topmost = _settings?.Config.Topmost ?? Topmost;
            ApplyClickThrough();
            _autonomy.ApplyConfig(_settings?.Config.Autonomy);
            _sleepConfig = SleepConfig.Normalize(_settings?.Config.Sleep);
            ApplyBubbleSettings();
            if (!_sleepConfig.Enabled && _stateMachine.Current == PetState.Sleep)
            {
                _stateMachine.Wake();
            }

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
            CancelWalkMovement();
            _pendingNamedAction = null;
            _hoverIdleOverride = false;
            _runtime.LoadPet(petName);
            _stateMachine.Reset();
            _autonomy.Reset();
            _bubbles.SetLines(PetAnimationMap.GetBubbleLines(petName));
            _bubbles.Reset();
            HideSpeechBubbleImmediate();
            NoteUserInteraction();
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
                if (previous is PetState.Clicked or PetState.Walk)
                {
                    ShrinkToFittedSize();
                }

                break;
            case PetState.Walk:
                _bubbles.Interrupt();
                _runtime.PlayWalk(_preferRunWalk);
                break;
            case PetState.Sleep:
                CancelWalkMovement();
                _autonomy.Interrupt();
                _autonomyAction = false;
                _bubbles.Interrupt();
                _runtime.PlaySleep();
                break;
            case PetState.Clicked:
                CancelWalkMovement();
                _hoverIdleOverride = false;
                if (_pendingNamedAction is not null)
                {
                    var named = _pendingNamedAction;
                    _pendingNamedAction = null;
                    _runtime.PlayNamed(named);
                    _autonomy.Interrupt();
                }
                else if (_autonomyAction)
                {
                    _runtime.PlayRandomAction();
                    _autonomyAction = false;
                }
                else
                {
                    _runtime.PlayClick();
                    _autonomy.Interrupt();
                }

                break;
        }

        if (previous == PetState.Walk && next != PetState.Walk)
        {
            CancelWalkMovement();
        }
    }

    private void OnAnimationCompleted()
    {
        RunOnUi(() =>
        {
            if (_stateMachine.Current != PetState.Clicked)
            {
                return;
            }

            _stateMachine.EndClick();
            _autonomy.NotifyActFinished();
        });
    }

    private void OnAutonomyRequestWalk() => RunOnUi(BeginAutonomyWalk);

    private void OnAutonomyRequestAct()
    {
        RunOnUi(() =>
        {
            if (!_autonomy.IsExpectingAct ||
                _dragStarted ||
                _mouseCaptured ||
                _stateMachine.Current == PetState.Sleep)
            {
                if (_autonomy.IsExpectingAct)
                {
                    _autonomy.Interrupt();
                }

                return;
            }

            _autonomyAction = true;
            if (!_stateMachine.TryStartAct())
            {
                _autonomyAction = false;
                _autonomy.Interrupt();
            }
        });
    }

    private void RunOnUi(Action action)
    {
        if (Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.BeginInvoke(action);
        }
    }

    private void BeginAutonomyWalk()
    {
        if (!_autonomy.IsExpectingWalk ||
            _dragStarted ||
            _mouseCaptured ||
            !_runtime.IsLoaded ||
            _stateMachine.Current == PetState.Sleep)
        {
            if (_autonomy.IsExpectingWalk)
            {
                _autonomy.Interrupt();
            }

            return;
        }

        var area = GetWalkArea();
        var size = new System.Windows.Size(
            _fittedWidth > 0 ? _fittedWidth : (ActualWidth > 0 ? ActualWidth : Width),
            _fittedHeight > 0 ? _fittedHeight : (ActualHeight > 0 ? ActualHeight : Height));
        var target = WalkAreaResolver.PickRandomTopLeft(area, size, _rng);
        var dx = target.X - Left;
        var dy = target.Y - Top;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance < 40)
        {
            // Too close — skip this walk and wait again.
            _autonomy.NotifyWalkFinished();
            return;
        }

        _preferRunWalk = distance >= RunDistanceThreshold;
        _walkSpeed = _preferRunWalk ? RunSpeed : WalkSpeed;
        _walkTargetLeft = target.X;
        _walkTargetTop = target.Y;
        _hasWalkTarget = true;
        _walkPullbackCount = 0;
        _walkLastDistance = distance;

        if (Math.Abs(dx) > 1)
        {
            _runtime.SetFacing(dx > 0);
        }

        if (!_stateMachine.TryStartWalk())
        {
            CancelWalkMovement();
            _autonomy.Interrupt();
        }
    }

    private void UpdateWalkMovement(float delta)
    {
        if (!_hasWalkTarget || _stateMachine.Current != PetState.Walk)
        {
            return;
        }

        var dx = _walkTargetLeft - Left;
        var dy = _walkTargetTop - Top;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance <= ArriveEpsilon)
        {
            FinishWalkAtTarget();
            return;
        }

        var step = _walkSpeed * delta;
        if (step >= distance)
        {
            Left = _walkTargetLeft;
            Top = _walkTargetTop;
        }
        else
        {
            Left += dx / distance * step;
            Top += dy / distance * step;
        }

        var beforeLeft = Left;
        var beforeTop = Top;
        ClampToWalkArea();

        var afterDx = _walkTargetLeft - Left;
        var afterDy = _walkTargetTop - Top;
        var afterDistance = Math.Sqrt(afterDx * afterDx + afterDy * afterDy);
        if (afterDistance <= ArriveEpsilon)
        {
            FinishWalkAtTarget();
            return;
        }

        var pulled =
            Math.Abs(Left - beforeLeft) > 0.5 || Math.Abs(Top - beforeTop) > 0.5;
        if (pulled && afterDistance >= _walkLastDistance - 0.5)
        {
            // 被边界拉回且没更接近目标
            _walkPullbackCount++;
            if (_walkPullbackCount >= 4)
            {
                FinishWalkAtTarget(snap: false);
                return;
            }
        }
        else if (afterDistance < _walkLastDistance - 1)
        {
            _walkPullbackCount = 0;
        }

        _walkLastDistance = afterDistance;
    }

    private void FinishWalkAtTarget(bool snap = true)
    {
        if (snap)
        {
            Left = _walkTargetLeft;
            Top = _walkTargetTop;
            ClampToWalkArea();
        }

        CancelWalkMovement();
        _stateMachine.EndWalk();
        _autonomy.NotifyWalkFinished();
    }

    private void CancelWalkMovement()
    {
        _hasWalkTarget = false;
        _walkPullbackCount = 0;
        _walkLastDistance = double.MaxValue;
    }

    private void InterruptAutonomyForUser(bool wakeIfSleeping = false)
    {
        CancelWalkMovement();
        if (_stateMachine.Current == PetState.Walk)
        {
            _stateMachine.EndWalk();
        }

        if (wakeIfSleeping && _stateMachine.Current == PetState.Sleep)
        {
            _stateMachine.Wake();
        }

        _autonomy.Interrupt();
        _autonomyAction = false;
        _pendingNamedAction = null;
    }

    private void NoteUserInteraction()
    {
        _secondsSinceUserInteraction = 0;
    }

    private void ApplyClickThrough()
    {
        ClickThroughService.Apply(this, _settings?.Config.ClickThrough ?? false);
    }

    private void UpdateSleep(float delta)
    {
        if (!_sleepConfig.Enabled || _stateMachine.Current == PetState.Sleep)
        {
            return;
        }

        _secondsSinceUserInteraction += delta;
        if (_secondsSinceUserInteraction < _sleepConfig.IdleSeconds)
        {
            return;
        }

        if (_stateMachine.Current is not (PetState.Idle or PetState.Walk))
        {
            return;
        }

        CancelWalkMovement();
        _stateMachine.TryStartSleep();
    }

    private Rect GetWalkArea()
    {
        var cfg = _settings?.Config.WalkArea ?? new WalkAreaConfig();
        return WalkAreaResolver.Resolve(cfg);
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
        // 托盘隐藏时不应再跑逻辑（双重保险；正常路径已 StopRendering）
        if (!IsVisible)
        {
            return;
        }

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

        // 透明窗 IsMouseOver 不可靠（透明像素点穿）；用光标是否在窗口矩形内判断
        var clickThrough = _settings?.Config.ClickThrough ?? false;
        var hoverPause = !clickThrough && !_dragStarted &&
                         (_contextMenuOpen || IsCursorOverPetWindow());
        var uiOverlayOpen = SettingsWindow.IsOpen || TrayIconService.IsMenuOpen || _contextMenuOpen;
        if (_dragStarted)
        {
            // user drag owns movement
        }
        else if (_stateMachine.Current == PetState.Sleep)
        {
            // sleeping: no autonomy
        }
        else if (SettingsWindow.IsOpen)
        {
            if (_hasWalkTarget)
            {
                InterruptAutonomyForUser();
            }
        }
        else if (TrayIconService.IsMenuOpen || hoverPause)
        {
            // soft-pause: keep walk target, resume after leave / menu closes
            ApplyHoverIdleOverride(hoverPause);
        }
        else
        {
            ApplyHoverIdleOverride(false);
            _autonomy.Tick(delta);
            UpdateWalkMovement(delta);
        }

        if (!uiOverlayOpen &&
            !_dragStarted &&
            !_hasWalkTarget &&
            _stateMachine.Current is PetState.Idle &&
            (_settings?.Config.Bubble.Enabled ?? true))
        {
            _bubbles.Tick(delta);
        }

        if (!uiOverlayOpen && !_dragStarted)
        {
            UpdateSleep(delta);
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

        UpdateBubblePositionAboveHead(_runtime.Skeleton, scale, offsetX, offsetY);
    }

    /// <summary>
    /// Place the bubble just above the skeleton head (not at the window top).
    /// </summary>
    private void UpdateBubblePositionAboveHead(
        global::Spine.Skeleton skeleton,
        float scale,
        float offsetX,
        float offsetY)
    {
        if (SpeechBubble.Visibility != Visibility.Visible)
        {
            return;
        }

        skeleton.GetBounds(out _, out var by, out _, out var bh, ref _boundsVertexBuffer);
        if (bh <= 1 || bh >= 5000)
        {
            return;
        }

        var headTop = -(by + bh) * scale + offsetY;
        var bubbleH = SpeechBubble.ActualHeight;
        if (bubbleH < 8)
        {
            SpeechBubble.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            bubbleH = Math.Max(36, SpeechBubble.DesiredSize.Height);
        }

        // 留出头发/饰品高度，避免尖角压住头顶
        const double gap = 16;
        var top = headTop - bubbleH - gap;
        if (top < 4)
        {
            // 头顶空间不够时向上扩窗，而不是把气泡压到头发上
            var need = 4 - top;
            Height += need;
            Top -= need;
            top = 4;
        }

        top = Math.Min(top, Math.Max(4, ActualHeight - bubbleH - 4));
        SpeechBubble.Margin = new Thickness(0, top, 0, 0);
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

    private void OnBubbleRequestShow(string message)
    {
        if (!IsVisible)
        {
            return;
        }

        RunOnUi(() => SpeechBubble.Show(message));
    }

    private void OnBubbleRequestHide()
    {
        RunOnUi(() => SpeechBubble.Hide());
    }

    private void HideSpeechBubbleImmediate()
    {
        SpeechBubble.HideImmediate();
    }

    private void ApplyBubbleSettings()
    {
        var bubble = BubbleConfig.Normalize(_settings?.Config.Bubble);
        var ai = AiConfig.Normalize(_settings?.Config.Ai);

        // 窗口隐藏时不挂 AI、不调度气泡（含天气请求）。
        if (!IsVisible)
        {
            _bubbles.Enabled = false;
            _bubbles.TryGetAiLineAsync = null;
            _bubbles.Interrupt();
            HideSpeechBubbleImmediate();
            return;
        }

        _bubbles.Enabled = bubble.Enabled;

        // 未启用气泡时不挂 AI 回调，避免天气/接口被调度到。
        if (!bubble.Enabled)
        {
            _bubbles.TryGetAiLineAsync = null;
            _bubbles.Interrupt();
            HideSpeechBubbleImmediate();
            return;
        }

        _bubbles.TryGetAiLineAsync = ai.IsReady
            ? async ct =>
            {
                var userPrompt = await AiPromptContextBuilder.BuildBubbleUserPromptAsync(ai, ct)
                    .ConfigureAwait(false);
                return await AiChatService.CompleteAsync(
                        ai,
                        PetAnimationMap.GetAiRolePrompt(),
                        userPrompt,
                        ct)
                    .ConfigureAwait(false);
            }
            : null;
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

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_settings?.Config.ClickThrough == true)
        {
            return;
        }

        NoteUserInteraction();
    }

    private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_settings?.Config.ClickThrough == true || !_runtime.IsLoaded)
        {
            return;
        }

        NoteUserInteraction();
        OpenActionContextMenu();
        e.Handled = true;
    }

    private void OpenActionContextMenu()
    {
        var L = LocalizationService.Instance;
        var menu = new System.Windows.Controls.ContextMenu();
        menu.Opened += (_, _) => _contextMenuOpen = true;
        menu.Closed += (_, _) => _contextMenuOpen = false;

        var header = new System.Windows.Controls.MenuItem
        {
            Header = L.Get("Main.ActionMenu.Header"),
            IsEnabled = false,
        };
        menu.Items.Add(header);
        menu.Items.Add(new System.Windows.Controls.Separator());

        var actions = _runtime.ListClickActions();
        if (actions.Count == 0)
        {
            menu.Items.Add(new System.Windows.Controls.MenuItem
            {
                Header = L.Get("Main.ActionMenu.Empty"),
                IsEnabled = false,
            });
        }
        else
        {
            foreach (var name in actions)
            {
                var animName = name;
                var item = new System.Windows.Controls.MenuItem
                {
                    Header = L.GetAnimationDisplayName(animName),
                };
                item.Click += (_, _) => RequestNamedAction(animName);
                menu.Items.Add(item);
            }
        }

        ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void RequestNamedAction(string animationName)
    {
        if (string.IsNullOrWhiteSpace(animationName) || !_runtime.IsLoaded)
        {
            return;
        }

        NoteUserInteraction();
        InterruptAutonomyForUser(wakeIfSleeping: true);
        _autonomyAction = false;
        _hoverIdleOverride = false;

        if (_stateMachine.Current == PetState.Clicked)
        {
            _pendingNamedAction = null;
            _runtime.PlayNamed(animationName);
            return;
        }

        _pendingNamedAction = animationName;
        if (!_stateMachine.TryClick())
        {
            _pendingNamedAction = null;
        }
    }

    /// <summary>
    /// While hovering (or action menu open), freeze walk in place with idle pose.
    /// </summary>
    private void ApplyHoverIdleOverride(bool active)
    {
        if (active)
        {
            if (_stateMachine.Current == PetState.Walk && !_hoverIdleOverride)
            {
                _hoverIdleOverride = true;
                _runtime.PlayIdle();
            }

            return;
        }

        if (!_hoverIdleOverride)
        {
            return;
        }

        _hoverIdleOverride = false;
        if (_stateMachine.Current == PetState.Walk && _hasWalkTarget)
        {
            _runtime.PlayWalk(_preferRunWalk);
        }
    }

    /// <summary>
    /// True when the OS cursor is inside this window's screen rectangle (DIP).
    /// Preferred over <see cref="UIElement.IsMouseOver"/> for transparent pets.
    /// </summary>
    private bool IsCursorOverPetWindow()
    {
        if (!IsVisible || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return false;
        }

        try
        {
            var screen = System.Windows.Forms.Cursor.Position;
            var local = PointFromScreen(new System.Windows.Point(screen.X, screen.Y));
            return local.X >= 0 &&
                   local.Y >= 0 &&
                   local.X < ActualWidth &&
                   local.Y < ActualHeight;
        }
        catch
        {
            return false;
        }
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
            NoteUserInteraction();
            InterruptAutonomyForUser(wakeIfSleeping: true);
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
            NoteUserInteraction();
            InterruptAutonomyForUser();
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
        ClampToArea(SystemParameters.WorkArea);
    }

    private void ClampToWalkArea()
    {
        ClampToArea(GetWalkArea());
    }

    private void ClampToArea(Rect area)
    {
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        var left = Left;
        var top = Top;
        WalkAreaResolver.ClampTopLeft(ref left, ref top, new System.Windows.Size(width, height), area);
        Left = left;
        Top = top;
    }

    private void Cleanup()
    {
        StopRendering();
        _stateMachine.StateChanged -= OnPetStateChanged;
        _runtime.AnimationCompleted -= OnAnimationCompleted;
        _autonomy.RequestWalk -= OnAutonomyRequestWalk;
        _autonomy.RequestAct -= OnAutonomyRequestAct;
        _bubbles.RequestShow -= OnBubbleRequestShow;
        _bubbles.RequestHide -= OnBubbleRequestHide;
        if (_settings is not null)
        {
            _settings.Changed -= OnSettingsChanged;
        }

        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;

        _renderer.Dispose();
        _runtime.Dispose();
    }
}
