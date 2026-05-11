using System.Net.Http.Json;
using System.Data;
using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Protocol;
using IoTSharp.Edge.Application;
using IoTSharp.Edge.Domain;
using Microsoft.Extensions.Logging;
using SonnetDB.Data;

namespace IoTSharp.Edge.Infrastructure.Uploads;

public sealed class HttpUploadTransport : IUploadTransport
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpUploadTransport(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public UploadProtocol Protocol => UploadProtocol.Http;

    public async Task UploadAsync(UploadChannel channel, UploadEnvelope envelope, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpUploadTransport));
        var settings = GatewayJson.Parse(channel.SettingsJson);
        if (settings.TryGetValue("headerName", out var headerName) && !string.IsNullOrWhiteSpace(headerName) && settings.TryGetValue("headerValue", out var headerValue) && !string.IsNullOrWhiteSpace(headerValue))
        {
            client.DefaultRequestHeaders.Remove(headerName);
            client.DefaultRequestHeaders.Add(headerName, headerValue);
        }

        if (settings.TryGetValue("bearerToken", out var bearerToken) && !string.IsNullOrWhiteSpace(bearerToken))
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        }

        var payload = string.IsNullOrWhiteSpace(envelope.PayloadTemplate)
            ? JsonSerializer.Serialize(envelope)
            : ApplyTemplate(envelope.PayloadTemplate, envelope);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(channel.Endpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    internal static string ApplyTemplate(string template, UploadEnvelope envelope)
        => template
            .Replace("{{device}}", envelope.DeviceName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{point}}", envelope.PointName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{target}}", envelope.Target, StringComparison.OrdinalIgnoreCase)
            .Replace("{{timestamp}}", envelope.Timestamp.ToString("O"), StringComparison.OrdinalIgnoreCase)
            .Replace("{{rawValue}}", Convert.ToString(envelope.RawValue, System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{{value}}", Convert.ToString(envelope.Value, System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
}

public sealed class IotSharpMqttUploadTransport : IUploadTransport
{
    public UploadProtocol Protocol => UploadProtocol.IotSharpMqtt;

    public async Task UploadAsync(UploadChannel channel, UploadEnvelope envelope, CancellationToken cancellationToken)
    {
        var settings = GatewayJson.Parse(channel.SettingsJson);
        var uri = new Uri(channel.Endpoint);
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(uri.Host, uri.Port > 0 ? uri.Port : 1883)
            .WithClientId(settings.TryGetValue("clientId", out var clientId) && !string.IsNullOrWhiteSpace(clientId) ? clientId : $"gateway-{Guid.NewGuid():N}");

        if (settings.TryGetValue("username", out var username) && !string.IsNullOrWhiteSpace(username))
        {
            builder = builder.WithCredentials(username, settings.TryGetValue("password", out var password) ? password : null);
        }

        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();
        await client.ConnectAsync(builder.Build(), cancellationToken);

        var topicPrefix = settings.TryGetValue("topicPrefix", out var configuredPrefix) && !string.IsNullOrWhiteSpace(configuredPrefix)
            ? configuredPrefix.TrimEnd('/')
            : "iotsharp/gateway";
        var topic = string.IsNullOrWhiteSpace(envelope.Target)
            ? $"{topicPrefix}/{Sanitize(envelope.DeviceName)}/{Sanitize(envelope.PointName)}"
            : envelope.Target;
        var payload = string.IsNullOrWhiteSpace(envelope.PayloadTemplate)
            ? JsonSerializer.Serialize(new
            {
                envelope.DeviceName,
                envelope.PointName,
                envelope.RawValue,
                envelope.Value,
                envelope.Timestamp,
                envelope.Quality
            })
            : HttpUploadTransport.ApplyTemplate(envelope.PayloadTemplate, envelope);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await client.PublishAsync(message, cancellationToken);
        await client.DisconnectAsync(cancellationToken: cancellationToken);
    }

    private static string Sanitize(string value)
        => value.Replace(' ', '_').ToLowerInvariant();
}

public sealed class IotSharpDeviceHttpUploadTransport : IUploadTransport
{
    private readonly IHttpClientFactory _httpClientFactory;

    public IotSharpDeviceHttpUploadTransport(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public UploadProtocol Protocol => UploadProtocol.IotSharpDeviceHttp;

    public async Task UploadAsync(UploadChannel channel, UploadEnvelope envelope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(channel.Endpoint))
        {
            throw new InvalidOperationException("IoTSharp 设备 HTTP 上传需要 endpoint。");
        }

        if (string.IsNullOrWhiteSpace(envelope.Target))
        {
            throw new InvalidOperationException("IoTSharp 设备 HTTP 上传需要 target。");
        }

        var client = _httpClientFactory.CreateClient(nameof(IotSharpDeviceHttpUploadTransport));
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [envelope.Target] = envelope.Value ?? envelope.RawValue
        };

        using var response = await client.PostAsJsonAsync(channel.Endpoint, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class SonnetDbUploadTransport : IUploadTransport
{
    private readonly ILogger<SonnetDbUploadTransport> _logger;

    public SonnetDbUploadTransport(ILogger<SonnetDbUploadTransport> logger)
    {
        _logger = logger;
    }

    public UploadProtocol Protocol => UploadProtocol.SonnetDb;

    public async Task UploadAsync(UploadChannel channel, UploadEnvelope envelope, CancellationToken cancellationToken)
    {
        var settings = GatewayJson.Parse(channel.SettingsJson);
        var connectionString = BuildConnectionString(channel.Endpoint, settings);
        var measurement = FirstNonEmpty(GatewayJson.Get(settings, "measurement"), "edge_modbus");
        var fieldName = SanitizeName(FirstNonEmpty(GatewayJson.Get(settings, "field"), "value"));
        var flushMode = FirstNonEmpty(GatewayJson.Get(settings, "flush"), "async");
        var payload = BuildJsonPointsPayload(measurement, fieldName, settings, envelope);

        await using var connection = new SndbConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.TableDirect;
        command.CommandText = payload;
        command.Parameters.Add(new SndbParameter("flush", flushMode));

        var written = await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation(
            "已将点位 {PointName} 从设备 {DeviceName} 写入 SonnetDB 测点集 {Measurement}，写入行数 {Written}。",
            envelope.PointName,
            envelope.DeviceName,
            measurement,
            written);
    }

    private static string BuildConnectionString(string endpoint, IReadOnlyDictionary<string, string?> settings)
    {
        var configured = FirstNonEmpty(GatewayJson.Get(settings, "connectionString"));
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("SonnetDB 需要 endpoint 或 settings.connectionString。");
        }

        var trimmedEndpoint = endpoint.Trim();
        if (trimmedEndpoint.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            return trimmedEndpoint;
        }

        var token = FirstNonEmpty(
            GatewayJson.Get(settings, "token"),
            GatewayJson.Get(settings, "bearerToken"),
            GatewayJson.Get(settings, "accessToken"));
        var tokenPart = string.IsNullOrWhiteSpace(token) ? string.Empty : $";Token={token}";

        if (trimmedEndpoint.StartsWith("sonnetdb+", StringComparison.OrdinalIgnoreCase))
        {
            return $"Data Source={trimmedEndpoint}{tokenPart}";
        }

        var database = FirstNonEmpty(GatewayJson.Get(settings, "database"), "metrics");
        return $"Data Source=sonnetdb+{trimmedEndpoint.TrimEnd('/')}/{database}{tokenPart}";
    }

    private static string BuildJsonPointsPayload(
        string measurement,
        string fieldName,
        IReadOnlyDictionary<string, string?> settings,
        UploadEnvelope envelope)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["device"] = envelope.DeviceName,
            ["point"] = envelope.PointName,
            ["target"] = string.IsNullOrWhiteSpace(envelope.Target) ? envelope.PointName : envelope.Target,
            ["quality"] = envelope.Quality.ToString()
        };

        if (settings.TryGetValue("site", out var site) && !string.IsNullOrWhiteSpace(site))
        {
            tags["site"] = site;
        }

        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [fieldName] = NormalizeFieldValue(envelope.Value ?? envelope.RawValue)
        };

        if (IsEnabled(settings, "includeRawValue", false) && envelope.RawValue is not null)
        {
            fields[SanitizeName(FirstNonEmpty(GatewayJson.Get(settings, "rawField"), "raw_value"))] = NormalizeFieldValue(envelope.RawValue);
        }

        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["m"] = measurement,
            ["points"] = new[]
            {
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["t"] = envelope.Timestamp.ToUnixTimeMilliseconds(),
                    ["tags"] = tags,
                    ["fields"] = fields
                }
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private static object? NormalizeFieldValue(object? value)
        => value switch
        {
            null => null,
            bool boolValue => boolValue,
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => Convert.ToDouble(value, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };

    private static bool IsEnabled(IReadOnlyDictionary<string, string?> settings, string key, bool defaultValue)
        => settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? bool.TryParse(value, out var parsed) ? parsed : value is "1" or "yes" or "on"
            : defaultValue;

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string SanitizeName(string value)
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
}

public sealed class UploadTransportRegistry : IUploadTransportRegistry
{
    private readonly IReadOnlyDictionary<UploadProtocol, IUploadTransport> _transports;

    public UploadTransportRegistry(IEnumerable<IUploadTransport> transports)
    {
        _transports = transports.ToDictionary(transport => transport.Protocol);
    }

    public IUploadTransport GetRequiredTransport(UploadProtocol protocol)
        => _transports.TryGetValue(protocol, out var transport)
            ? transport
            : throw new KeyNotFoundException($"上传传输“{protocol}”未注册。");
}
