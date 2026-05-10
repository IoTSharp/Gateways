using System.Globalization;
using System.Text.Json;
using IoTSharp.Edge.BasicRuntime;
using IoTSharp.Edge.Domain;
using MyBasicRuntime = IoTSharp.Edge.BasicRuntime.BasicRuntime;

namespace IoTSharp.Edge.Application;

public interface IGatewayRepository
{
    Task<IReadOnlyCollection<GatewayChannel>> GetChannelsAsync(CancellationToken cancellationToken);
    Task<GatewayChannel?> GetChannelAsync(Guid id, CancellationToken cancellationToken);
    Task SaveChannelAsync(GatewayChannel channel, CancellationToken cancellationToken);
    Task DeleteChannelAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Device>> GetDevicesAsync(CancellationToken cancellationToken);
    Task<Device?> GetDeviceAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Device>> GetDevicesByChannelAsync(Guid channelId, CancellationToken cancellationToken);
    Task SaveDeviceAsync(Device device, CancellationToken cancellationToken);
    Task DeleteDeviceAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Point>> GetPointsAsync(CancellationToken cancellationToken);
    Task<Point?> GetPointAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Point>> GetPointsByDeviceAsync(Guid deviceId, CancellationToken cancellationToken);
    Task SavePointAsync(Point point, CancellationToken cancellationToken);
    Task DeletePointAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PollingTask>> GetPollingTasksAsync(CancellationToken cancellationToken);
    Task<PollingTask?> GetPollingTaskAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PollingTask>> GetPollingTasksByDeviceAsync(Guid deviceId, CancellationToken cancellationToken);
    Task SavePollingTaskAsync(PollingTask task, CancellationToken cancellationToken);
    Task DeletePollingTaskAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TransformRule>> GetTransformRulesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TransformRule>> GetTransformRulesByPointAsync(Guid pointId, CancellationToken cancellationToken);
    Task SaveTransformRuleAsync(TransformRule rule, CancellationToken cancellationToken);
    Task DeleteTransformRuleAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UploadChannel>> GetUploadChannelsAsync(CancellationToken cancellationToken);
    Task<UploadChannel?> GetUploadChannelAsync(Guid id, CancellationToken cancellationToken);
    Task SaveUploadChannelAsync(UploadChannel channel, CancellationToken cancellationToken);
    Task DeleteUploadChannelAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UploadRoute>> GetUploadRoutesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<UploadRoute>> GetUploadRoutesByPointAsync(Guid pointId, CancellationToken cancellationToken);
    Task SaveUploadRouteAsync(UploadRoute route, CancellationToken cancellationToken);
    Task DeleteUploadRouteAsync(Guid id, CancellationToken cancellationToken);

    Task SaveWriteCommandAsync(WriteCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WriteCommand>> GetWriteCommandsAsync(CancellationToken cancellationToken);
    Task ReplaceConfigurationAsync(GatewayConfigurationSnapshot snapshot, CancellationToken cancellationToken);
}

public static class GatewayJson
{
    public static IReadOnlyDictionary<string, string?> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new Dictionary<string, string?>();
    }

    public static string Serialize(IReadOnlyDictionary<string, string?> values)
        => JsonSerializer.Serialize(values);

    public static decimal? GetDecimal(IReadOnlyDictionary<string, string?> values, string key)
        => values.TryGetValue(key, out var value) && decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    public static int? GetInt32(IReadOnlyDictionary<string, string?> values, string key)
        => values.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    public static string? Get(IReadOnlyDictionary<string, string?> values, string key)
        => values.TryGetValue(key, out var value) ? value : null;

    public static IReadOnlyCollection<Guid> ParseGuidArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<Guid>();
        }

        try
        {
            var values = JsonSerializer.Deserialize<Guid[]>(json);
            return values?.Where(value => value != Guid.Empty).Distinct().ToArray() ?? Array.Empty<Guid>();
        }
        catch (JsonException)
        {
            return Array.Empty<Guid>();
        }
    }
}

public sealed record GatewayConfigurationSnapshot(
    IReadOnlyCollection<GatewayChannel> Channels,
    IReadOnlyCollection<Device> Devices,
    IReadOnlyCollection<Point> Points,
    IReadOnlyCollection<PollingTask> PollingTasks,
    IReadOnlyCollection<TransformRule> TransformRules,
    IReadOnlyCollection<UploadChannel> UploadChannels,
    IReadOnlyCollection<UploadRoute> UploadRoutes);

public sealed class GatewayConfigurationService
{
    private readonly IGatewayRepository _repository;

    public GatewayConfigurationService(IGatewayRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyCollection<GatewayChannel>> GetChannelsAsync(CancellationToken cancellationToken) => _repository.GetChannelsAsync(cancellationToken);
    public Task SaveChannelAsync(GatewayChannel channel, CancellationToken cancellationToken) => _repository.SaveChannelAsync(channel, cancellationToken);
    public Task DeleteChannelAsync(Guid id, CancellationToken cancellationToken) => _repository.DeleteChannelAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<Device>> GetDevicesAsync(CancellationToken cancellationToken) => _repository.GetDevicesAsync(cancellationToken);
    public Task SaveDeviceAsync(Device device, CancellationToken cancellationToken) => _repository.SaveDeviceAsync(device, cancellationToken);
    public Task DeleteDeviceAsync(Guid id, CancellationToken cancellationToken) => _repository.DeleteDeviceAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<Point>> GetPointsAsync(CancellationToken cancellationToken) => _repository.GetPointsAsync(cancellationToken);
    public Task SavePointAsync(Point point, CancellationToken cancellationToken) => _repository.SavePointAsync(point, cancellationToken);
    public Task DeletePointAsync(Guid id, CancellationToken cancellationToken) => _repository.DeletePointAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<PollingTask>> GetPollingTasksAsync(CancellationToken cancellationToken) => _repository.GetPollingTasksAsync(cancellationToken);
    public Task SavePollingTaskAsync(PollingTask task, CancellationToken cancellationToken) => _repository.SavePollingTaskAsync(task, cancellationToken);
    public Task DeletePollingTaskAsync(Guid id, CancellationToken cancellationToken) => _repository.DeletePollingTaskAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<TransformRule>> GetTransformRulesAsync(CancellationToken cancellationToken) => _repository.GetTransformRulesAsync(cancellationToken);
    public Task SaveTransformRuleAsync(TransformRule rule, CancellationToken cancellationToken) => _repository.SaveTransformRuleAsync(rule, cancellationToken);
    public Task DeleteTransformRuleAsync(Guid id, CancellationToken cancellationToken) => _repository.DeleteTransformRuleAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<UploadChannel>> GetUploadChannelsAsync(CancellationToken cancellationToken) => _repository.GetUploadChannelsAsync(cancellationToken);
    public Task SaveUploadChannelAsync(UploadChannel channel, CancellationToken cancellationToken) => _repository.SaveUploadChannelAsync(channel, cancellationToken);
    public Task DeleteUploadChannelAsync(Guid id, CancellationToken cancellationToken) => _repository.DeleteUploadChannelAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<UploadRoute>> GetUploadRoutesAsync(CancellationToken cancellationToken) => _repository.GetUploadRoutesAsync(cancellationToken);
    public Task SaveUploadRouteAsync(UploadRoute route, CancellationToken cancellationToken) => _repository.SaveUploadRouteAsync(route, cancellationToken);
    public Task DeleteUploadRouteAsync(Guid id, CancellationToken cancellationToken) => _repository.DeleteUploadRouteAsync(id, cancellationToken);
}

public sealed class DriverCatalogService
{
    private readonly IDeviceDriverRegistry _registry;

    public DriverCatalogService(IDeviceDriverRegistry registry)
    {
        _registry = registry;
    }

    public IReadOnlyCollection<DriverDefinition> GetDrivers()
        => _registry.GetMetadata()
            .Select(metadata => new DriverDefinition
            {
                Code = metadata.Code,
                DriverType = metadata.DriverType,
                DisplayName = metadata.DisplayName,
                Description = metadata.Description,
                SupportsRead = metadata.SupportsRead,
                SupportsWrite = metadata.SupportsWrite,
                SupportsBatchRead = metadata.SupportsBatchRead,
                SupportsBatchWrite = metadata.SupportsBatchWrite,
                ConnectionSettings = metadata.ConnectionSettings,
                RiskLevel = metadata.RiskLevel
            })
            .ToArray();
}

public sealed class ValueTransformationService
{
    private readonly MyBasicRuntime _basicRuntime;

    public ValueTransformationService()
        : this(new MyBasicRuntime())
    {
    }

    public ValueTransformationService(MyBasicRuntime basicRuntime)
    {
        _basicRuntime = basicRuntime;
    }

    public object? Apply(object? rawValue, IReadOnlyCollection<TransformRule> rules)
    {
        object? current = rawValue;
        foreach (var rule in rules.Where(x => x.Enabled).OrderBy(x => x.SortOrder))
        {
            current = ApplyRule(current, rule);
        }

        return current;
    }

    private object? ApplyRule(object? value, TransformRule rule)
    {
        if (value is null)
        {
            return null;
        }

        var arguments = GatewayJson.Parse(rule.ArgumentsJson);
        return rule.Kind switch
        {
            TransformationKind.Scale => Scale(value, GatewayJson.GetDecimal(arguments, "factor") ?? 1m),
            TransformationKind.Offset => Offset(value, GatewayJson.GetDecimal(arguments, "offset") ?? 0m),
            TransformationKind.Cast => Cast(value, GatewayJson.Get(arguments, "type")),
            TransformationKind.BitExtract => BitExtract(value, GatewayJson.GetInt32(arguments, "index") ?? 0),
            TransformationKind.EnumMap => arguments.TryGetValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, out var mapped) ? mapped : value,
            TransformationKind.Expression => ApplyExpression(value, arguments),
            _ => value
        };
    }

    private object? ApplyExpression(object value, IReadOnlyDictionary<string, string?> arguments)
    {
        var code = GatewayJson.Get(arguments, "expression")
            ?? GatewayJson.Get(arguments, "script")
            ?? GatewayJson.Get(arguments, "code");
        if (string.IsNullOrWhiteSpace(code))
        {
            return value;
        }

        var variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["value"] = value,
            ["raw"] = value,
            ["x"] = value
        };

        foreach (var pair in arguments)
        {
            if (!IsExpressionKey(pair.Key))
            {
                variables[pair.Key] = pair.Value;
            }
        }

        var options = new BasicRuntimeOptions
        {
            MaxStatements = 10_000,
            MaxLoopIterations = 10_000
        };

        if (LooksLikeScript(code))
        {
            var result = _basicRuntime.Execute(code, variables, options);
            if (result.ReturnValue is not null)
            {
                return result.ReturnValue;
            }

            if (result.Variables.TryGetValue("result", out var transformed))
            {
                return transformed;
            }

            return result.Variables.TryGetValue("value", out var updatedValue) ? updatedValue : value;
        }

        return _basicRuntime.Evaluate(code, variables, options);
    }

    private static object? Scale(object value, decimal factor)
        => TryGetDecimal(value, out var decimalValue) ? decimalValue * factor : value;

    private static object? Offset(object value, decimal offset)
        => TryGetDecimal(value, out var decimalValue) ? decimalValue + offset : value;

    private static object? BitExtract(object value, int index)
    {
        if (!TryGetDecimal(value, out var decimalValue))
        {
            return value;
        }

        var integerValue = (long)decimalValue;
        return (integerValue & (1L << index)) != 0;
    }

    private static object? Cast(object value, string? targetType)
        => targetType?.ToLowerInvariant() switch
        {
            "string" => Convert.ToString(value, CultureInfo.InvariantCulture),
            "boolean" => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
            "byte" => Convert.ToByte(value, CultureInfo.InvariantCulture),
            "int16" => Convert.ToInt16(value, CultureInfo.InvariantCulture),
            "uint16" => Convert.ToUInt16(value, CultureInfo.InvariantCulture),
            "int32" => Convert.ToInt32(value, CultureInfo.InvariantCulture),
            "uint32" => Convert.ToUInt32(value, CultureInfo.InvariantCulture),
            "int64" => Convert.ToInt64(value, CultureInfo.InvariantCulture),
            "uint64" => Convert.ToUInt64(value, CultureInfo.InvariantCulture),
            "float" => Convert.ToSingle(value, CultureInfo.InvariantCulture),
            "double" => Convert.ToDouble(value, CultureInfo.InvariantCulture),
            _ => value
        };

    private static bool IsExpressionKey(string key)
        => string.Equals(key, "expression", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "script", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "code", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "value", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeScript(string code)
    {
        if (code.Contains('\n') || code.Contains('\r'))
        {
            return true;
        }

        var trimmed = code.TrimStart();
        var firstSpace = trimmed.IndexOfAny([' ', '\t', '(']);
        var firstWord = firstSpace >= 0 ? trimmed[..firstSpace] : trimmed;
        return firstWord.Equals("LET", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("IF", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("FOR", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("WHILE", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("DO", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("DEF", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("DIM", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("RETURN", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("PRINT", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("INPUT", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("GOTO", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("GOSUB", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("END", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetDecimal(object value, out decimal decimalValue)
    {
        switch (value)
        {
            case byte byteValue:
                decimalValue = byteValue;
                return true;
            case short shortValue:
                decimalValue = shortValue;
                return true;
            case ushort ushortValue:
                decimalValue = ushortValue;
                return true;
            case int intValue:
                decimalValue = intValue;
                return true;
            case uint uintValue:
                decimalValue = uintValue;
                return true;
            case long longValue:
                decimalValue = longValue;
                return true;
            case ulong ulongValue:
                decimalValue = ulongValue;
                return true;
            case float floatValue:
                decimalValue = (decimal)floatValue;
                return true;
            case double doubleValue:
                decimalValue = (decimal)doubleValue;
                return true;
            case decimal directValue:
                decimalValue = directValue;
                return true;
            default:
                return decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out decimalValue);
        }
    }
}

public sealed record GatewayExecutionReport(Guid TaskId, string TaskName, int SuccessCount, int FailureCount);

public sealed class GatewayRuntimeService
{
    private const string GatewayPollingScript = """
        report = EDGE_GATEWAY_POLLING_TASK(context)
        return report

        def EDGE_GATEWAY_POLLING_TASK(context)
          report = dict()
          report("successCount") = 0
          report("failureCount") = 0

          channel = context("channel")
          device = context("device")
          points = context("points")

          for pointIndex = 0 to len(points) - 1
            point = points(pointIndex)
            read = EDGE_DRIVER_READ(channel("driverCode"), channel("connection"), point("address"), point("dataType"), point("length"), point("settings"))
            if read("success") = 0 then
              report("failureCount") = report("failureCount") + 1
            else
              transformed = EDGE_TRANSFORM_APPLY(read("rawValue"), point("transforms"))
              if transformed("success") = 0 then
                report("failureCount") = report("failureCount") + 1
              else
                routeFailures = 0
                routes = point("routes")
                for routeIndex = 0 to len(routes) - 1
                  route = routes(routeIndex)
                  envelope = dict()
                  envelope("deviceName") = device("name")
                  envelope("pointName") = point("name")
                  envelope("rawValue") = read("rawValue")
                  envelope("value") = transformed("value")
                  envelope("quality") = read("quality")
                  envelope("timestampUtc") = read("timestampUtc")
                  envelope("target") = route("target")
                  envelope("payloadTemplate") = route("payloadTemplate")

                  upload = EDGE_UPLOAD(route("protocol"), route("endpoint"), route("settings"), envelope)
                  if upload("success") = 0 then
                    routeFailures = routeFailures + 1
                  endif
                next

                if routeFailures > 0 then
                  report("failureCount") = report("failureCount") + 1
                else
                  report("successCount") = report("successCount") + 1
                endif
              endif
            endif
          next

          return report
        enddef
        """;

    private readonly IGatewayRepository _repository;
    private readonly IDeviceDriverRegistry _driverRegistry;
    private readonly IUploadTransportRegistry _uploadTransportRegistry;
    private readonly MyBasicRuntime _basicRuntime;
    private readonly ValueTransformationService _transformationService;

    public GatewayRuntimeService(
        IGatewayRepository repository,
        IDeviceDriverRegistry driverRegistry,
        IUploadTransportRegistry uploadTransportRegistry,
        MyBasicRuntime basicRuntime,
        ValueTransformationService transformationService)
    {
        _repository = repository;
        _driverRegistry = driverRegistry;
        _uploadTransportRegistry = uploadTransportRegistry;
        _basicRuntime = basicRuntime;
        _transformationService = transformationService;
    }

    public async Task<DriverReadResult> ExecuteReadAsync(string driverCode, IReadOnlyDictionary<string, string?> connectionSettings, DriverReadRequest request, CancellationToken cancellationToken)
    {
        var driver = _driverRegistry.GetRequiredDriver(driverCode);
        return await driver.ReadAsync(new DriverConnectionContext(driverCode, connectionSettings), request, cancellationToken);
    }

    public async Task<DriverWriteResult> ExecuteWriteAsync(string driverCode, IReadOnlyDictionary<string, string?> connectionSettings, DriverWriteRequest request, CancellationToken cancellationToken)
    {
        var driver = _driverRegistry.GetRequiredDriver(driverCode);
        return await driver.WriteAsync(new DriverConnectionContext(driverCode, connectionSettings), request, cancellationToken);
    }

    public async Task<GatewayExecutionReport> ExecutePollingTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _repository.GetPollingTaskAsync(taskId, cancellationToken) ?? throw new InvalidOperationException($"Polling task '{taskId}' was not found.");
        var device = await _repository.GetDeviceAsync(task.DeviceId, cancellationToken) ?? throw new InvalidOperationException($"Device '{task.DeviceId}' was not found.");
        var channel = await _repository.GetChannelAsync(device.ChannelId, cancellationToken) ?? throw new InvalidOperationException($"Channel '{device.ChannelId}' was not found.");
        var configuredPointIds = GatewayJson.ParseGuidArray(task.PointIdsJson).ToHashSet();
        var points = (await _repository.GetPointsByDeviceAsync(device.Id, cancellationToken))
            .Where(point => point.Enabled && point.AccessMode is PointAccessMode.Read or PointAccessMode.ReadWrite)
            .Where(point => configuredPointIds.Count == 0 || configuredPointIds.Contains(point.Id))
            .ToArray();

        if (points.Length == 0)
        {
            return new GatewayExecutionReport(task.Id, task.Name, 0, 0);
        }

        if (CanExecuteScriptedPolling())
        {
            return await ExecutePollingTaskScriptAsync(task, device, channel, points, cancellationToken);
        }

        return await ExecutePollingTaskDirectAsync(task, device, channel, points, cancellationToken);
    }

    private async Task<GatewayExecutionReport> ExecutePollingTaskScriptAsync(
        PollingTask task,
        Device device,
        GatewayChannel channel,
        IReadOnlyCollection<Point> points,
        CancellationToken cancellationToken)
    {
        var context = await BuildPollingScriptContextAsync(task, device, channel, points, cancellationToken);
        var result = _basicRuntime.Execute(
            GatewayPollingScript,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["context"] = context
            },
            new BasicRuntimeOptions
            {
                MaxStatements = 1_000_000,
                MaxLoopIterations = 1_000_000
            });

        var report = ExpectDictionary(result.ReturnValue, "Gateway polling script result");
        return new GatewayExecutionReport(
            task.Id,
            task.Name,
            ReadInt32(report, "successCount"),
            ReadInt32(report, "failureCount"));
    }

    private async Task<Dictionary<string, object?>> BuildPollingScriptContextAsync(
        PollingTask task,
        Device device,
        GatewayChannel channel,
        IReadOnlyCollection<Point> points,
        CancellationToken cancellationToken)
    {
        var pointPayloads = new List<Dictionary<string, object?>>();
        foreach (var point in points)
        {
            var transforms = await _repository.GetTransformRulesByPointAsync(point.Id, cancellationToken);
            var routes = await _repository.GetUploadRoutesByPointAsync(point.Id, cancellationToken);
            var routePayloads = new List<Dictionary<string, object?>>();

            foreach (var route in routes.Where(route => route.Enabled))
            {
                var uploadChannel = await _repository.GetUploadChannelAsync(route.UploadChannelId, cancellationToken);
                if (uploadChannel is null || !uploadChannel.Enabled)
                {
                    continue;
                }

                routePayloads.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = route.Id.ToString("D"),
                    ["uploadChannelId"] = uploadChannel.Id.ToString("D"),
                    ["protocol"] = uploadChannel.Protocol.ToString(),
                    ["endpoint"] = uploadChannel.Endpoint,
                    ["settings"] = ToObjectDictionary(GatewayJson.Parse(uploadChannel.SettingsJson)),
                    ["target"] = route.Target,
                    ["payloadTemplate"] = route.PayloadTemplate
                });
            }

            pointPayloads.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = point.Id.ToString("D"),
                ["name"] = point.Name,
                ["address"] = point.Address,
                ["dataType"] = point.DataType.ToString(),
                ["length"] = point.Length,
                ["settings"] = ToObjectDictionary(GatewayJson.Parse(point.SettingsJson)),
                ["transforms"] = transforms
                    .OrderBy(rule => rule.SortOrder)
                    .Select(rule => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["id"] = rule.Id.ToString("D"),
                        ["name"] = rule.Name,
                        ["kind"] = rule.Kind.ToString(),
                        ["sortOrder"] = rule.SortOrder,
                        ["enabled"] = rule.Enabled,
                        ["arguments"] = ToObjectDictionary(GatewayJson.Parse(rule.ArgumentsJson))
                    })
                    .Cast<object?>()
                    .ToArray(),
                ["routes"] = routePayloads.Cast<object?>().ToArray()
            });
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["task"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = task.Id.ToString("D"),
                ["name"] = task.Name,
                ["intervalSeconds"] = task.IntervalSeconds,
                ["triggerOnChange"] = task.TriggerOnChange,
                ["batchRead"] = task.BatchRead
            },
            ["device"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = device.Id.ToString("D"),
                ["name"] = device.Name,
                ["externalId"] = device.ExternalId,
                ["settings"] = ToObjectDictionary(GatewayJson.Parse(device.SettingsJson))
            },
            ["channel"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = channel.Id.ToString("D"),
                ["name"] = channel.Name,
                ["driverCode"] = channel.DriverCode,
                ["connection"] = ToObjectDictionary(MergeSettings(channel.ConnectionJson, device.SettingsJson))
            },
            ["points"] = pointPayloads.Cast<object?>().ToArray()
        };
    }

    private async Task<GatewayExecutionReport> ExecutePollingTaskDirectAsync(
        PollingTask task,
        Device device,
        GatewayChannel channel,
        IReadOnlyCollection<Point> points,
        CancellationToken cancellationToken)
    {
        var driver = _driverRegistry.GetRequiredDriver(channel.DriverCode);
        var connectionContext = new DriverConnectionContext(channel.DriverCode, MergeSettings(channel.ConnectionJson, device.SettingsJson));
        var successCount = 0;
        var failureCount = 0;

        foreach (var point in points)
        {
            var readRequest = new DriverReadRequest(point.Address, point.DataType, point.Length, GatewayJson.Parse(point.SettingsJson));
            var readResult = await driver.ReadAsync(connectionContext, readRequest, cancellationToken);
            if (readResult.Quality != QualityStatus.Good)
            {
                failureCount++;
                continue;
            }

            var transforms = await _repository.GetTransformRulesByPointAsync(point.Id, cancellationToken);
            var transformedValue = _transformationService.Apply(readResult.RawValue, transforms);
            var routes = await _repository.GetUploadRoutesByPointAsync(point.Id, cancellationToken);
            foreach (var route in routes.Where(x => x.Enabled))
            {
                var uploadChannel = await _repository.GetUploadChannelAsync(route.UploadChannelId, cancellationToken);
                if (uploadChannel is null || !uploadChannel.Enabled)
                {
                    continue;
                }

                var transport = _uploadTransportRegistry.GetRequiredTransport(uploadChannel.Protocol);
                var envelope = new UploadEnvelope(device.Name, point.Name, readResult.RawValue, transformedValue, readResult.Timestamp, readResult.Quality, route.Target, route.PayloadTemplate);
                await transport.UploadAsync(uploadChannel, envelope, cancellationToken);
            }

            successCount++;
        }

        return new GatewayExecutionReport(task.Id, task.Name, successCount, failureCount);
    }

    private bool CanExecuteScriptedPolling()
        => _basicRuntime.RegisteredExtensions.Any(extension => string.Equals(extension, "gateway-runtime", StringComparison.OrdinalIgnoreCase));

    public async Task<DriverWriteResult> ExecutePointWriteAsync(Guid deviceId, Guid pointId, object? value, CancellationToken cancellationToken)
    {
        var device = await _repository.GetDeviceAsync(deviceId, cancellationToken) ?? throw new InvalidOperationException($"Device '{deviceId}' was not found.");
        var point = await _repository.GetPointAsync(pointId, cancellationToken) ?? throw new InvalidOperationException($"Point '{pointId}' was not found.");
        var channel = await _repository.GetChannelAsync(device.ChannelId, cancellationToken) ?? throw new InvalidOperationException($"Channel '{device.ChannelId}' was not found.");
        var connectionSettings = MergeSettings(channel.ConnectionJson, device.SettingsJson);
        var driver = _driverRegistry.GetRequiredDriver(channel.DriverCode);
        var request = new DriverWriteRequest(point.Address, point.DataType, value, point.Length, GatewayJson.Parse(point.SettingsJson));
        var result = await driver.WriteAsync(new DriverConnectionContext(channel.DriverCode, connectionSettings), request, cancellationToken);

        await _repository.SaveWriteCommandAsync(new WriteCommand
        {
            DeviceId = deviceId,
            PointId = pointId,
            Address = point.Address,
            ValueJson = JsonSerializer.Serialize(value),
            RequestedAtUtc = DateTime.UtcNow,
            Status = result.Quality == QualityStatus.Good ? WriteCommandStatus.Succeeded : WriteCommandStatus.Failed,
            ErrorMessage = result.ErrorMessage ?? string.Empty
        }, cancellationToken);

        return result;
    }

    private static IReadOnlyDictionary<string, object?> ExpectDictionary(object? value, string description)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return readOnlyDictionary;
        }

        if (value is IDictionary<string, object?> mutableDictionary)
        {
            return new Dictionary<string, object?>(mutableDictionary, StringComparer.OrdinalIgnoreCase);
        }

        throw new InvalidOperationException($"{description} must return a dictionary.");
    }

    private static int ReadInt32(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var raw) || raw is null)
        {
            return 0;
        }

        return raw switch
        {
            int intValue => intValue,
            long longValue => checked((int)longValue),
            short shortValue => shortValue,
            byte byteValue => byteValue,
            decimal decimalValue => (int)decimalValue,
            double doubleValue => (int)doubleValue,
            float floatValue => (int)floatValue,
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ when int.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0
        };
    }

    private static Dictionary<string, object?> ToObjectDictionary(IReadOnlyDictionary<string, string?> values)
        => values.ToDictionary(
            pair => pair.Key,
            pair => (object?)pair.Value,
            StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, string?> MergeSettings(string firstJson, string secondJson)
    {
        var merged = new Dictionary<string, string?>(GatewayJson.Parse(firstJson), StringComparer.OrdinalIgnoreCase);
        foreach (var pair in GatewayJson.Parse(secondJson))
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }
}
