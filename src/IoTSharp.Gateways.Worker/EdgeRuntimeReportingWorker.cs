using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using IoTSharp.Gateways.Application;
using IoTSharp.Gateways.Domain;
using Microsoft.Extensions.Options;

namespace IoTSharp.Gateways.Worker;

public sealed class EdgeReportingOptions
{
    public bool Enabled { get; set; } = true;
    public string RuntimeType { get; set; } = "gateway";
    public string? RuntimeName { get; set; }
    public string? InstanceId { get; set; }
    public string? BaseUrl { get; set; }
    public string? AccessToken { get; set; }
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public int RetryDelaySeconds { get; set; } = 5;
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class EdgeRuntimeReportingWorker : BackgroundService
{
    // IoTSharp ApiResult uses 10000 as the logical success code even when HTTP status is 200.
    private const int ApiSuccessCode = 10000;
    private const string DefaultPollingTaskName = "gateway-polling";
    private const int IPv4Priority = 0;
    private const int IPv6Priority = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEdgeTaskReceiptReporter _receiptReporter;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<EdgeRuntimeReportingWorker> _logger;
    private readonly EdgeReportingOptions _options;
    private readonly Process _currentProcess = Process.GetCurrentProcess();
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private readonly string _version;
    private readonly Stopwatch _uptime = Stopwatch.StartNew();

    private bool _registrationPending = true;
    private bool _capabilitiesPending = true;
    private string? _lastCapabilitiesSignature;
    private DateTimeOffset? _lastHeartbeatAt;
    private readonly Channel<EdgeTaskRequestPayload> _dispatchQueue = Channel.CreateUnbounded<EdgeTaskRequestPayload>();
    private readonly HashSet<Guid> _queuedTaskIds = [];
    private bool _missingConfigurationLogged;

    public EdgeRuntimeReportingWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IEdgeTaskReceiptReporter receiptReporter,
        IHostEnvironment hostEnvironment,
        IOptions<EdgeReportingOptions> options,
        ILogger<EdgeRuntimeReportingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _receiptReporter = receiptReporter;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
        _options = options.Value;
        _version = ResolveVersion();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("IoTSharp Edge reporting is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(Math.Max(_options.HeartbeatIntervalSeconds, 1));

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
                _registrationPending = true;
                _capabilitiesPending = true;
                delay = RetryDelay();
                _logger.LogError(exception, "IoTSharp Edge reporting iteration failed.");
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    private async Task<TimeSpan> ExecuteIterationAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGatewayRepository>();
        var driverCatalog = scope.ServiceProvider.GetRequiredService<DriverCatalogService>();
        var runtimeService = scope.ServiceProvider.GetRequiredService<GatewayRuntimeService>();

        var channels = await repository.GetChannelsAsync(cancellationToken);
        var devices = await repository.GetDevicesAsync(cancellationToken);
        var points = await repository.GetPointsAsync(cancellationToken);
        var pollingTasks = await repository.GetPollingTasksAsync(cancellationToken);
        var transformRules = await repository.GetTransformRulesAsync(cancellationToken);
        var uploadChannels = await repository.GetUploadChannelsAsync(cancellationToken);
        var uploadRoutes = await repository.GetUploadRoutesAsync(cancellationToken);

        var edgeTarget = ResolveEdgeTarget(uploadChannels);
        if (edgeTarget is null)
        {
            if (!_missingConfigurationLogged)
            {
                _missingConfigurationLogged = true;
                _logger.LogWarning("IoTSharp Edge reporting skipped because base URL or access token is not configured.");
            }
            return RetryDelay();
        }

        _missingConfigurationLogged = false;

        var snapshot = BuildRuntimeSnapshot(channels, devices, points, pollingTasks, uploadChannels, uploadRoutes);
        var capabilities = BuildCapabilities(driverCatalog, channels, points, pollingTasks, transformRules, uploadChannels, uploadRoutes);
        var capabilitiesSignature = ComputeCapabilitiesSignature(capabilities);

        if (!string.Equals(_lastCapabilitiesSignature, capabilitiesSignature, StringComparison.Ordinal))
        {
            _capabilitiesPending = true;
        }

        if (_registrationPending)
        {
            await PostAsync(edgeTarget, "Register", snapshot.Registration, cancellationToken);
            _registrationPending = false;
            _capabilitiesPending = true;
            _logger.LogInformation("Registered Gateway runtime to IoTSharp Edge with instance {InstanceId}.", snapshot.Registration.InstanceId);
        }

        if (_capabilitiesPending)
        {
            await PostAsync(edgeTarget, "Capabilities", capabilities, cancellationToken);
            _capabilitiesPending = false;
            _lastCapabilitiesSignature = capabilitiesSignature;
            _logger.LogInformation("Reported Gateway capabilities to IoTSharp Edge.");
        }

        if (!_lastHeartbeatAt.HasValue || DateTimeOffset.UtcNow - _lastHeartbeatAt.Value >= HeartbeatInterval())
        {
            await PostAsync(edgeTarget, "Heartbeat", snapshot.Heartbeat, cancellationToken);
            _lastHeartbeatAt = DateTimeOffset.UtcNow;
            _logger.LogDebug("Sent Gateway heartbeat to IoTSharp Edge.");
        }

        await EnqueuePendingDispatchesAsync(edgeTarget, cancellationToken);
        await DrainDispatchQueueAsync(runtimeService, edgeTarget, snapshot, points, cancellationToken);

        return NextDelay();
    }

    private async Task EnqueuePendingDispatchesAsync(
        EdgeTarget edgeTarget,
        CancellationToken cancellationToken)
    {
        var requests = await PullPendingDispatchAsync(edgeTarget, cancellationToken);
        if (requests.Count == 0)
        {
            return;
        }

        foreach (var request in requests)
        {
            if (request.TaskId == Guid.Empty || !_queuedTaskIds.Add(request.TaskId))
            {
                continue;
            }

            await _dispatchQueue.Writer.WriteAsync(request, cancellationToken);
        }
    }

    private async Task DrainDispatchQueueAsync(
        GatewayRuntimeService runtimeService,
        EdgeTarget edgeTarget,
        EdgeRuntimeSnapshot snapshot,
        IReadOnlyCollection<Point> points,
        CancellationToken cancellationToken)
    {
        while (_dispatchQueue.Reader.TryRead(out var request))
        {
            try
            {
                var deviceId = ResolveDeviceId(request.Address.TargetKey);
                if (deviceId == Guid.Empty)
                {
                    await _receiptReporter.ReportCompletedAsync(edgeTarget.BaseUrl, Guid.Empty, _options.RuntimeType, snapshot.Registration.InstanceId, request.TaskId, "Failed", "Gateway worker failed to resolve device id from dispatch target.", null, cancellationToken);
                    continue;
                }

                await _receiptReporter.ReportAcceptedAsync(edgeTarget.BaseUrl, edgeTarget.AccessToken, deviceId, _options.RuntimeType, snapshot.Registration.InstanceId, request.TaskId, cancellationToken);
                var result = await ExecuteDispatchAsync(runtimeService, request, deviceId, points, cancellationToken);
                await _receiptReporter.ReportCompletedAsync(edgeTarget.BaseUrl, deviceId, _options.RuntimeType, snapshot.Registration.InstanceId, request.TaskId, "Succeeded", result.Message, result.Payload, cancellationToken);
            }
            catch (Exception exception)
            {
                await _receiptReporter.ReportCompletedAsync(edgeTarget.BaseUrl, Guid.Empty, _options.RuntimeType, snapshot.Registration.InstanceId, request.TaskId, "Failed", exception.Message, new Dictionary<string, object> { ["exception"] = exception.GetType().Name }, cancellationToken);
            }
            finally
            {
                _queuedTaskIds.Remove(request.TaskId);
            }
        }
    }

    private async Task<List<EdgeTaskRequestPayload>> PullPendingDispatchAsync(EdgeTarget edgeTarget, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(EdgeRuntimeReportingWorker));
        client.BaseAddress = new Uri(edgeTarget.BaseUrl, UriKind.Absolute);
        using var response = await client.GetAsync($"api/EdgeTask/Dispatch/{Uri.EscapeDataString(edgeTarget.AccessToken)}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var apiResult = await response.Content.ReadFromJsonAsync<EdgeApiResult<List<EdgeTaskRequestPayload>>>(JsonOptions, cancellationToken);
        return apiResult is { Code: ApiSuccessCode, Data: not null } ? apiResult.Data : [];
    }

    private static Guid ResolveDeviceId(string targetKey)
    {
        var deviceIdSegment = targetKey.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return Guid.TryParse(deviceIdSegment, out var deviceId) ? deviceId : Guid.Empty;
    }

    private static async Task<DispatchExecutionResult> ExecuteDispatchAsync(
        GatewayRuntimeService runtimeService,
        EdgeTaskRequestPayload request,
        Guid deviceId,
        IReadOnlyCollection<Point> points,
        CancellationToken cancellationToken)
    {
        return request.TaskType switch
        {
            "HealthProbe" => new DispatchExecutionResult("Gateway runtime health probe completed.", new Dictionary<string, object>
            {
                ["checkedAtUtc"] = DateTime.UtcNow,
                ["taskType"] = request.TaskType
            }),
            "ConfigPush" => await ExecuteConfigPushAsync(runtimeService, request, deviceId, points, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported task type: {request.TaskType}")
        };
    }

    private static async Task<DispatchExecutionResult> ExecuteConfigPushAsync(
        GatewayRuntimeService runtimeService,
        EdgeTaskRequestPayload request,
        Guid deviceId,
        IReadOnlyCollection<Point> points,
        CancellationToken cancellationToken)
    {
        if (request.Payload is null || !request.Payload.TryGetValue("pointId", out var pointIdText) || !Guid.TryParse(pointIdText?.ToString(), out var pointId))
        {
            throw new InvalidOperationException("ConfigPush requires payload.pointId.");
        }

        var point = points.FirstOrDefault(item => item.Id == pointId && item.DeviceId == deviceId)
            ?? throw new InvalidOperationException($"Point '{pointId}' was not found on device '{deviceId}'.");

        request.Payload.TryGetValue("value", out var value);
        var writeResult = await runtimeService.ExecutePointWriteAsync(deviceId, point.Id, value, cancellationToken);
        return new DispatchExecutionResult(
            writeResult.Quality == QualityStatus.Good ? "Config push completed." : "Config push finished with degraded quality.",
            new Dictionary<string, object>
            {
                ["pointId"] = point.Id,
                ["address"] = point.Address,
                ["quality"] = writeResult.Quality.ToString(),
                ["errorMessage"] = writeResult.ErrorMessage ?? string.Empty
            });
    }

    private async Task PostAsync<TPayload>(EdgeTarget edgeTarget, string action, TPayload payload, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(EdgeRuntimeReportingWorker));
            client.BaseAddress = new Uri(edgeTarget.BaseUrl, UriKind.Absolute);
            using var response = await client.PostAsJsonAsync(
                $"api/Edge/{Uri.EscapeDataString(edgeTarget.AccessToken)}/{action}",
                payload,
                JsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var apiResult = await response.Content.ReadFromJsonAsync<EdgeApiResult>(JsonOptions, cancellationToken);
            if (apiResult is null)
            {
                throw new InvalidOperationException($"IoTSharp Edge {action} returned an empty response.");
            }

            if (apiResult.Code != ApiSuccessCode)
            {
                throw new InvalidOperationException($"IoTSharp Edge {action} failed with code {apiResult.Code}: {apiResult.Msg}");
            }
        }
        catch (Exception)
        {
            _registrationPending = true;
            _capabilitiesPending = true;
            throw;
        }
    }

    private EdgeRuntimeSnapshot BuildRuntimeSnapshot(
        IReadOnlyCollection<GatewayChannel> channels,
        IReadOnlyCollection<Device> devices,
        IReadOnlyCollection<Point> points,
        IReadOnlyCollection<PollingTask> pollingTasks,
        IReadOnlyCollection<UploadChannel> uploadChannels,
        IReadOnlyCollection<UploadRoute> uploadRoutes)
    {
        var hostName = ResolveHostName();
        var runtimeName = string.IsNullOrWhiteSpace(_options.RuntimeName) ? hostName : _options.RuntimeName.Trim();
        var instanceId = string.IsNullOrWhiteSpace(_options.InstanceId)
            ? CreateStableInstanceId(hostName, _hostEnvironment.ApplicationName, _hostEnvironment.ContentRootPath)
            : _options.InstanceId.Trim();
        var metadata = BuildMetadata();
        var metrics = BuildMetrics(channels, devices, points, pollingTasks, uploadChannels, uploadRoutes);
        var ipAddress = ResolveIpAddress();
        var uptimeSeconds = Math.Max(0L, (long)_uptime.Elapsed.TotalSeconds);
        var heartbeatAt = DateTimeOffset.UtcNow;

        return new EdgeRuntimeSnapshot(
            new EdgeRegistrationRequest(
                _options.RuntimeType,
                runtimeName,
                _version,
                instanceId,
                ResolvePlatform(),
                hostName,
                ipAddress,
                metadata),
            new EdgeHeartbeatRequest(
                heartbeatAt.UtcDateTime,
                "Running",
                true,
                uptimeSeconds,
                ipAddress,
                metrics));
    }

    private EdgeCapabilityReportRequest BuildCapabilities(
        DriverCatalogService driverCatalog,
        IReadOnlyCollection<GatewayChannel> channels,
        IReadOnlyCollection<Point> points,
        IReadOnlyCollection<PollingTask> pollingTasks,
        IReadOnlyCollection<TransformRule> transformRules,
        IReadOnlyCollection<UploadChannel> uploadChannels,
        IReadOnlyCollection<UploadRoute> uploadRoutes)
    {
        var protocols = driverCatalog.GetDrivers()
            .Select(driver => driver.Code)
            .Concat(channels.Where(channel => channel.Enabled).Select(channel => channel.DriverCode))
            .Concat(uploadChannels.Where(channel => channel.Enabled).Select(channel => channel.Protocol switch
            {
                UploadProtocol.Http => "http",
                UploadProtocol.IotSharpMqtt => "mqtt",
                _ => channel.Protocol.ToString()
            }))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var features = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "polling",
            "runtime-reporting"
        };

        if (points.Any(point => point.Enabled && point.AccessMode is PointAccessMode.Write or PointAccessMode.ReadWrite))
        {
            features.Add("point-write");
        }

        if (transformRules.Any(rule => rule.Enabled))
        {
            features.Add("transform-rules");
        }

        if (uploadRoutes.Any(route => route.Enabled))
        {
            features.Add("upload-routing");
        }

        if (uploadChannels.Any(channel => channel.Enabled && channel.Protocol == UploadProtocol.Http))
        {
            features.Add("http-upload");
        }

        if (uploadChannels.Any(channel => channel.Enabled && channel.Protocol == UploadProtocol.IotSharpMqtt))
        {
            features.Add("iotsharp-mqtt-upload");
        }

        var pollingTaskNames = pollingTasks.Where(task => task.Enabled)
            .Select(task => task.Name.Trim())
            .Where(task => !string.IsNullOrWhiteSpace(task))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(task => task, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (pollingTaskNames.Length == 0)
        {
            pollingTaskNames = [DefaultPollingTaskName];
        }

        return new EdgeCapabilityReportRequest(
            protocols,
            features.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            pollingTaskNames,
            null);
    }

    private EdgeTarget? ResolveEdgeTarget(IReadOnlyCollection<UploadChannel> uploadChannels)
    {
        var accessToken = FirstNonEmpty(
            _options.AccessToken,
            uploadChannels.Where(channel => channel.Enabled).SelectMany(channel =>
            {
                var settings = GatewayJson.Parse(channel.SettingsJson);
                return new[]
                {
                    GatewayJson.Get(settings, "accessToken"),
                    GatewayJson.Get(settings, "gatewayAccessToken"),
                    channel.Protocol == UploadProtocol.IotSharpMqtt ? GatewayJson.Get(settings, "username") : null
                };
            }));

        var baseUrl = FirstNonEmpty(
            _options.BaseUrl,
            uploadChannels.Where(channel => channel.Enabled).SelectMany(channel =>
            {
                var settings = GatewayJson.Parse(channel.SettingsJson);
                var configuredBaseUrl = GatewayJson.Get(settings, "edgeBaseUrl");
                var endpointBaseUrl = TryGetHttpBaseUrl(channel.Endpoint);
                return new[] { configuredBaseUrl, endpointBaseUrl };
            }));

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        return new EdgeTarget(accessToken.Trim(), NormalizeBaseUrl(baseUrl));
    }

    private Dictionary<string, string> BuildMetadata()
    {
        var metadata = new Dictionary<string, string>(_options.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            ["applicationName"] = _hostEnvironment.ApplicationName,
            ["environment"] = _hostEnvironment.EnvironmentName,
            ["startedAtUtc"] = _startedAt.ToString("O")
        };

        return metadata;
    }

    private Dictionary<string, object> BuildMetrics(
        IReadOnlyCollection<GatewayChannel> channels,
        IReadOnlyCollection<Device> devices,
        IReadOnlyCollection<Point> points,
        IReadOnlyCollection<PollingTask> pollingTasks,
        IReadOnlyCollection<UploadChannel> uploadChannels,
        IReadOnlyCollection<UploadRoute> uploadRoutes)
    {
        _currentProcess.Refresh();

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["workingSetBytes"] = _currentProcess.WorkingSet64,
            ["privateMemoryBytes"] = _currentProcess.PrivateMemorySize64,
            ["threadCount"] = _currentProcess.Threads.Count,
            ["gcHeapBytes"] = GC.GetTotalMemory(false),
            ["enabledChannelCount"] = channels.Count(channel => channel.Enabled),
            ["enabledDeviceCount"] = devices.Count(device => device.Enabled),
            ["enabledPointCount"] = points.Count(point => point.Enabled),
            ["enabledPollingTaskCount"] = pollingTasks.Count(task => task.Enabled),
            ["enabledUploadChannelCount"] = uploadChannels.Count(channel => channel.Enabled),
            ["enabledUploadRouteCount"] = uploadRoutes.Count(route => route.Enabled)
        };
    }

    private static string ResolveVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(EdgeRuntimeReportingWorker).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }

    private static string ResolvePlatform()
        => $"{RuntimeInformation.OSDescription.Trim()}-{RuntimeInformation.OSArchitecture}".Trim();

    private static string ResolveHostName()
        => Dns.GetHostName();

    private static string ResolveIpAddress()
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address)
            .Where(address =>
                !IPAddress.IsLoopback(address) &&
                address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
            .Select(address => address.ToString())
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetAddressPriority)
            .ToArray();

        return addresses.FirstOrDefault() ?? string.Empty;
    }

    private static string CreateStableInstanceId(string hostName, string applicationName, string contentRootPath)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{hostName}:{applicationName}:{contentRootPath}"));
        return Convert.ToHexString(bytes[..16]).ToLowerInvariant();
    }

    private static string ComputeCapabilitiesSignature(EdgeCapabilityReportRequest capabilities)
        => JsonSerializer.Serialize(capabilities, JsonOptions);

    private static string? TryGetHttpBaseUrl(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Scheme is "http" or "https"
            ? uri.GetLeftPart(UriPartial.Authority)
            : null;
    }

    private static string NormalizeBaseUrl(string baseUrl)
        => baseUrl.Trim().TrimEnd('/') + "/";

    private static int GetAddressPriority(string address)
        => address.Contains(':') ? IPv6Priority : IPv4Priority;

    private static string? FirstNonEmpty(string? first, IEnumerable<string?> others)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first;
        }

        return others.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private TimeSpan HeartbeatInterval()
        => TimeSpan.FromSeconds(Math.Max(_options.HeartbeatIntervalSeconds, 1));

    private TimeSpan RetryDelay()
        => TimeSpan.FromSeconds(Math.Max(_options.RetryDelaySeconds, 1));

    private TimeSpan NextDelay()
    {
        if (_registrationPending || _capabilitiesPending || !_lastHeartbeatAt.HasValue)
        {
            return RetryDelay();
        }

        var nextHeartbeatAt = _lastHeartbeatAt.Value + HeartbeatInterval();
        var delay = nextHeartbeatAt - DateTimeOffset.UtcNow;
        return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
    }

    private sealed record EdgeTarget(string AccessToken, string BaseUrl);

    private sealed record DispatchExecutionResult(string Message, Dictionary<string, object> Payload);

    private sealed record EdgeRuntimeSnapshot(EdgeRegistrationRequest Registration, EdgeHeartbeatRequest Heartbeat);

    private sealed record EdgeRegistrationRequest(
        string RuntimeType,
        string RuntimeName,
        string Version,
        string InstanceId,
        string Platform,
        string HostName,
        string IpAddress,
        Dictionary<string, string> Metadata);

    private sealed record EdgeHeartbeatRequest(
        DateTime Timestamp,
        string Status,
        bool Healthy,
        long UptimeSeconds,
        string IpAddress,
        Dictionary<string, object> Metrics);

    private sealed record EdgeCapabilityReportRequest(
        string[] Protocols,
        string[] Features,
        string[] Tasks,
        Dictionary<string, object>? Metadata);

    private sealed class EdgeApiResult
    {
        public int Code { get; set; }
        public string Msg { get; set; } = string.Empty;
    }

    private sealed class EdgeApiResult<T>
    {
        public int Code { get; set; }
        public string Msg { get; set; } = string.Empty;
        public T? Data { get; set; }
    }

    private sealed record EdgeTaskRequestPayload(
        string ContractVersion,
        Guid TaskId,
        string TaskType,
        EdgeTaskAddressPayload Address,
        Dictionary<string, object>? Payload,
        DateTime CreatedAt,
        DateTime? ExpiredAt);

    private sealed record EdgeTaskAddressPayload(
        string TargetType,
        string TargetKey,
        Guid? DeviceId,
        string RuntimeType,
        string InstanceId,
        string RuntimeName);
}
