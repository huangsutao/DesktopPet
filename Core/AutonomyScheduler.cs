namespace DesktopPet.Core;

/// <summary>
/// Calm autonomy loop: wait → walk → wait → act → wait → …
/// deliberately sparse so the pet does not constantly demand attention.
/// Pause ranges come from <see cref="AutonomyConfig"/> (settings.json).
/// </summary>
public sealed class AutonomyScheduler
{
    private enum Phase
    {
        IdlePause,
        Walking,
        PostWalkPause,
        Acting,
        PostActPause,
    }

    private readonly Random _rng = new();
    private AutonomyConfig _config = AutonomyConfig.CreateDefault();
    private Phase _phase = Phase.IdlePause;
    private double _timer;

    public event Action? RequestWalk;
    public event Action? RequestAct;

    /// <summary>True after <see cref="RequestWalk"/> until finish/interrupt.</summary>
    public bool IsExpectingWalk => _phase == Phase.Walking;

    /// <summary>True after <see cref="RequestAct"/> until finish/interrupt.</summary>
    public bool IsExpectingAct => _phase == Phase.Acting;

    public AutonomyScheduler() => Schedule(_config.PauseBeforeWalk);

    public void ApplyConfig(AutonomyConfig? config)
    {
        _config = AutonomyConfig.Normalize(config);
    }

    public void Reset()
    {
        _phase = Phase.IdlePause;
        Schedule(_config.PauseBeforeWalk);
    }

    /// <summary>User click/drag: abort current plan and wait before walking again.</summary>
    public void Interrupt()
    {
        _phase = Phase.IdlePause;
        Schedule(_config.PauseBeforeWalk);
    }

    public void NotifyWalkFinished()
    {
        if (_phase != Phase.Walking)
        {
            return;
        }

        _phase = Phase.PostWalkPause;
        Schedule(_config.PauseAfterWalk);
    }

    public void NotifyActFinished()
    {
        if (_phase != Phase.Acting)
        {
            return;
        }

        _phase = Phase.PostActPause;
        Schedule(_config.PauseAfterAct);
    }

    public void Tick(double deltaSeconds)
    {
        if (_phase is Phase.Walking or Phase.Acting)
        {
            return;
        }

        _timer -= deltaSeconds;
        if (_timer > 0)
        {
            return;
        }

        switch (_phase)
        {
            case Phase.IdlePause:
            case Phase.PostActPause:
                _phase = Phase.Walking;
                RequestWalk?.Invoke();
                break;
            case Phase.PostWalkPause:
                _phase = Phase.Acting;
                RequestAct?.Invoke();
                break;
        }
    }

    private void Schedule(SecondsRange range) =>
        _timer = range.Min + _rng.NextDouble() * (range.Max - range.Min);
}
