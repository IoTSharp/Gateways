using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IoTSharp.Edge.Application;
using Microsoft.Extensions.Options;

namespace IoTSharp.Edge;

internal sealed class GatewayCollectionConfigurationWorker : BackgroundService
{
    private const int ApiSuccessCode = 10000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<EdgeReportingOptions> _optionsMonitor;
    private readonly LocalCollectionConfigurationService _localConfigurationService;
    private readonly CollectionConfigurationSyncState _syncState;
    private readonly ILogger<GatewayCollectionConfigurationWorker> _logger;

    private int? _appliedVersion;

    public GatewayCollectionConfigurationWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<EdgeReportingOptions> optionsMonitor,
        LocalCollectionConfigurationService localConfigurationService,
        CollectionConfigurationSyncState syncState,
        ILogger<GatewayCollectionConfigurationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _optionsMonitor = optionsMonitor;
        _localConfigurationService = localConfigurationService;
        _syncState = syncState;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = RetryDelay(_optionsMonitor.CurrentValue);

            try
            {
                delay = await ExecuteIterationAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var options = _optionsMonitor.CurrentValue;
                _syncState.MarkError(exception.Message, NormalizeBaseUrlOrNull(options.BaseUrl), !string.IsNullOrWhiteSpace(options.AccessToken));
                _logger.LogError(exception, "网关采集配置同步失败。");
                delay = RetryDelay(options);
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    private async Task<TimeSpan> ExecuteIterationAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        var normalizedBaseUrl = NormalizeBaseUrlOrNull(options.BaseUrl);
        var hasAccessToken = !string.IsNullOrWhiteSpace(options.AccessToken);

        if (!options.Enabled)
        {
            _syncState.MarkDisabled(normalizedBaseUrl, hasAccessToken);
            return RetryDelay(options);
        }

        if (string.IsNullOrWhiteSpace(normalizedBaseUrl) || !hasAccessToken)
        {
            _syncState.MarkWaitingBootstrap("在采集同步启动前，Bootstrap 配置必须提供 EdgeReporting.BaseUrl 和 EdgeReporting.AccessToken。", normalizedBaseUrl, hasAccessToken);
            return RetryDelay(options);
        }

        _syncState.MarkSyncing(normalizedBaseUrl, hasAccessToken);
        var configuration = await PullConfigurationAsync(normalizedBaseUrl!, options.AccessToken!, cancellationToken);

        var applied = false;
        if (_appliedVersion != configuration.Version)
        {
            await TryCacheConfigurationAsync(configuration, cancellationToken);
            var snapshot = GatewayCollectionConfigurationMapper.Map(configuration, options);
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IGatewayRepository>();
            await repository.ReplaceConfigurationAsync(snapshot, cancellationToken);
            _appliedVersion = configuration.Version;
            _localConfigurationService.MarkApplied(configuration.Version);
            applied = true;

            _logger.LogInformation(
                "已应用采集配置版本 {Version}，边缘节点 {EdgeNodeId}，共 {TaskCount} 个任务。",
                configuration.Version,
                configuration.EdgeNodeId,
                configuration.Tasks?.Count ?? 0);
        }

        _syncState.MarkSynced(configuration.Version, configuration.UpdatedAt, configuration.UpdatedBy, normalizedBaseUrl, hasAccessToken, applied);
        return SuccessDelay(options);
    }

    private async Task<EdgeCollectionConfigurationContract> PullConfigurationAsync(string normalizedBaseUrl, string accessToken, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(GatewayCollectionConfigurationWorker));
        client.BaseAddress = new Uri(normalizedBaseUrl, UriKind.Absolute);

        using var response = await client.GetAsync($"api/Edge/{Uri.EscapeDataString(accessToken)}/CollectionConfig", cancellationToken);
        response.EnsureSuccessStatusCode();

        var apiResult = await response.Content.ReadFromJsonAsync<EdgeApiResult<EdgeCollectionConfigurationContract>>(JsonOptions, cancellationToken);
        if (apiResult is null)
        {
            throw new InvalidOperationException("IoTSharp 返回了空的采集配置响应。");
        }

        if (apiResult.Code != ApiSuccessCode)
        {
            throw new InvalidOperationException($"IoTSharp 采集配置拉取失败，代码 {apiResult.Code}：{apiResult.Msg}");
        }

        return apiResult.Data ?? new EdgeCollectionConfigurationContract();
    }

    private async Task TryCacheConfigurationAsync(EdgeCollectionConfigurationContract configuration, CancellationToken cancellationToken)
    {
        try
        {
            await _localConfigurationService.CacheUpstreamAsync(configuration, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "缓存上游采集配置到本地失败。");
        }
    }

    private static string? NormalizeBaseUrlOrNull(string? baseUrl)
        => string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim().TrimEnd('/') + "/";

    private static TimeSpan RetryDelay(EdgeReportingOptions options)
        => TimeSpan.FromSeconds(Math.Max(options.RetryDelaySeconds, 1));

    private static TimeSpan SuccessDelay(EdgeReportingOptions options)
        => TimeSpan.FromSeconds(Math.Max(options.HeartbeatIntervalSeconds, 1));

    private sealed class EdgeApiResult<T>
    {
        public int Code { get; set; }
        public string Msg { get; set; } = string.Empty;
        public T? Data { get; set; }
    }
}
