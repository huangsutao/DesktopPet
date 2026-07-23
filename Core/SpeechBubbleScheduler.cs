using DesktopPet.Spine;

namespace DesktopPet.Core;

/// <summary>
/// Periodically shows short random lines above the pet.
/// Lines are loaded from pet-animations.json; optional AI provider is preferred when set.
/// </summary>
public sealed class SpeechBubbleScheduler
{
    private static readonly (double Min, double Max) GapSeconds = (18, 36);
    private static readonly (double Min, double Max) ShowSeconds = (4.0, 6.5);
    private static readonly TimeSpan AiTimeout = TimeSpan.FromSeconds(8);

    private readonly Random _rng = new();
    private IReadOnlyList<string> _lines = PetAnimationMap.GetBubbleLines();
    private double _timer;
    private bool _showing;
    private bool _fetching;
    private int _generation;
    private int _lastIndex = -1;
    private CancellationTokenSource? _fetchCts;

    public event Action<string>? RequestShow;
    public event Action? RequestHide;

    /// <summary>When false, ticks are no-ops and any visible bubble is interrupted.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Optional AI line provider. Return null/empty to fall back to local config lines.
    /// Only invoked when <see cref="Enabled"/> is true.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? TryGetAiLineAsync { get; set; }

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
        CancelFetch();
        _generation++;
        _fetching = false;
        _showing = false;
        _timer = 5 + _rng.NextDouble() * 5;
    }

    /// <summary>
    /// Hide if currently showing. Does not reset the idle gap when already hidden
    /// (otherwise frequent Walk would keep postponing bubbles forever).
    /// Also cancels any in-flight AI/weather request.
    /// </summary>
    public void Interrupt()
    {
        CancelFetch();
        _generation++;
        _fetching = false;
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
        if (!Enabled)
        {
            if (_showing || _fetching)
            {
                Interrupt();
            }

            return;
        }

        if (_fetching)
        {
            return;
        }

        // AI is only for bubbles: with bubbles off we never get here; with AI off, provider is null.
        if (_lines.Count == 0 && TryGetAiLineAsync is null)
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

        _fetching = true;
        var gen = ++_generation;
        _ = BeginShowAsync(gen);
    }

    private async Task BeginShowAsync(int generation)
    {
        var fallback = PickLine();
        var message = fallback;

        try
        {
            // Bubble must stay enabled; otherwise skip AI/weather entirely.
            var aiProvider = Enabled ? TryGetAiLineAsync : null;
            if (aiProvider is not null)
            {
                try
                {
                    using var timeoutCts = new CancellationTokenSource(AiTimeout);
                    _fetchCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);
                    var token = _fetchCts.Token;
                    var ai = await aiProvider(token).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(ai))
                    {
                        message = ai.Trim();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Interrupted or timed out — keep local fallback only if still current.
                }
                catch
                {
                    // Keep local fallback on any AI failure.
                }
                finally
                {
                    _fetchCts?.Dispose();
                    _fetchCts = null;
                }
            }

            if (generation != _generation || !Enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                ScheduleGap();
                return;
            }

            _showing = true;
            RequestShow?.Invoke(message);
            ScheduleShow();
        }
        finally
        {
            if (generation == _generation)
            {
                _fetching = false;
            }
        }
    }

    private void CancelFetch()
    {
        try
        {
            _fetchCts?.Cancel();
        }
        catch
        {
            // Ignore dispose races.
        }
    }

    private string PickLine()
    {
        if (_lines.Count == 0)
        {
            return string.Empty;
        }

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
