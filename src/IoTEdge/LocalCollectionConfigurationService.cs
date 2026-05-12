using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IoTEdge.Application;
using IoTEdge.Domain;
using Microsoft.Extensions.Options;

namespace IoTEdge;

internal sealed class LocalCollectionConfigurationOptions
{
    public bool Enabled { get; set; } = true;

    public bool ApplyOnStartup { get; set; } = true;

    public bool CacheUpstreamConfigurations { get; set; } = true;

    public string? FilePath { get; set; }

    public string FileName { get; set; } = "local-collection.json";

    public string? TemplatePath { get; set; }

    public string DefaultUpdatedBy { get; set; } = "本地配置";
}

internal sealed record LocalCollectionConfigurationDocument(
    bool Exists,
    string FilePath,
    DateTime? LastWriteTimeUtc,
    bool Applied,
    EdgeCollectionConfigurationContract Configuration);

internal sealed class LocalCollectionConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHostEnvironment _hostEnvironment;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<LocalCollectionConfigurationOptions> _optionsMonitor;
    private readonly ILogger<LocalCollectionConfigurationService> _logger;
    private readonly object _stateLock = new();
    private int? _appliedVersion;

    public LocalCollectionConfigurationService(
        IHostEnvironment hostEnvironment,
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<LocalCollectionConfigurationOptions> optionsMonitor,
        ILogger<LocalCollectionConfigurationService> logger)
    {
        _hostEnvironment = hostEnvironment;
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public string ResolveFilePath()
    {
        var options = _optionsMonitor.CurrentValue;
        if (!string.IsNullOrWhiteSpace(options.FilePath))
        {
            return options.FilePath.Trim();
        }

        var fileName = string.IsNullOrWhiteSpace(options.FileName) ? "local-collection.json" : options.FileName.Trim();
        return Path.Combine(_hostEnvironment.ContentRootPath, fileName);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (!options.Enabled)
        {
            return;
        }

        var document = await EnsureDocumentAsync(cancellationToken);
        if (!options.ApplyOnStartup)
        {
            return;
        }

        await ApplyAsync(document.Configuration, "启动", cancellationToken);
        MarkApplied(document.Configuration.Version);
    }

    public async Task<LocalCollectionConfigurationDocument> GetAsync(CancellationToken cancellationToken)
    {
        var filePath = ResolveFilePath();
        if (!File.Exists(filePath))
        {
            return new LocalCollectionConfigurationDocument(
                false,
                filePath,
                null,
                false,
                await CreateTemplateConfigurationAsync(cancellationToken));
        }

        var configuration = await ReadConfigurationAsync(filePath, cancellationToken);
        var applied = IsApplied(configuration.Version);
        return new LocalCollectionConfigurationDocument(
            true,
            filePath,
            File.GetLastWriteTimeUtc(filePath),
            applied,
            configuration);
    }

    public async Task<LocalCollectionConfigurationDocument> SaveAsync(
        EdgeCollectionConfigurationContract configuration,
        bool apply,
        string? updatedBy,
        CancellationToken cancellationToken)
    {
        var filePath = ResolveFilePath();
        var current = await TryReadConfigurationAsync(filePath, cancellationToken);
        var normalized = NormalizeConfiguration(configuration, current?.Version ?? 0, updatedBy);
        GatewayCollectionConfigurationValidator.ValidateStructuralKeys(normalized);
        await WriteConfigurationAsync(filePath, normalized, cancellationToken);

        if (apply)
        {
            await ApplyAsync(normalized, "本地保存", cancellationToken);
            MarkApplied(normalized.Version);
        }

        return new LocalCollectionConfigurationDocument(
            true,
            filePath,
            File.GetLastWriteTimeUtc(filePath),
            IsApplied(normalized.Version),
            normalized);
    }

    public async Task<LocalCollectionConfigurationDocument> ApplyCurrentAsync(CancellationToken cancellationToken)
    {
        var document = await EnsureDocumentAsync(cancellationToken);
        await ApplyAsync(document.Configuration, "本地应用", cancellationToken);
        MarkApplied(document.Configuration.Version);

        return new LocalCollectionConfigurationDocument(
            true,
            document.FilePath,
            File.GetLastWriteTimeUtc(document.FilePath),
            true,
            document.Configuration);
    }

    public async Task<LocalCollectionConfigurationDocument> ResetAsync(CancellationToken cancellationToken)
    {
        var filePath = ResolveFilePath();
        var template = await CreateTemplateConfigurationAsync(cancellationToken);
        template = template with
        {
            Version = 1,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = _optionsMonitor.CurrentValue.DefaultUpdatedBy
        };

        await WriteConfigurationAsync(filePath, template, cancellationToken);
        await ApplyAsync(template, "重置", cancellationToken);
        MarkApplied(template.Version);

        return new LocalCollectionConfigurationDocument(
            true,
            filePath,
            File.GetLastWriteTimeUtc(filePath),
            true,
            template);
    }

    public async Task CacheUpstreamAsync(EdgeCollectionConfigurationContract configuration, CancellationToken cancellationToken)
    {
        if (!_optionsMonitor.CurrentValue.CacheUpstreamConfigurations)
        {
            return;
        }

        var filePath = ResolveFilePath();
        await WriteConfigurationAsync(filePath, configuration, cancellationToken);
    }

    public void MarkApplied(int version)
    {
        lock (_stateLock)
        {
            _appliedVersion = version;
        }
    }

    private async Task<LocalCollectionConfigurationDocument> EnsureDocumentAsync(CancellationToken cancellationToken)
    {
        var filePath = ResolveFilePath();
        if (File.Exists(filePath))
        {
            var existingConfiguration = await ReadConfigurationAsync(filePath, cancellationToken);
            return new LocalCollectionConfigurationDocument(true, filePath, File.GetLastWriteTimeUtc(filePath), IsApplied(existingConfiguration.Version), existingConfiguration);
        }

        var configuration = await CreateTemplateConfigurationAsync(cancellationToken);
        if (TryLoadTemplateOverride(out var template))
        {
            configuration = template;
        }

        configuration = NormalizeConfiguration(configuration, 0, _optionsMonitor.CurrentValue.DefaultUpdatedBy);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? _hostEnvironment.ContentRootPath);
        await WriteConfigurationAsync(filePath, configuration, cancellationToken);
        return new LocalCollectionConfigurationDocument(true, filePath, File.GetLastWriteTimeUtc(filePath), IsApplied(configuration.Version), configuration);
    }

    private async Task ApplyAsync(EdgeCollectionConfigurationContract configuration, string source, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGatewayRepository>();
        var snapshot = GatewayCollectionConfigurationMapper.Map(configuration, new EdgeReportingOptions());
        await repository.ReplaceConfigurationAsync(snapshot, cancellationToken);
        _logger.LogInformation("已从 {Source} 应用本地采集配置，版本 {Version}。", source, configuration.Version);
    }

    private async Task<EdgeCollectionConfigurationContract> ReadConfigurationAsync(string filePath, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return DeserializeConfiguration(json);
    }

    private async Task<EdgeCollectionConfigurationContract?> TryReadConfigurationAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        return await ReadConfigurationAsync(filePath, cancellationToken);
    }

    private async Task WriteConfigurationAsync(string filePath, EdgeCollectionConfigurationContract configuration, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? _hostEnvironment.ContentRootPath);
        var normalized = JsonSerializer.Serialize(configuration, JsonOptions);
        await File.WriteAllTextAsync(filePath, normalized, new UTF8Encoding(false), cancellationToken);
    }

    private EdgeCollectionConfigurationContract NormalizeConfiguration(
        EdgeCollectionConfigurationContract configuration,
        int currentVersion,
        string? updatedBy)
    {
        var normalizedUploads = NormalizeUploads(configuration.Uploads, configuration.Upload);
        var normalizedRoutes = NormalizeRoutes(configuration.UploadRoutes);
        var normalized = configuration with
        {
            EdgeNodeId = configuration.EdgeNodeId == Guid.Empty ? Guid.NewGuid() : configuration.EdgeNodeId,
            Version = Math.Max(configuration.Version, currentVersion + 1),
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = string.IsNullOrWhiteSpace(updatedBy)
                ? _optionsMonitor.CurrentValue.DefaultUpdatedBy
                : updatedBy.Trim(),
            Upload = normalizedUploads.FirstOrDefault(),
            Uploads = normalizedUploads,
            UploadRoutes = normalizedRoutes,
            Tasks = (configuration.Tasks ?? []).Select(NormalizeTask).ToArray()
        };

        return normalized;
    }

    private static IReadOnlyList<CollectionUploadContract> NormalizeUploads(
        IReadOnlyList<CollectionUploadContract>? uploads,
        CollectionUploadContract? legacyUpload)
    {
        var source = (uploads is { Count: >0 } ? uploads : legacyUpload is null ? [] : [legacyUpload]).ToArray();
        if (source.Length == 0)
        {
            return [];
        }

        return source.Select((upload, index) => NormalizeUpload(upload, index)).ToArray();
    }

    private static CollectionUploadContract NormalizeUpload(CollectionUploadContract upload, int index)
    {
        var protocol = string.IsNullOrWhiteSpace(upload.Protocol) ? "SonnetDb" : upload.Protocol.Trim();
        var displayName = string.IsNullOrWhiteSpace(upload.DisplayName)
            ? GetUploadProtocolDisplayName(protocol)
            : upload.DisplayName.Trim();
        var targetKey = string.IsNullOrWhiteSpace(upload.TargetKey)
            ? CreateUploadTargetKey(displayName, protocol, index)
            : upload.TargetKey.Trim();

        return upload with
        {
            TargetKey = targetKey,
            DisplayName = displayName,
            Protocol = protocol,
            Endpoint = upload.Endpoint?.Trim() ?? string.Empty,
            BatchSize = Math.Max(upload.BatchSize, 1),
            Settings = upload.Settings.HasValue
                ? upload.Settings
                : JsonSerializer.SerializeToElement(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), JsonOptions)
            };
    }

    private static IReadOnlyList<CollectionRouteContract> NormalizeRoutes(IReadOnlyList<CollectionRouteContract>? routes)
    {
        if (routes is not { Count: > 0 })
        {
            return [];
        }

        return routes.Select(NormalizeRoute).ToArray();
    }

    private static CollectionRouteContract NormalizeRoute(CollectionRouteContract route)
        => route with
        {
            TaskKey = route.TaskKey?.Trim() ?? string.Empty,
            DeviceKey = route.DeviceKey?.Trim() ?? string.Empty,
            PointKey = route.PointKey?.Trim() ?? string.Empty,
            UploadTargetKey = route.UploadTargetKey?.Trim() ?? string.Empty,
            TargetName = route.TargetName?.Trim() ?? string.Empty,
            PayloadTemplate = route.PayloadTemplate?.Trim() ?? string.Empty
        };

    private static CollectionTaskContract NormalizeTask(CollectionTaskContract task)
    {
        var connection = task.Connection ?? new CollectionConnectionContract();
        return task with
        {
            Connection = connection with
            {
                ProtocolOptions = connection.ProtocolOptions.HasValue
                    ? connection.ProtocolOptions
                    : JsonSerializer.SerializeToElement(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), JsonOptions)
            },
            Devices = (task.Devices ?? []).Select(NormalizeDevice).ToArray(),
            ReportPolicy = task.ReportPolicy ?? new ReportPolicyContract()
        };
    }

    private static CollectionDeviceContract NormalizeDevice(CollectionDeviceContract device)
    {
        var protocolOptions = device.ProtocolOptions;
        return device with
        {
            ProtocolOptions = protocolOptions.HasValue
                ? protocolOptions
                : JsonSerializer.SerializeToElement(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), JsonOptions),
            Points = (device.Points ?? []).Select(NormalizePoint).ToArray()
        };
    }

    private static CollectionPointContract NormalizePoint(CollectionPointContract point)
    {
        var mapping = point.Mapping ?? new PlatformMappingContract();
        return point with
        {
            Polling = point.Polling ?? new PollingPolicyContract(),
            Transforms = (point.Transforms ?? []).Select(NormalizeTransform).ToArray(),
            Mapping = mapping,
            ProtocolOptions = point.ProtocolOptions.HasValue
                ? point.ProtocolOptions
                : JsonSerializer.SerializeToElement(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), JsonOptions)
        };
    }

    private static ValueTransformContract NormalizeTransform(ValueTransformContract transform)
    {
        return transform with
        {
            Parameters = transform.Parameters.HasValue
                ? transform.Parameters
                : JsonSerializer.SerializeToElement(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), JsonOptions)
        };
    }

    private async Task<EdgeCollectionConfigurationContract> CreateTemplateConfigurationAsync(CancellationToken cancellationToken)
    {
        if (TryLoadTemplateOverride(out var template))
        {
            return template;
        }

        await Task.CompletedTask;
        return new EdgeCollectionConfigurationContract
        {
            ContractVersion = "edge-collection-v1",
            EdgeNodeId = Guid.Empty,
            Version = 1,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = _optionsMonitor.CurrentValue.DefaultUpdatedBy,
            Upload = new CollectionUploadContract
            {
                TargetKey = "sonnetdb-main",
                DisplayName = "SonnetDB 主目标",
                Protocol = "SonnetDb",
                Endpoint = string.Empty,
                Settings = JsonSerializer.SerializeToElement(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), JsonOptions),
                Enabled = true,
                BatchSize = 1,
                BufferingEnabled = false
            },
            Uploads =
            [
                new CollectionUploadContract
                {
                    TargetKey = "sonnetdb-main",
                    DisplayName = "SonnetDB 主目标",
                    Protocol = "SonnetDb",
                    Endpoint = string.Empty,
                    Settings = JsonSerializer.SerializeToElement(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), JsonOptions),
                    Enabled = true,
                    BatchSize = 1,
                    BufferingEnabled = false
                }
            ],
            UploadRoutes = [],
            Tasks = []
        };
    }

    private bool TryLoadTemplateOverride(out EdgeCollectionConfigurationContract configuration)
    {
        var templatePath = _optionsMonitor.CurrentValue.TemplatePath;
        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
        {
            configuration = new EdgeCollectionConfigurationContract();
            return false;
        }

        try
        {
            var json = File.ReadAllText(templatePath);
            configuration = DeserializeConfiguration(json);
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "从 {TemplatePath} 加载本地采集模板失败。", templatePath);
            configuration = new EdgeCollectionConfigurationContract();
            return false;
        }
    }

    private static EdgeCollectionConfigurationContract DeserializeConfiguration(string json)
    {
        var configuration = JsonSerializer.Deserialize<EdgeCollectionConfigurationContract>(json, JsonOptions);
        return configuration ?? new EdgeCollectionConfigurationContract();
    }

    private bool IsApplied(int version)
    {
        lock (_stateLock)
        {
            return _appliedVersion.HasValue && _appliedVersion.Value == version;
        }
    }

    private static string CreateUploadTargetKey(string displayName, string protocol, int index)
    {
        var normalizedDisplayName = NormalizeKey(displayName);
        if (!string.IsNullOrWhiteSpace(normalizedDisplayName))
        {
            return $"{normalizedDisplayName}-{index + 1}";
        }

        var normalizedProtocol = NormalizeKey(protocol);
        return !string.IsNullOrWhiteSpace(normalizedProtocol)
            ? $"{normalizedProtocol}-{index + 1}"
            : $"upload-target-{index + 1}";
    }

    private static string GetUploadProtocolDisplayName(string protocol)
    {
        return NormalizeKey(protocol) switch
        {
            "iotsharp" or "iotsharpdevicehttp" or "iotsharpmqtt" => "IoTSharp",
            "thingsboard" or "thingboard" => "ThingsBoard",
            "sonnetdb" or "sonnet" => "SonnetDB",
            "influxdb" or "influx" => "InfluxDB",
            _ => string.IsNullOrWhiteSpace(protocol) ? "上传目标" : protocol.Trim()
        };
    }

    private static string NormalizeKey(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
