using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DesktopPet.Core;

namespace DesktopPet.Services;

/// <summary>
/// Builds the user message for bubble AI calls: local time/date + location + optional weather.
/// Copy comes from <see cref="LocalizationService"/> (Locales/*.json).
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

    public static async Task<string> BuildBubbleUserPromptAsync(
        AiConfig config,
        CancellationToken cancellationToken = default)
    {
        config = AiConfig.Normalize(config);
        var L = LocalizationService.Instance;
        var now = DateTime.Now;
        var sb = new StringBuilder();

        var weekday = L.Get($"Ai.Weekday.{(int)now.DayOfWeek}", now.DayOfWeek.ToString());
        var dayPart = DescribeDayPart(now);
        var dateFmt = L.Get("Ai.Context.DateFormat", "yyyy-M-d");
        string dateText;
        try
        {
            dateText = now.ToString(dateFmt, CultureInfo.InvariantCulture);
        }
        catch
        {
            dateText = now.ToString("yyyy-M-d", CultureInfo.InvariantCulture);
        }

        var timeText = now.ToString("HH:mm", CultureInfo.InvariantCulture);

        sb.AppendLine(L.Get("Ai.Context.Header", "[Context]"));
        sb.AppendLine(Format(
            L.Get("Ai.Context.LocalTime", "Local time: {0} {1} {2} ({3})"),
            dateText,
            weekday,
            timeText,
            dayPart));
        sb.AppendLine(Format(
            L.Get("Ai.Context.Country", "Country/region: {0}"),
            config.Country));

        if (!string.IsNullOrWhiteSpace(config.City))
        {
            sb.AppendLine(Format(
                L.Get("Ai.Context.City", "City: {0}"),
                config.City));
            var weather = await TryGetWeatherBriefAsync(config.Country, config.City, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(weather))
            {
                sb.AppendLine(Format(
                    L.Get("Ai.Context.Weather", "Weather: {0}"),
                    weather));
            }
        }
        else
        {
            sb.AppendLine(L.Get("Ai.Context.CityUnset", "City: (not set)"));
        }

        sb.AppendLine();
        sb.AppendLine(L.Get(
            "Ai.Context.UserAsk",
            "Using the context above, say one short friendly line (max ~40 characters). Do not invent news headlines."));

        return sb.ToString().TrimEnd();
    }

    private static string DescribeDayPart(DateTime now)
    {
        var L = LocalizationService.Instance;
        var key = now.Hour switch
        {
            >= 5 and < 9 => "Ai.DayPart.Dawn",
            >= 9 and < 12 => "Ai.DayPart.Morning",
            >= 12 and < 14 => "Ai.DayPart.Noon",
            >= 14 and < 18 => "Ai.DayPart.Afternoon",
            >= 18 and < 23 => "Ai.DayPart.Evening",
            _ => "Ai.DayPart.Night",
        };
        return L.Get(key, key);
    }

    private static async Task<string?> TryGetWeatherBriefAsync(
        string country,
        string city,
        CancellationToken cancellationToken)
    {
        var lang = LocalizationService.Instance.CurrentLanguage;
        var cacheKey = $"{lang}|{country}|{city}";
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

            var L = LocalizationService.Instance;
            var temp = current.TryGetProperty("temperature_2m", out var t) ? t.GetDouble() : double.NaN;
            var code = current.TryGetProperty("weather_code", out var c) ? c.GetInt32() : -1;
            var humidity = current.TryGetProperty("relative_humidity_2m", out var h) ? h.GetInt32() : -1;

            var parts = new List<string> { label, DescribeWmoCode(code) };
            if (!double.IsNaN(temp))
            {
                parts.Add(Format(
                    L.Get("Weather.Temp", "{0}°C"),
                    temp.ToString("0.#", CultureInfo.InvariantCulture)));
            }

            if (humidity >= 0)
            {
                parts.Add(Format(L.Get("Weather.Humidity", "Humidity {0}%"), humidity));
            }

            var join = L.Get("Weather.Join", ", ");
            var text = string.Join(join, parts);
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

        var L = LocalizationService.Instance;
        var name = p.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
        var admin = p.TryGetProperty("admin1", out var adminEl) ? adminEl.GetString() : null;
        var local = L.Get("Weather.LocalArea", "local");
        var label = string.IsNullOrWhiteSpace(admin) ||
                    string.Equals(admin, name, StringComparison.OrdinalIgnoreCase)
            ? (name ?? local)
            : Format(L.Get("Weather.PlaceWithAdmin", "{0} ({1})"), name, admin);

        return (latEl.GetDouble(), lonEl.GetDouble(), label);
    }

    private static string DescribeWmoCode(int code)
    {
        var L = LocalizationService.Instance;
        var key = code switch
        {
            0 => "Weather.Clear",
            1 or 2 => "Weather.MainlyClear",
            3 => "Weather.Overcast",
            45 or 48 => "Weather.Fog",
            51 or 53 or 55 => "Weather.Drizzle",
            56 or 57 => "Weather.FreezingDrizzle",
            61 or 63 or 65 => "Weather.Rain",
            66 or 67 => "Weather.FreezingRain",
            71 or 73 or 75 or 77 => "Weather.Snow",
            80 or 81 or 82 => "Weather.RainShowers",
            85 or 86 => "Weather.SnowShowers",
            95 => "Weather.Thunderstorm",
            96 or 99 => "Weather.ThunderstormHail",
            _ => "Weather.Other",
        };
        return L.Get(key, key);
    }

    private static string Format(string template, params object?[] args)
    {
        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}
