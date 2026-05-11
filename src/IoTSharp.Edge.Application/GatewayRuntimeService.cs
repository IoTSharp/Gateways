namespace IoTSharp.Edge.Application;

/// <summary>
/// 网关运行时服务。
/// 负责驱动读写、轮询任务执行和上传路由编排。
/// </summary>
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
