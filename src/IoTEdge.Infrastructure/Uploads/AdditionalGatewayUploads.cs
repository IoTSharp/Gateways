using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IoTEdge.Application;
using IoTEdge.Domain;

namespace IoTEdge.Infrastructure.Uploads;

public sealed class IoTSharpUploadTransport : IUploadTransport
{
    private readonly IHttpClientFactory _httpClientFactory;

    public IoTSharpUploadTransport(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public UploadProtocol Protocol => UploadProtocol.IoTSharp;

    public async Task UploadAsync(UploadChannel channel, UploadEnvelope envelope, CancellationToken cancellationToken)
    {
        var settings = GatewayJson.Parse(channel.SettingsJson);
        var client = _httpClientFactory.CreateClient(nameof(IoTSharpUploadTransport));
        var endpoint = UploadEndpointHelper.ResolvePlatformEndpoint(
            channel.Endpoint,
            settings,
            "token",
            UploadEndpointHelper.NormalizeTargetKind(settings),
            "/api/Devices/{token}/Telemetry",
            "/api/Devices/{token}/Attributes",
            "IoTSharp");

        using var content = new StringContent(UploadPayloadHelper.BuildKeyValuePayload(envelope), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(endpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class ThingsBoardUploadTransport : IUploadTransport
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ThingsBoardUploadTransport(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public UploadProtocol Protocol => UploadProtocol.ThingsBoard;

    public async Task UploadAsync(UploadChannel channel, UploadEnvelope envelope, CancellationToken cancellationToken)
    {
        var settings = GatewayJson.Parse(channel.SettingsJson);
        var client = _httpClientFactory.CreateClient(nameof(ThingsBoardUploadTransport));
        var endpoint = UploadEndpointHelper.ResolvePlatformEndpoint(
            channel.Endpoint,
            settings,
            "token",
            UploadEndpointHelper.NormalizeTargetKind(settings),
            "/api/v1/{token}/telemetry",
            "/api/v1/{token}/attributes",
            "ThingsBoard");

        using var content = new StringContent(UploadPayloadHelper.BuildKeyValuePayload(envelope), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(endpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class InfluxDbUploadTransport : IUploadTransport
{
    private readonly IHttpClientFactory _httpClientFactory;

    public InfluxDbUploadTransport(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public UploadProtocol Protocol => UploadProtocol.InfluxDb;

    public async Task UploadAsync(UploadChannel channel, UploadEnvelope envelope, CancellationToken cancellationToken)
    {
        var settings = GatewayJson.Parse(channel.SettingsJson);
        var client = _httpClientFactory.CreateClient(nameof(InfluxDbUploadTransport));
        var payload = BuildLineProtocolPayload(settings, envelope);
        var requestUri = BuildWriteUri(channel.Endpoint, settings);

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(payload, Encoding.UTF8, "text/plain")
        };

        var token = UploadEndpointHelper.FirstNonEmpty(GatewayJson.Get(settings, "token"), GatewayJson.Get(settings, "accessToken"));
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Token", token);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string BuildLineProtocolPayload(IReadOnlyDictionary<string, string?> settings, UploadEnvelope envelope)
    {
        var measurement = UploadEndpointHelper.FirstNonEmpty(GatewayJson.Get(settings, "measurement"), "edge");
        var fieldName = SanitizeKey(UploadEndpointHelper.FirstNonEmpty(GatewayJson.Get(settings, "field"), "value"));
        var targetKind = UploadEndpointHelper.NormalizeTargetKind(settings);
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["device"] = envelope.DeviceName,
            ["point"] = envelope.PointName,
            ["target"] = string.IsNullOrWhiteSpace(envelope.Target) ? envelope.PointName : envelope.Target,
            ["quality"] = envelope.Quality.ToString(),
            ["target_kind"] = targetKind
        };

        if (settings.TryGetValue("site", out var site) && !string.IsNullOrWhiteSpace(site))
        {
            tags["site"] = site!;
        }

        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [fieldName] = NormalizeFieldValue(envelope.Value ?? envelope.RawValue)
        };

        if (UploadEndpointHelper.IsEnabled(settings, "includeRawValue", false) && envelope.RawValue is not null)
        {
            var rawField = SanitizeKey(UploadEndpointHelper.FirstNonEmpty(GatewayJson.Get(settings, "rawField"), "raw_value"));
            fields[rawField] = NormalizeFieldValue(envelope.RawValue);
        }

        var precision = NormalizePrecision(UploadEndpointHelper.FirstNonEmpty(GatewayJson.Get(settings, "precision"), "ms"));
        var timestamp = FormatTimestamp(envelope.Timestamp, precision);

        return string.Concat(
            EscapeMeasurement(measurement),
            ",",
            string.Join(
                ",",
                tags.Select(pair => $"{EscapeTagKey(pair.Key)}={EscapeTagValue(pair.Value)}")),
            " ",
            string.Join(
                ",",
                fields.Select(pair => $"{EscapeFieldKey(pair.Key)}={FormatFieldValue(pair.Value)}")),
            " ",
            timestamp);
    }

    private static string BuildWriteUri(string endpoint, IReadOnlyDictionary<string, string?> settings)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("InfluxDB 需要 endpoint。");
        }

        var trimmed = endpoint.Trim().TrimEnd('/');
        if (trimmed.Contains("/api/v2/write", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("/write", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var bucket = UploadEndpointHelper.FirstNonEmpty(GatewayJson.Get(settings, "bucket"));
        var database = UploadEndpointHelper.FirstNonEmpty(GatewayJson.Get(settings, "database"));
        var precision = NormalizePrecision(UploadEndpointHelper.FirstNonEmpty(GatewayJson.Get(settings, "precision"), "ms"));
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(bucket))
        {
            var org = UploadEndpointHelper.FirstNonEmpty(GatewayJson.Get(settings, "org"));
            if (!string.IsNullOrWhiteSpace(org))
            {
                parameters.Add($"org={Uri.EscapeDataString(org)}");
            }

            parameters.Add($"bucket={Uri.EscapeDataString(bucket)}");
            parameters.Add($"precision={Uri.EscapeDataString(precision)}");
            return $"{trimmed}/api/v2/write?{string.Join("&", parameters)}";
        }

        if (!string.IsNullOrWhiteSpace(database))
        {
            parameters.Add($"db={Uri.EscapeDataString(database)}");
            parameters.Add($"precision={Uri.EscapeDataString(precision)}");
            var retentionPolicy = UploadEndpointHelper.FirstNonEmpty(GatewayJson.Get(settings, "retentionPolicy"), GatewayJson.Get(settings, "rp"));
            if (!string.IsNullOrWhiteSpace(retentionPolicy))
            {
                parameters.Add($"rp={Uri.EscapeDataString(retentionPolicy)}");
            }

            return $"{trimmed}/write?{string.Join("&", parameters)}";
        }

        return $"{trimmed}/api/v2/write?precision={Uri.EscapeDataString(precision)}";
    }

    private static string NormalizePrecision(string precision)
        => precision.Trim().ToLowerInvariant() switch
        {
            "s" => "s",
            "us" => "us",
            "ms" => "ms",
            "ns" => "ns",
            _ => "ms"
        };

    private static string FormatTimestamp(DateTimeOffset timestamp, string precision)
    {
        var milliseconds = timestamp.ToUnixTimeMilliseconds();
        return precision switch
        {
            "s" => timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            "us" => (milliseconds * 1000L).ToString(CultureInfo.InvariantCulture),
            "ns" => (milliseconds * 1_000_000L).ToString(CultureInfo.InvariantCulture),
            _ => milliseconds.ToString(CultureInfo.InvariantCulture)
        };
    }

    private static string EscapeMeasurement(string value)
        => EscapeValue(value);

    private static string EscapeTagKey(string value)
        => EscapeValue(value);

    private static string EscapeTagValue(string value)
        => EscapeValue(value);

    private static string EscapeFieldKey(string value)
        => EscapeValue(value);

    private static string EscapeValue(string value)
        => value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace(",", @"\,", StringComparison.Ordinal)
            .Replace(" ", @"\ ", StringComparison.Ordinal)
            .Replace("=", @"\=", StringComparison.Ordinal);

    private static string SanitizeKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }

        var normalized = builder.ToString().Trim('_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "value";
        }

        return char.IsDigit(normalized[0]) ? $"_{normalized}" : normalized;
    }

    private static object? NormalizeFieldValue(object? value)
        => value switch
        {
            null => string.Empty,
            bool boolValue => boolValue,
            byte or sbyte or short or ushort or int or uint or long or ulong => value,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            decimal decimalValue => decimalValue,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

    private static string FormatFieldValue(object? value)
        => value switch
        {
            null => "\"\"",
            bool boolValue => boolValue ? "true" : "false",
            byte or sbyte or short or ushort or int or uint or long or ulong => Convert.ToString(value, CultureInfo.InvariantCulture) + "i",
            float floatValue => floatValue.ToString(CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture),
            decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
            string text => $"\"{text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
            _ => $"\"{Convert.ToString(value, CultureInfo.InvariantCulture)?.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) ?? string.Empty}\""
        };
}

internal static class UploadEndpointHelper
{
    public static string ResolvePlatformEndpoint(
        string endpoint,
        IReadOnlyDictionary<string, string?> settings,
        string tokenKey,
        string targetKind,
        string telemetryTemplate,
        string attributesTemplate,
        string platformName)
    {
        var trimmed = endpoint?.Trim().TrimEnd('/') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException($"{platformName} 需要 endpoint。");
        }

        if (HasKnownUploadPath(trimmed))
        {
            return trimmed;
        }

        var token = FirstNonEmpty(
            GatewayJson.Get(settings, tokenKey),
            GatewayJson.Get(settings, "accessToken"),
            GatewayJson.Get(settings, "deviceToken"));
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException($"{platformName} 需要 {tokenKey}。");
        }

        var suffix = string.Equals(targetKind, "attributes", StringComparison.OrdinalIgnoreCase)
            ? attributesTemplate
            : telemetryTemplate;
        return $"{trimmed}{suffix.Replace("{token}", Uri.EscapeDataString(token), StringComparison.OrdinalIgnoreCase)}";
    }

    public static string NormalizeTargetKind(IReadOnlyDictionary<string, string?> settings)
    {
        var targetKind = FirstNonEmpty(GatewayJson.Get(settings, "targetKind"), "telemetry");
        return string.Equals(targetKind, "attributes", StringComparison.OrdinalIgnoreCase) ? "attributes" : "telemetry";
    }

    public static bool IsEnabled(IReadOnlyDictionary<string, string?> settings, string key, bool defaultValue)
        => settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? bool.TryParse(value, out var parsed) ? parsed : value is "1" or "yes" or "on"
            : defaultValue;

    public static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static bool HasKnownUploadPath(string endpoint)
        => endpoint.Contains("/api/Devices/", StringComparison.OrdinalIgnoreCase)
            || endpoint.Contains("/api/v1/", StringComparison.OrdinalIgnoreCase)
            || endpoint.Contains("/api/v2/write", StringComparison.OrdinalIgnoreCase)
            || endpoint.Contains("/write", StringComparison.OrdinalIgnoreCase);
}

internal static class UploadPayloadHelper
{
    public static string BuildKeyValuePayload(UploadEnvelope envelope)
        => JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [string.IsNullOrWhiteSpace(envelope.Target) ? envelope.PointName : envelope.Target] = envelope.Value ?? envelope.RawValue
        });
}
