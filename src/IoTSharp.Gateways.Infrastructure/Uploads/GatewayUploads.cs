using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Protocol;
using IoTSharp.Gateways.Application;
using IoTSharp.Gateways.Domain;

namespace IoTSharp.Gateways.Infrastructure.Uploads;

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
            : throw new KeyNotFoundException($"Upload transport '{protocol}' is not registered.");
}
