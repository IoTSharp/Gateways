using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IoTSharp.Edge.Application;
using IoTSharp.Edge.Domain;

namespace IoTSharp.Edge;

internal static class GatewayCollectionConfigurationMapper
{
    public static GatewayConfigurationSnapshot Map(EdgeCollectionConfigurationContract configuration, EdgeReportingOptions options)
    {
        if (configuration == null)
        {
            throw new InvalidOperationException("Collection configuration payload is required.");
        }

        var edgeNodeId = configuration.EdgeNodeId;
        var normalizedBaseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? string.Empty : NormalizeBaseUrl(options.BaseUrl);
        var accessToken = options.AccessToken ?? string.Empty;
        var telemetryUploadChannelId = CreateDeterministicGuid(edgeNodeId, "upload-channel", "telemetry");
        var attributeUploadChannelId = CreateDeterministicGuid(edgeNodeId, "upload-channel", "attribute");

        var channels = new List<GatewayChannel>();
        var devices = new List<Device>();
        var points = new List<Point>();
        var pollingTasks = new List<PollingTask>();
        var transformRules = new List<TransformRule>();
        var uploadRoutes = new List<UploadRoute>();
        var requiredUploadProtocols = new HashSet<GatewayCollectionTargetType>();

        foreach (var task in configuration.Tasks ?? [])
        {
            if (string.IsNullOrWhiteSpace(task.TaskKey))
            {
                throw new InvalidOperationException("task.taskKey is required.");
            }

            var channelId = CreateDeterministicGuid(edgeNodeId, "channel", task.TaskKey);
            channels.Add(new GatewayChannel
            {
                Id = channelId,
                DriverCode = ResolveDriverCode(task.Protocol),
                Name = string.IsNullOrWhiteSpace(task.Connection?.ConnectionName) ? task.TaskKey : task.Connection.ConnectionName,
                Description = $"Platform collection task {task.TaskKey}",
                ConnectionJson = GatewayJson.Serialize(BuildConnectionSettings(task)),
                Enabled = true
            });

            foreach (var deviceContract in task.Devices ?? [])
            {
                if (string.IsNullOrWhiteSpace(deviceContract.DeviceKey))
                {
                    throw new InvalidOperationException($"task '{task.TaskKey}' contains a device without deviceKey.");
                }

                var deviceId = CreateDeterministicGuid(edgeNodeId, "device", task.TaskKey, deviceContract.DeviceKey);
                var devicePointStates = new List<DevicePointState>();

                foreach (var pointContract in deviceContract.Points ?? [])
                {
                    if (string.IsNullOrWhiteSpace(pointContract.PointKey) ||
                        string.IsNullOrWhiteSpace(pointContract.PointName) ||
                        string.IsNullOrWhiteSpace(pointContract.Address))
                    {
                        throw new InvalidOperationException($"task '{task.TaskKey}', device '{deviceContract.DeviceKey}' contains an invalid point definition.");
                    }

                    if (pointContract.Mapping == null || string.IsNullOrWhiteSpace(pointContract.Mapping.TargetName))
                    {
                        throw new InvalidOperationException($"task '{task.TaskKey}', point '{pointContract.PointKey}' requires mapping.targetName.");
                    }

                    var pointId = CreateDeterministicGuid(edgeNodeId, "point", task.TaskKey, deviceContract.DeviceKey, pointContract.PointKey);
                    var pointEnabled = deviceContract.Enabled;
                    var point = new Point
                    {
                        Id = pointId,
                        DeviceId = deviceId,
                        Name = pointContract.PointName,
                        Address = pointContract.Address,
                        DataType = ResolveDataType(pointContract.RawValueType),
                        AccessMode = ResolveAccessMode(pointContract.Mapping.TargetType),
                        Length = ResolvePointLength(pointContract),
                        SettingsJson = GatewayJson.Serialize(BuildPointSettings(task, deviceContract, pointContract)),
                        Enabled = pointEnabled
                    };

                    points.Add(point);
                    devicePointStates.Add(new DevicePointState(point.Id, pointEnabled, pointContract));

                    foreach (var transform in BuildTransformRules(edgeNodeId, task, deviceContract, pointContract, pointId))
                    {
                        transformRules.Add(transform);
                    }

                    var targetType = NormalizeTargetType(pointContract.Mapping.TargetType, pointContract.PointKey);
                    requiredUploadProtocols.Add(targetType);
                    uploadRoutes.Add(new UploadRoute
                    {
                        Id = CreateDeterministicGuid(edgeNodeId, "upload-route", task.TaskKey, deviceContract.DeviceKey, pointContract.PointKey, pointContract.Mapping.TargetType.ToString()),
                        PointId = pointId,
                        UploadChannelId = targetType == GatewayCollectionTargetType.Telemetry ? telemetryUploadChannelId : attributeUploadChannelId,
                        PayloadTemplate = string.Empty,
                        Target = pointContract.Mapping.TargetName,
                        Enabled = pointEnabled
                    });
                }

                devices.Add(new Device
                {
                    Id = deviceId,
                    ChannelId = channelId,
                    Name = string.IsNullOrWhiteSpace(deviceContract.DeviceName) ? deviceContract.DeviceKey : deviceContract.DeviceName,
                    ExternalId = string.IsNullOrWhiteSpace(deviceContract.ExternalKey) ? deviceContract.DeviceKey : deviceContract.ExternalKey,
                    PollingIntervalSeconds = ResolveDevicePollingIntervalSeconds(devicePointStates.Select(item => item.Contract)),
                    SettingsJson = GatewayJson.Serialize(BuildDeviceSettings(deviceContract)),
                    Enabled = deviceContract.Enabled
                });

                foreach (var pollingTask in BuildPollingTasks(edgeNodeId, task, deviceContract, deviceId, devicePointStates))
                {
                    pollingTasks.Add(pollingTask);
                }
            }
        }

        var uploadChannels = BuildUploadChannels(
            requiredUploadProtocols,
            telemetryUploadChannelId,
            attributeUploadChannelId,
            normalizedBaseUrl,
            accessToken,
            configuration.EdgeNodeId,
            configuration.Upload);

        return new GatewayConfigurationSnapshot(
            channels.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            devices.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            points.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            pollingTasks.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            transformRules.OrderBy(item => item.SortOrder).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            uploadChannels.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            uploadRoutes.OrderBy(item => item.Target, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static IReadOnlyCollection<UploadChannel> BuildUploadChannels(
        IReadOnlyCollection<GatewayCollectionTargetType> requiredTargetTypes,
        Guid telemetryUploadChannelId,
        Guid attributeUploadChannelId,
        string normalizedBaseUrl,
        string accessToken,
        Guid edgeNodeId,
        CollectionUploadContract? configuredUpload)
    {
        var uploadChannels = new List<UploadChannel>();
        var uploadProtocol = ResolveUploadProtocol(configuredUpload?.Protocol);
        var uploadSettings = BuildUploadSettings(configuredUpload, normalizedBaseUrl, accessToken, edgeNodeId);

        if (requiredTargetTypes.Contains(GatewayCollectionTargetType.Telemetry))
        {
            uploadChannels.Add(new UploadChannel
            {
                Id = telemetryUploadChannelId,
                Name = uploadProtocol == UploadProtocol.SonnetDb ? "SonnetDB Telemetry" : "IoTSharp Telemetry",
                Protocol = uploadProtocol,
                Endpoint = ResolveUploadEndpoint(configuredUpload, normalizedBaseUrl, accessToken, uploadProtocol, "telemetry"),
                SettingsJson = GatewayJson.Serialize(uploadSettings),
                BatchSize = 1,
                BufferingEnabled = false,
                Enabled = true
            });
        }

        if (requiredTargetTypes.Contains(GatewayCollectionTargetType.Attribute))
        {
            uploadChannels.Add(new UploadChannel
            {
                Id = attributeUploadChannelId,
                Name = uploadProtocol == UploadProtocol.SonnetDb ? "SonnetDB Attributes" : "IoTSharp Attributes",
                Protocol = uploadProtocol,
                Endpoint = ResolveUploadEndpoint(configuredUpload, normalizedBaseUrl, accessToken, uploadProtocol, "attributes"),
                SettingsJson = GatewayJson.Serialize(uploadSettings),
                BatchSize = 1,
                BufferingEnabled = false,
                Enabled = true
            });
        }

        return uploadChannels;
    }

    private static Dictionary<string, string?> BuildUploadSettings(
        CollectionUploadContract? configuredUpload,
        string normalizedBaseUrl,
        string accessToken,
        Guid edgeNodeId)
    {
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["edgeNodeId"] = edgeNodeId == Guid.Empty ? null : edgeNodeId.ToString("D"),
            ["edgeBaseUrl"] = string.IsNullOrWhiteSpace(normalizedBaseUrl) ? null : normalizedBaseUrl,
            ["accessToken"] = string.IsNullOrWhiteSpace(accessToken) ? null : accessToken
        };

        MergeJsonObject(settings, configuredUpload?.Settings);
        return settings;
    }

    private static UploadProtocol ResolveUploadProtocol(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol))
        {
            return UploadProtocol.IotSharpDeviceHttp;
        }

        return protocol.Trim().ToLowerInvariant() switch
        {
            "http" => UploadProtocol.Http,
            "mqtt" or "iotsharpmqtt" => UploadProtocol.IotSharpMqtt,
            "devicehttp" or "iotsharpdevicehttp" or "iotsharp" => UploadProtocol.IotSharpDeviceHttp,
            "sonnet" or "sonnetdb" => UploadProtocol.SonnetDb,
            _ => throw new NotSupportedException($"Configured upload protocol '{protocol}' is not supported.")
        };
    }

    private static string ResolveUploadEndpoint(
        CollectionUploadContract? configuredUpload,
        string normalizedBaseUrl,
        string accessToken,
        UploadProtocol uploadProtocol,
        string targetKind)
    {
        if (configuredUpload is not null && !string.IsNullOrWhiteSpace(configuredUpload.Endpoint))
        {
            return configuredUpload.Endpoint;
        }

        return uploadProtocol switch
        {
            UploadProtocol.IotSharpDeviceHttp when !string.IsNullOrWhiteSpace(normalizedBaseUrl) && !string.IsNullOrWhiteSpace(accessToken) => new Uri(new Uri(normalizedBaseUrl, UriKind.Absolute), $"api/Devices/{Uri.EscapeDataString(accessToken)}/{char.ToUpperInvariant(targetKind[0])}{targetKind[1..]}").ToString(),
            UploadProtocol.IotSharpDeviceHttp => throw new InvalidOperationException("IoTSharp device HTTP upload requires EdgeReporting.BaseUrl and EdgeReporting.AccessToken or an explicit upload endpoint."),
            UploadProtocol.SonnetDb => throw new InvalidOperationException("SonnetDB upload requires an explicit endpoint."),
            _ => throw new NotSupportedException($"Upload protocol '{uploadProtocol}' requires an explicit endpoint.")
        };
    }

    private static IEnumerable<PollingTask> BuildPollingTasks(
        Guid edgeNodeId,
        CollectionTaskContract task,
        CollectionDeviceContract device,
        Guid deviceId,
        IReadOnlyCollection<DevicePointState> devicePointStates)
    {
        var groups = devicePointStates
            .GroupBy(item => new PollingKey(
                Math.Max(item.Contract.Polling?.ReadPeriodMs ?? 5000, 1000),
                string.IsNullOrWhiteSpace(item.Contract.Polling?.Group) ? "default" : item.Contract.Polling.Group!.Trim()),
                item => item)
            .OrderBy(group => group.Key.ReadPeriodMs)
            .ThenBy(group => group.Key.Group, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var pointIds = group
                .Select(item => item.PointId)
                .Where(item => item != Guid.Empty)
                .Distinct()
                .OrderBy(item => item)
                .ToArray();

            yield return new PollingTask
            {
                Id = CreateDeterministicGuid(edgeNodeId, "polling-task", task.TaskKey, device.DeviceKey, group.Key.Group, group.Key.ReadPeriodMs.ToString(CultureInfo.InvariantCulture)),
                DeviceId = deviceId,
                Name = $"{task.TaskKey}/{device.DeviceKey}/{group.Key.Group}",
                IntervalSeconds = Math.Max(1, (int)Math.Ceiling(group.Key.ReadPeriodMs / 1000d)),
                PointIdsJson = JsonSerializer.Serialize(pointIds),
                TriggerOnChange = task.ReportPolicy.DefaultTrigger == GatewayReportTriggerType.OnChange,
                BatchRead = true,
                Enabled = device.Enabled && group.Any(item => item.Enabled)
            };
        }
    }

    private static IEnumerable<TransformRule> BuildTransformRules(
        Guid edgeNodeId,
        CollectionTaskContract task,
        CollectionDeviceContract device,
        CollectionPointContract point,
        Guid pointId)
    {
        foreach (var transform in (point.Transforms ?? Array.Empty<ValueTransformContract>()).OrderBy(item => item.Order))
        {
            yield return new TransformRule
            {
                Id = CreateDeterministicGuid(edgeNodeId, "transform", task.TaskKey, device.DeviceKey, point.PointKey, transform.Order.ToString(CultureInfo.InvariantCulture), transform.TransformType.ToString()),
                PointId = pointId,
                Name = $"{point.PointName}-{transform.TransformType}",
                Kind = ResolveTransformKind(transform.TransformType),
                SortOrder = transform.Order,
                ArgumentsJson = GatewayJson.Serialize(BuildTransformArguments(transform)),
                Enabled = true
            };
        }
    }

    private static Dictionary<string, string?> BuildConnectionSettings(CollectionTaskContract task)
    {
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (task.Connection == null)
        {
            return settings;
        }

        switch (task.Protocol)
        {
            case GatewayCollectionProtocolType.Modbus:
                settings["transport"] = NormalizeModbusTransport(task.Connection.Transport);
                settings["host"] = Require(task.Connection.Host, $"task '{task.TaskKey}' requires connection.host for Modbus.");
                settings["port"] = (task.Connection.Port ?? 502).ToString(CultureInfo.InvariantCulture);
                settings["timeout"] = Math.Max(task.Connection.TimeoutMs, 1).ToString(CultureInfo.InvariantCulture);
                break;
            case GatewayCollectionProtocolType.SiemensS7:
                settings["host"] = Require(task.Connection.Host, $"task '{task.TaskKey}' requires connection.host for Siemens S7.");
                settings["port"] = (task.Connection.Port ?? 102).ToString(CultureInfo.InvariantCulture);
                settings["timeout"] = Math.Max(task.Connection.TimeoutMs, 1).ToString(CultureInfo.InvariantCulture);
                settings["model"] = FirstString(task.Connection.ProtocolOptions, "model", "plcModel")
                    ?? throw new InvalidOperationException($"task '{task.TaskKey}' requires protocolOptions.model for Siemens S7.");
                settings["rack"] = FirstString(task.Connection.ProtocolOptions, "rack") ?? "0";
                settings["slot"] = FirstString(task.Connection.ProtocolOptions, "slot") ?? "0";
                break;
            case GatewayCollectionProtocolType.Mitsubishi:
                settings["host"] = Require(task.Connection.Host, $"task '{task.TaskKey}' requires connection.host for Mitsubishi.");
                settings["port"] = (task.Connection.Port ?? 6000).ToString(CultureInfo.InvariantCulture);
                settings["timeout"] = Math.Max(task.Connection.TimeoutMs, 1).ToString(CultureInfo.InvariantCulture);
                settings["model"] = FirstString(task.Connection.ProtocolOptions, "model", "plcModel")
                    ?? throw new InvalidOperationException($"task '{task.TaskKey}' requires protocolOptions.model for Mitsubishi.");
                break;
            case GatewayCollectionProtocolType.OmronFins:
                settings["host"] = Require(task.Connection.Host, $"task '{task.TaskKey}' requires connection.host for Omron FINS.");
                settings["port"] = (task.Connection.Port ?? 9600).ToString(CultureInfo.InvariantCulture);
                settings["timeout"] = Math.Max(task.Connection.TimeoutMs, 1).ToString(CultureInfo.InvariantCulture);
                settings["endianFormat"] = FirstString(task.Connection.ProtocolOptions, "endianFormat", "endian") ?? "ABCD";
                break;
            case GatewayCollectionProtocolType.AllenBradley:
                settings["host"] = Require(task.Connection.Host, $"task '{task.TaskKey}' requires connection.host for Allen-Bradley.");
                settings["port"] = (task.Connection.Port ?? 44818).ToString(CultureInfo.InvariantCulture);
                settings["timeout"] = Math.Max(task.Connection.TimeoutMs, 1).ToString(CultureInfo.InvariantCulture);
                settings["slot"] = FirstString(task.Connection.ProtocolOptions, "slot") ?? "0";
                break;
            case GatewayCollectionProtocolType.OpcUa:
                settings["endpoint"] = ResolveOpcUaEndpoint(task.Connection);
                if (!string.IsNullOrWhiteSpace(task.Connection.Host))
                {
                    settings["host"] = task.Connection.Host;
                }
                if (task.Connection.Port.HasValue)
                {
                    settings["port"] = task.Connection.Port.Value.ToString(CultureInfo.InvariantCulture);
                }
                break;
            case GatewayCollectionProtocolType.OpcDa:
                settings["progId"] = FirstString(task.Connection.ProtocolOptions, "progId", "programId")
                    ?? throw new InvalidOperationException($"task '{task.TaskKey}' requires protocolOptions.progId for OPC DA.");
                if (!string.IsNullOrWhiteSpace(task.Connection.Host))
                {
                    settings["host"] = task.Connection.Host;
                }
                break;
            case GatewayCollectionProtocolType.MtCnc:
                settings["baseUrl"] = ResolveMtConnectBaseUrl(task.Connection);
                if (!string.IsNullOrWhiteSpace(task.Connection.Host))
                {
                    settings["host"] = task.Connection.Host;
                }
                if (task.Connection.Port.HasValue)
                {
                    settings["port"] = task.Connection.Port.Value.ToString(CultureInfo.InvariantCulture);
                }
                settings["timeout"] = Math.Max(task.Connection.TimeoutMs, 1).ToString(CultureInfo.InvariantCulture);
                break;
            case GatewayCollectionProtocolType.FanucCnc:
                settings["host"] = Require(task.Connection.Host, $"task '{task.TaskKey}' requires connection.host for Fanuc CNC.");
                settings["port"] = (task.Connection.Port ?? 8193).ToString(CultureInfo.InvariantCulture);
                settings["timeout"] = Math.Max((int)Math.Ceiling(task.Connection.TimeoutMs / 1000d), 1).ToString(CultureInfo.InvariantCulture);
                break;
            default:
                throw new NotSupportedException($"Collection protocol '{task.Protocol}' is not supported by Gateway sync.");
        }

        MergeJsonObject(settings, task.Connection.ProtocolOptions);
        return settings;
    }

    private static Dictionary<string, string?> BuildDeviceSettings(CollectionDeviceContract device)
    {
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        MergeJsonObject(settings, device.ProtocolOptions);
        return settings;
    }

    private static Dictionary<string, string?> BuildPointSettings(
        CollectionTaskContract task,
        CollectionDeviceContract device,
        CollectionPointContract point)
    {
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        MergeJsonObject(settings, device.ProtocolOptions);
        MergeJsonObject(settings, point.ProtocolOptions);

        if (task.Protocol == GatewayCollectionProtocolType.Modbus)
        {
            var stationNumber = FirstString(point.ProtocolOptions, "slaveId")
                ?? FirstString(point.ProtocolOptions, "stationNumber")
                ?? FirstString(device.ProtocolOptions, "slaveId")
                ?? FirstString(device.ProtocolOptions, "stationNumber");
            if (!string.IsNullOrWhiteSpace(stationNumber))
            {
                settings["stationNumber"] = stationNumber;
            }

            settings["functionCode"] = FirstString(point.ProtocolOptions, "functionCode")
                ?? ResolveModbusFunctionCode(point.SourceType).ToString(CultureInfo.InvariantCulture);

            var stringEncoding = FirstString(point.ProtocolOptions, "stringEncoding");
            if (!string.IsNullOrWhiteSpace(stringEncoding))
            {
                settings["encoding"] = stringEncoding switch
                {
                    "GBK" => "GBK",
                    "ASCII" => "ASCII",
                    "UTF8" => "UTF-8",
                    _ => stringEncoding
                };
            }
        }

        return settings;
    }

    private static Dictionary<string, string?> BuildTransformArguments(ValueTransformContract transform)
    {
        return transform.TransformType switch
        {
            GatewayCollectionTransformType.Scale => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["factor"] = RequireNumber(transform.Parameters, "factor", "value", "scale")
            },
            GatewayCollectionTransformType.Offset => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["offset"] = RequireNumber(transform.Parameters, "offset", "value")
            },
            GatewayCollectionTransformType.EnumMap => BuildEnumMapArguments(transform.Parameters),
            GatewayCollectionTransformType.BitExtract => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["index"] = RequireInteger(transform.Parameters, "index", "bitOffset", "offset")
            },
            GatewayCollectionTransformType.Expression => BuildExpressionArguments(transform.Parameters),
            GatewayCollectionTransformType.WordSwap => throw new NotSupportedException("Gateway sync does not support WordSwap transforms yet."),
            GatewayCollectionTransformType.ByteSwap => throw new NotSupportedException("Gateway sync does not support ByteSwap transforms yet."),
            GatewayCollectionTransformType.Clamp => throw new NotSupportedException("Gateway sync does not support Clamp transforms yet."),
            GatewayCollectionTransformType.DefaultOnError => throw new NotSupportedException("Gateway sync does not support DefaultOnError transforms yet."),
            _ => throw new NotSupportedException($"Gateway sync does not support transform '{transform.TransformType}'.")
        };
    }

    private static Dictionary<string, string?> BuildEnumMapArguments(JsonElement? parameters)
    {
        if (!parameters.HasValue || parameters.Value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("EnumMap transform requires object parameters.");
        }

        var source = parameters.Value;
        if (TryGetProperty(source, "mapping", out var mappingElement) ||
            TryGetProperty(source, "enumMapping", out mappingElement) ||
            TryGetProperty(source, "map", out mappingElement))
        {
            source = mappingElement;
        }

        if (source.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("EnumMap transform requires an object mapping.");
        }

        var arguments = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in source.EnumerateObject())
        {
            arguments[property.Name] = ToStringValue(property.Value);
        }

        if (arguments.Count == 0)
        {
            throw new InvalidOperationException("EnumMap transform requires at least one mapping item.");
        }

        return arguments;
    }

    private static Dictionary<string, string?> BuildExpressionArguments(JsonElement? parameters)
    {
        if (!parameters.HasValue)
        {
            throw new InvalidOperationException("Expression transform requires parameters.");
        }

        if (parameters.Value.ValueKind == JsonValueKind.String)
        {
            var expression = parameters.Value.GetString();
            if (string.IsNullOrWhiteSpace(expression))
            {
                throw new InvalidOperationException("Expression transform requires a non-empty expression.");
            }

            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["expression"] = expression
            };
        }

        if (parameters.Value.ValueKind != JsonValueKind.Object)
        {
            var expression = ToStringValue(parameters.Value);
            if (string.IsNullOrWhiteSpace(expression))
            {
                throw new InvalidOperationException("Expression transform requires a non-empty expression.");
            }

            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["expression"] = expression
            };
        }

        var arguments = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in parameters.Value.EnumerateObject())
        {
            arguments[property.Name] = ToStringValue(property.Value);
        }

        var code = FirstNonEmpty(arguments, "expression", "script", "code", "value");
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Expression transform requires 'expression', 'script', or 'code'.");
        }

        arguments["expression"] = code;
        return arguments;
    }

    private static string RequireNumber(JsonElement? parameters, params string[] keys)
    {
        var value = FirstString(parameters, keys);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Transform parameter '{string.Join("' or '", keys)}' is required.");
        }

        return value;
    }

    private static string RequireInteger(JsonElement? parameters, params string[] keys)
    {
        var value = RequireNumber(parameters, keys);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed.ToString(CultureInfo.InvariantCulture)
            : throw new InvalidOperationException($"Transform parameter '{string.Join("' or '", keys)}' must be an integer.");
    }

    private static ushort ResolvePointLength(CollectionPointContract point)
    {
        var length = point.Length > 0 ? point.Length : 1;
        return length > ushort.MaxValue ? ushort.MaxValue : (ushort)length;
    }

    private static int ResolveDevicePollingIntervalSeconds(IEnumerable<CollectionPointContract> points)
    {
        var minPeriodMs = points
            .Select(point => point.Polling?.ReadPeriodMs ?? 5000)
            .Where(value => value > 0)
            .DefaultIfEmpty(5000)
            .Min();

        return Math.Max(1, (int)Math.Ceiling(minPeriodMs / 1000d));
    }

    private static PointAccessMode ResolveAccessMode(GatewayCollectionTargetType targetType)
    {
        return targetType switch
        {
            GatewayCollectionTargetType.Telemetry => PointAccessMode.Read,
            GatewayCollectionTargetType.Attribute => PointAccessMode.Read,
            GatewayCollectionTargetType.AlarmInput => throw new NotSupportedException("Gateway sync does not support AlarmInput target mapping yet."),
            GatewayCollectionTargetType.CommandFeedback => throw new NotSupportedException("Gateway sync does not support CommandFeedback target mapping yet."),
            _ => throw new NotSupportedException($"Gateway sync does not support target type '{targetType}'.")
        };
    }

    private static GatewayCollectionTargetType NormalizeTargetType(GatewayCollectionTargetType targetType, string pointKey)
    {
        return targetType switch
        {
            GatewayCollectionTargetType.Telemetry => GatewayCollectionTargetType.Telemetry,
            GatewayCollectionTargetType.Attribute => GatewayCollectionTargetType.Attribute,
            GatewayCollectionTargetType.AlarmInput => throw new NotSupportedException($"Point '{pointKey}' uses unsupported target type AlarmInput."),
            GatewayCollectionTargetType.CommandFeedback => throw new NotSupportedException($"Point '{pointKey}' uses unsupported target type CommandFeedback."),
            _ => throw new NotSupportedException($"Point '{pointKey}' uses unsupported target type '{targetType}'.")
        };
    }

    private static TransformationKind ResolveTransformKind(GatewayCollectionTransformType transformType)
    {
        return transformType switch
        {
            GatewayCollectionTransformType.Scale => TransformationKind.Scale,
            GatewayCollectionTransformType.Offset => TransformationKind.Offset,
            GatewayCollectionTransformType.EnumMap => TransformationKind.EnumMap,
            GatewayCollectionTransformType.BitExtract => TransformationKind.BitExtract,
            GatewayCollectionTransformType.Expression => TransformationKind.Expression,
            _ => throw new NotSupportedException($"Gateway sync does not support transform '{transformType}'.")
        };
    }

    private static GatewayDataType ResolveDataType(string rawValueType)
    {
        return rawValueType.Trim().ToLowerInvariant() switch
        {
            "bool" or "boolean" => GatewayDataType.Boolean,
            "byte" => GatewayDataType.Byte,
            "int16" or "short" => GatewayDataType.Int16,
            "uint16" or "ushort" => GatewayDataType.UInt16,
            "int32" or "int" => GatewayDataType.Int32,
            "uint32" => GatewayDataType.UInt32,
            "int64" or "long" => GatewayDataType.Int64,
            "uint64" or "ulong" => GatewayDataType.UInt64,
            "float" or "float32" or "single" => GatewayDataType.Float,
            "double" or "float64" => GatewayDataType.Double,
            "decimal" => GatewayDataType.Double,
            "string" or "text" => GatewayDataType.String,
            _ => throw new NotSupportedException($"Gateway sync does not support rawValueType '{rawValueType}'.")
        };
    }

    private static string ResolveDriverCode(GatewayCollectionProtocolType protocol)
    {
        return protocol switch
        {
            GatewayCollectionProtocolType.Modbus => "modbus",
            GatewayCollectionProtocolType.OpcUa => "opc-ua",
            GatewayCollectionProtocolType.OpcDa => "opc-da",
            GatewayCollectionProtocolType.MtCnc => "mt-cnc",
            GatewayCollectionProtocolType.FanucCnc => "fanuc-cnc",
            GatewayCollectionProtocolType.SiemensS7 => "siemens-s7",
            GatewayCollectionProtocolType.Mitsubishi => "mitsubishi",
            GatewayCollectionProtocolType.OmronFins => "omron-fins",
            GatewayCollectionProtocolType.AllenBradley => "allen-bradley",
            _ => throw new NotSupportedException($"Gateway sync does not support collection protocol '{protocol}'.")
        };
    }

    private static string ResolveOpcUaEndpoint(CollectionConnectionContract connection)
    {
        var endpoint = FirstString(connection.ProtocolOptions, "endpointUrl")
            ?? FirstString(connection.ProtocolOptions, "endpoint");
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            return endpoint;
        }

        if (string.IsNullOrWhiteSpace(connection.Host))
        {
            throw new InvalidOperationException("OPC UA connection requires protocolOptions.endpointUrl or connection.host.");
        }

        var port = connection.Port ?? 4840;
        return $"opc.tcp://{connection.Host}:{port}";
    }

    private static string ResolveMtConnectBaseUrl(CollectionConnectionContract connection)
    {
        var baseUrl = FirstString(connection.ProtocolOptions, "baseUrl", "agentUrl", "endpoint");
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            return baseUrl;
        }

        if (string.IsNullOrWhiteSpace(connection.Host))
        {
            throw new InvalidOperationException("MTConnect connection requires protocolOptions.baseUrl or connection.host.");
        }

        var scheme = FirstString(connection.ProtocolOptions, "scheme", "protocol") ?? "http";
        var port = connection.Port.HasValue ? $":{connection.Port.Value}" : string.Empty;
        return $"{scheme}://{connection.Host}{port}";
    }

    private static string NormalizeModbusTransport(string? transport)
    {
        return transport?.Trim().ToLowerInvariant() switch
        {
            "tcp" => "tcp",
            "rtuovertcp" => "rtuOverTcp",
            "serialrtu" => throw new NotSupportedException("Gateway sync does not support Modbus SerialRtu transport yet."),
            _ => "tcp"
        };
    }

    private static byte ResolveModbusFunctionCode(string? sourceType)
    {
        return sourceType?.Trim().ToLowerInvariant() switch
        {
            "coil" => 1,
            "discreteinput" => 2,
            "holdingregister" => 3,
            "inputregister" => 4,
            _ => 3
        };
    }

    private static void MergeJsonObject(IDictionary<string, string?> target, JsonElement? element)
    {
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.Value.EnumerateObject())
        {
            target[property.Name] = ToStringValue(property.Value);
        }
    }

    private static string? FirstString(JsonElement? element, params string[] keys)
    {
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (TryGetProperty(element.Value, key, out var value))
            {
                return ToStringValue(value);
            }
        }

        return null;
    }

    private static string? FirstNonEmpty(IReadOnlyDictionary<string, string?> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? ToStringValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.Object or JsonValueKind.Array => value.GetRawText(),
            _ => value.ToString()
        };
    }

    private static string NormalizeBaseUrl(string baseUrl)
        => baseUrl.Trim().TrimEnd('/') + "/";

    private static string Require(string? value, string message)
        => string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException(message) : value.Trim();

    private static Guid CreateDeterministicGuid(Guid edgeNodeId, params string[] parts)
    {
        var normalized = string.Join("::", new[] { edgeNodeId.ToString("N") }.Concat(parts).Select(value => value?.Trim() ?? string.Empty));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private sealed record PollingKey(int ReadPeriodMs, string Group);

    private sealed record DevicePointState(Guid PointId, bool Enabled, CollectionPointContract Contract);
}
