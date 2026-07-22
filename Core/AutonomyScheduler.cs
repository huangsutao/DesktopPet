namespace DesktopPet.Core;

/// <summary>
/// Calm autonomy loop: wait → walk → wait → act → wait → …
/// deliberately sparse so the pet does not constantly demand attention.
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

    // Seconds — long pauses by design.
    private static readonly (double Min, double Max) PauseBeforeWalk = (12, 28);
    private static readonly (double Min, double Max) PauseAfterWalk = (6, 16);
    private static readonly (double Min, double Max) PauseAfterAct = (10, 24);

    private readonly Random _rng = new();
    private Phase _phase = Phase.IdlePause;
    private double _timer;

    public event Action? RequestWalk;
    public event Action? RequestAct;

    /// <summary>True after <see cref="RequestWalk"/> until finish/interrupt.</summary>
    public bool IsExpectingWalk => _phase == Phase.Walking;

    /// <summary>True after <see cref="RequestAct"/> until finish/interrupt.</summary>
    public bool IsExpectingAct => _phase == Phase.Acting;

    public AutonomyScheduler() => Schedule(PauseBeforeWalk);

    public void Reset()
    {
        _phase = Phase.IdlePause;
        Schedule(PauseBeforeWalk);
    }

    /// <summary>User click/drag: abort current plan and wait before walking again.</summary>
    public void Interrupt()
    {
        _phase = Phase.IdlePause;
        Schedule(PauseBeforeWalk);
    }

    public void NotifyWalkFinished()
    {
        if (_phase != Phase.Walking)
        {
            return;
        }

        _phase = Phase.PostWalkPause;
        Schedule(PauseAfterWalk);
    }

    public void NotifyActFinished()
    {
        if (_phase != Phase.Acting)
        {
            return;
        }

        _phase = Phase.PostActPause;
        Schedule(PauseAfterAct);
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

    private void Schedule((double Min, double Max) range) =>
        _timer = range.Min + _rng.NextDouble() * (range.Max - range.Min);
}
