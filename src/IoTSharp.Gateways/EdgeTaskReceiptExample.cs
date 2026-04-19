using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace IoTSharp.Gateways;

public interface IEdgeTaskReceiptReporter
{
    Task ReportAcceptedAsync(string baseUrl, string accessToken, Guid deviceId, string runtimeType, string instanceId, Guid taskId, CancellationToken cancellationToken = default);
    Task ReportCompletedAsync(string baseUrl, Guid deviceId, string runtimeType, string instanceId, Guid taskId, string status, string message, Dictionary<string, object>? result, CancellationToken cancellationToken = default);
}

public sealed class EdgeTaskReceiptExample : IEdgeTaskReceiptReporter
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EdgeTaskReceiptExample> _logger;

    public EdgeTaskReceiptExample(IHttpClientFactory httpClientFactory, ILogger<EdgeTaskReceiptExample> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task ReportAcceptedAsync(string baseUrl, string accessToken, Guid deviceId, string runtimeType, string instanceId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(nameof(EdgeTaskReceiptExample));
        var payload = new
        {
            contractVersion = "edge-task-v1",
            taskId,
            targetType = "GatewayRuntime",
            targetKey = $"{deviceId}:{runtimeType}:{instanceId}",
            runtimeType,
            instanceId,
            status = "Accepted",
            message = "Gateway worker accepted platform dispatch.",
            reportedAt = DateTime.UtcNow,
            progress = 0,
            result = new Dictionary<string, object>(),
            metadata = new Dictionary<string, string>
            {
                ["source"] = "gateway-dispatch-loop"
            }
        };

        var response = await client.PostAsJsonAsync($"{baseUrl.TrimEnd('/')}/api/EdgeTask/Dispatch/{accessToken}/Accept", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Accepted edge task {TaskId} for {TargetKey}", taskId, payload.targetKey);
    }

    public async Task ReportCompletedAsync(string baseUrl, Guid deviceId, string runtimeType, string instanceId, Guid taskId, string status, string message, Dictionary<string, object>? result, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(nameof(EdgeTaskReceiptExample));
        var payload = new
        {
            contractVersion = "edge-task-v1",
            taskId,
            targetType = "GatewayRuntime",
            targetKey = $"{deviceId}:{runtimeType}:{instanceId}",
            runtimeType,
            instanceId,
            status,
            message,
            reportedAt = DateTime.UtcNow,
            progress = status == "Running" ? 50 : 100,
            result = result ?? new Dictionary<string, object>(),
            metadata = new Dictionary<string, string>
            {
                ["source"] = "gateway-dispatch-loop"
            }
        };

        var response = await client.PostAsJsonAsync($"{baseUrl.TrimEnd('/')}/api/EdgeTask/Receipt", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Reported edge task receipt {TaskId} for {TargetKey}, status {Status}", taskId, payload.targetKey, status);
    }
}
