using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DesktopPet.Core;

namespace DesktopPet.Services;

/// <summary>
/// Builds the user message for bubble AI calls: local time/date + location + optional weather.
/// Weather uses Open-Meteo (no API key). Hot topics are left to the model with prompt guidance.
/// </summary>
public static class AiPromptContextBuilder
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(6),
    };

    private static readonly object WeatherLock = new();
    private static string? _weatherCacheKey;
    private static string? _weatherCacheText;
    private static DateTime _weatherCacheUtc = DateTime.MinValue;
    private static readonly TimeSpan WeatherCacheTtl = TimeSpan.FromMinutes(30);

    private static readonly string[] WeekdaysZh =
    [
        "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六",
    ];

    public static async Task<string> BuildBubbleUserPromptAsync(
        AiConfig config,
        CancellationToken cancellationToken = default)
    {
        config = AiConfig.Normalize(config);
        var now = DateTime.Now;
        var sb = new StringBuilder();

        sb.AppendLine("【当前情境】");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"本地时间：{now:yyyy年M月d日} {WeekdaysZh[(int)now.DayOfWeek]} {now:HH:mm}（{DescribeDayPart(now)}）");
        sb.AppendLine(CultureInfo.InvariantCulture, $"所在国家/地区：{config.Country}");

        if (!string.IsNullOrWhiteSpace(config.City))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"所在城市：{config.City}");
            var weather = await TryGetWeatherBriefAsync(config.Country, config.City, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(weather))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"当地天气：{weather}");
            }
        }
        else
        {
            sb.AppendLine("所在城市：未设置");
        }

        sb.AppendLine();
        sb.AppendLine(
            "请结合以上情境（时段、日期、地点、天气），并可自然带一句应季或近期大众话题的轻松感受；" +
            "不要编造具体新闻标题或数据。说一句很短的话跟我互动，不要超过40个字。");

        return sb.ToString().TrimEnd();
    }

    private static string DescribeDayPart(DateTime now) => now.Hour switch
    {
        >= 5 and < 9 => "清晨",
        >= 9 and < 12 => "上午",
        >= 12 and < 14 => "中午",
        >= 14 and < 18 => "下午",
        >= 18 and < 23 => "晚上",
        _ => "深夜",
    };

    private static async Task<string?> TryGetWeatherBriefAsync(
        string country,
        string city,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{country}|{city}";
        lock (WeatherLock)
        {
            if (_weatherCacheKey == cacheKey &&
                !string.IsNullOrWhiteSpace(_weatherCacheText) &&
                DateTime.UtcNow - _weatherCacheUtc < WeatherCacheTtl)
            {
                return _weatherCacheText;
            }
        }

        try
        {
            var query = Uri.EscapeDataString(city);
            var geoUrl =
                $"https://geocoding-api.open-meteo.com/v1/search?name={query}&count=5&language=zh&format=json";
            using var geoResponse = await Http.GetAsync(geoUrl, cancellationToken).ConfigureAwait(false);
            if (!geoResponse.IsSuccessStatusCode)
            {
                return null;
            }

            await using var geoStream = await geoResponse.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var geoDoc = await JsonDocument.ParseAsync(geoStream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!geoDoc.RootElement.TryGetProperty("results", out var results) ||
                results.ValueKind != JsonValueKind.Array ||
                results.GetArrayLength() == 0)
            {
                return null;
            }

            var place = PickBestPlace(results, country);
            if (place is null)
            {
                return null;
            }

            var (lat, lon, label) = place.Value;
            var weatherUrl =
                $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString(CultureInfo.InvariantCulture)}" +
                $"&longitude={lon.ToString(CultureInfo.InvariantCulture)}" +
                "&current=temperature_2m,weather_code,relative_humidity_2m&timezone=auto";

            using var weatherResponse = await Http.GetAsync(weatherUrl, cancellationToken)
                .ConfigureAwait(false);
            if (!weatherResponse.IsSuccessStatusCode)
            {
                return null;
            }

            await using var weatherStream =
                await weatherResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var weatherDoc =
                await JsonDocument.ParseAsync(weatherStream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

            if (!weatherDoc.RootElement.TryGetProperty("current", out var current))
            {
                return null;
            }

            var temp = current.TryGetProperty("temperature_2m", out var t) ? t.GetDouble() : double.NaN;
            var code = current.TryGetProperty("weather_code", out var c) ? c.GetInt32() : -1;
            var humidity = current.TryGetProperty("relative_humidity_2m", out var h) ? h.GetInt32() : -1;

            var parts = new List<string> { label, DescribeWmoCode(code) };
            if (!double.IsNaN(temp))
            {
                parts.Add($"{temp.ToString("0.#", CultureInfo.InvariantCulture)}℃");
            }

            if (humidity >= 0)
            {
                parts.Add($"湿度{humidity}%");
            }

            var text = string.Join("，", parts);
            lock (WeatherLock)
            {
                _weatherCacheKey = cacheKey;
                _weatherCacheText = text;
                _weatherCacheUtc = DateTime.UtcNow;
            }

            return text;
        }
        catch
        {
            return null;
        }
    }

    private static (double Lat, double Lon, string Label)? PickBestPlace(
        JsonElement results,
        string country)
    {
        JsonElement? preferred = null;
        foreach (var item in results.EnumerateArray())
        {
            if (!string.IsNullOrWhiteSpace(country) &&
                item.TryGetProperty("country", out var countryEl))
            {
                var c = countryEl.GetString() ?? string.Empty;
                if (c.Contains(country, StringComparison.OrdinalIgnoreCase) ||
                    country.Contains(c, StringComparison.OrdinalIgnoreCase))
                {
                    preferred = item;
                    break;
                }
            }

            preferred ??= item;
        }

        if (preferred is null)
        {
            return null;
        }

        var p = preferred.Value;
        if (!p.TryGetProperty("latitude", out var latEl) ||
            !p.TryGetProperty("longitude", out var lonEl))
        {
            return null;
        }

        var name = p.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
        var admin = p.TryGetProperty("admin1", out var adminEl) ? adminEl.GetString() : null;
        var label = string.IsNullOrWhiteSpace(admin) ||
                    string.Equals(admin, name, StringComparison.OrdinalIgnoreCase)
            ? (name ?? "当地")
            : $"{name}（{admin}）";

        return (latEl.GetDouble(), lonEl.GetDouble(), label);
    }

    private static string DescribeWmoCode(int code) => code switch
    {
        0 => "晴朗",
        1 or 2 => "少云",
        3 => "多云",
        45 or 48 => "有雾",
        51 or 53 or 55 => "毛毛雨",
        56 or 57 => "冻毛毛雨",
        61 or 63 or 65 => "下雨",
        66 or 67 => "冻雨",
        71 or 73 or 75 or 77 => "下雪",
        80 or 81 or 82 => "阵雨",
        85 or 86 => "阵雪",
        95 => "雷阵雨",
        96 or 99 => "雷暴伴冰雹",
        _ => "天气一般",
    };
}
