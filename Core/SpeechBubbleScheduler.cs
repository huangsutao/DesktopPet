using DesktopPet.Spine;

namespace DesktopPet.Core;

/// <summary>
/// Periodically shows short random lines above the pet.
/// Lines are loaded from pet-animations.json.
/// </summary>
public sealed class SpeechBubbleScheduler
{
    private static readonly (double Min, double Max) GapSeconds = (18, 36);
    private static readonly (double Min, double Max) ShowSeconds = (4.0, 6.5);

    private readonly Random _rng = new();
    private IReadOnlyList<string> _lines = PetAnimationMap.GetBubbleLines();
    private double _timer;
    private bool _showing;
    private int _lastIndex = -1;

    public event Action<string>? RequestShow;
    public event Action? RequestHide;

    public bool IsShowing => _showing;

    public SpeechBubbleScheduler() =>
        _timer = 5 + _rng.NextDouble() * 5;

    public void SetLines(IReadOnlyList<string> lines)
    {
        _lines = lines.Count > 0 ? lines : PetAnimationMap.GetBubbleLines();
        _lastIndex = -1;
    }

    public void Reset()
    {
        _showing = false;
        _timer = 5 + _rng.NextDouble() * 5;
    }

    /// <summary>
    /// Hide if currently showing. Does not reset the idle gap when already hidden
    /// (otherwise frequent Walk would keep postponing bubbles forever).
    /// </summary>
    public void Interrupt()
    {
        if (!_showing)
        {
            return;
        }

        _showing = false;
        RequestHide?.Invoke();
        ScheduleGap();
    }

    public void Tick(double deltaSeconds)
    {
        if (_lines.Count == 0)
        {
            return;
        }

        _timer -= deltaSeconds;
        if (_timer > 0)
        {
            return;
        }

        if (_showing)
        {
            _showing = false;
            RequestHide?.Invoke();
            ScheduleGap();
            return;
        }

        _showing = true;
        RequestShow?.Invoke(PickLine());
        ScheduleShow();
    }

    private string PickLine()
    {
        if (_lines.Count == 1)
        {
            return _lines[0];
        }

        int index;
        do
        {
            index = _rng.Next(_lines.Count);
        } while (index == _lastIndex);

        _lastIndex = index;
        return _lines[index];
    }

    private void ScheduleGap() =>
        _timer = GapSeconds.Min + _rng.NextDouble() * (GapSeconds.Max - GapSeconds.Min);

    private void ScheduleShow() =>
        _timer = ShowSeconds.Min + _rng.NextDouble() * (ShowSeconds.Max - ShowSeconds.Min);
}
