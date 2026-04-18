using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace IoTSharp.Gateways.Worker;

public interface IEdgeTaskReceiptReporter
{
    Task ReportAsync(string baseUrl, Guid deviceId, string runtimeType, string instanceId, Guid taskId, CancellationToken cancellationToken = default);
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

    public async Task ReportAsync(string baseUrl, Guid deviceId, string runtimeType, string instanceId, Guid taskId, CancellationToken cancellationToken = default)
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
            status = "Succeeded",
            message = "Gateway worker example finished successfully.",
            reportedAt = DateTime.UtcNow,
            progress = 100,
            result = new Dictionary<string, object>
            {
                ["example"] = true,
                ["worker"] = "IoTSharp.Gateways.Worker"
            },
            metadata = new Dictionary<string, string>
            {
                ["source"] = "gateway-example"
            }
        };

        var response = await client.PostAsJsonAsync($"{baseUrl.TrimEnd('/')}/api/EdgeTask/Receipt", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Reported example edge task receipt {TaskId} for {TargetKey}", taskId, payload.targetKey);
    }
}