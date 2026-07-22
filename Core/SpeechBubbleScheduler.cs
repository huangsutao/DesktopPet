namespace DesktopPet.Core;

/// <summary>
/// Periodically shows short random lines above the pet.
/// </summary>
public sealed class SpeechBubbleScheduler
{
    private static readonly string[] Lines =
    [
        "今天也要加油呀～",
        "摸鱼一时爽，一直摸鱼一直爽。",
        "你那边天气怎么样？",
        "我在这儿陪着你呢。",
        "喝口水、眨眨眼，休息一下吧。",
        "有什么开心的事，讲给我听？",
        "工作再忙，也别忘了站起来走走。",
        "哼，才没有在等你理我。",
        "午后阳光刚刚好。",
        "悄悄告诉你：我超可爱的。",
        "想吃点甜的……你呢？",
        "发呆也是一种生产力。",
        "别皱眉，会变老的哦。",
        "我刚刚做了个好梦。",
        "世界很大，但你面前有我。",
    ];

    private static readonly (double Min, double Max) GapSeconds = (22, 48);
    private static readonly (double Min, double Max) ShowSeconds = (4.0, 6.5);

    private readonly Random _rng = new();
    private double _timer;
    private bool _showing;
    private int _lastIndex = -1;

    public event Action<string>? RequestShow;
    public event Action? RequestHide;

    public bool IsShowing => _showing;

    public SpeechBubbleScheduler() =>
        _timer = 5 + _rng.NextDouble() * 5;

    public void Reset()
    {
        _showing = false;
        _timer = 5 + _rng.NextDouble() * 5;
    }

    public void Interrupt()
    {
        if (_showing)
        {
            _showing = false;
            RequestHide?.Invoke();
        }

        ScheduleGap();
    }

    public void Tick(double deltaSeconds)
    {
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
        if (Lines.Length == 1)
        {
            return Lines[0];
        }

        int index;
        do
        {
            index = _rng.Next(Lines.Length);
        } while (index == _lastIndex);

        _lastIndex = index;
        return Lines[index];
    }

    private void ScheduleGap() =>
        _timer = GapSeconds.Min + _rng.NextDouble() * (GapSeconds.Max - GapSeconds.Min);

    private void ScheduleShow() =>
        _timer = ShowSeconds.Min + _rng.NextDouble() * (ShowSeconds.Max - ShowSeconds.Min);
}
