using System.Globalization;
using System.Text.Json;
using IoTSharp.Edge.BasicRuntime;
using IoTSharp.Edge.Domain;
using BasicRuntimeHost = global::IoTSharp.Edge.BasicRuntime.BasicRuntime;

namespace IoTSharp.Edge.RuntimeExtensions;

public sealed class GatewayRuntimeExtension : IBasicRuntimeExtension
{
    private readonly IDeviceDriverRegistry _driverRegistry;
    private readonly IUploadTransportRegistry _uploadTransportRegistry;

    public GatewayRuntimeExtension(
        IDeviceDriverRegistry driverRegistry,
        IUploadTransportRegistry uploadTransportRegistry)
    {
        _driverRegistry = driverRegistry;
        _uploadTransportRegistry = uploadTransportRegistry;
    }

    public string Name => "gateway-runtime";

    public void Register(BasicRuntimeHost runtime)
    {
        runtime.RegisterFunction("EDGE_DRIVER_CATALOG", (_, _) => BuildDriverCatalog());
        runtime.RegisterFunction("EDGE_DRIVER_METADATA", (_, arguments) => BuildDriverMetadata(RequiredString(arguments, 0, "driverCode")));
        runtime.RegisterFunction("EDGE_DRIVER_READ", (_, arguments) => ExecuteDriverRead(arguments));
        runtime.RegisterFunction("EDGE_DRIVER_WRITE", (_, arguments) => ExecuteDriverWrite(arguments));
        runtime.RegisterFunction("EDGE_UPLOAD", (_, arguments) => ExecuteUpload(arguments));
    }

    private IReadOnlyCollection<Dictionary<string, object?>> BuildDriverCatalog()
        => _driverRegistry.GetMetadata().Select(BuildDriverMetadata).ToArray();

    private Dictionary<string, object?> BuildDriverMetadata(string driverCode)
        => BuildDriverMetadata(_driverRegistry.GetRequiredDriver(driverCode).Metadata);

    private static Dictionary<string, object?> BuildDriverMetadata(DriverMetadata metadata)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = metadata.Code,
            ["driverType"] = metadata.DriverType.ToString(),
            ["displayName"] = metadata.DisplayName,
            ["description"] = metadata.Description,
            ["supportsRead"] = metadata.SupportsRead,
            ["supportsWrite"] = metadata.SupportsWrite,
            ["supportsBatchRead"] = metadata.SupportsBatchRead,
            ["supportsBatchWrite"] = metadata.SupportsBatchWrite,
            ["riskLevel"] = metadata.RiskLevel,
            ["connectionSettings"] = metadata.ConnectionSettings
                .Select(setting => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["key"] = setting.Key,
                    ["label"] = setting.Label,
                    ["valueType"] = setting.ValueType,
                    ["required"] = setting.Required,
                    ["description"] = setting.Description,
                    ["options"] = setting.Options?.Cast<object?>().ToArray()
                })
                .ToArray()
        };

    private Dictionary<string, object?> ExecuteDriverRead(IReadOnlyList<object?> arguments)
    {
        try
        {
            var driverCode = RequiredString(arguments, 0, "driverCode");
            var connectionSettings = ToStringDictionary(Argument(arguments, 1));
            var address = RequiredString(arguments, 2, "address");
            var dataType = ParseGatewayDataType(RequiredString(arguments, 3, "dataType"));
            var length = Math.Max(1, OptionalInt(arguments, 4, 1));
            var pointSettings = ToStringDictionary(Argument(arguments, 5));

            var driver = _driverRegistry.GetRequiredDriver(driverCode);
            var request = new DriverReadRequest(address, dataType, (ushort)Math.Min(length, ushort.MaxValue), pointSettings);
            var result = driver.ReadAsync(new DriverConnectionContext(driverCode, connectionSettings), request, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            return BuildReadResult(driverCode, result);
        }
        catch (Exception exception)
        {
            return BuildFailureResult("read", exception.Message);
        }
    }

    private Dictionary<string, object?> ExecuteDriverWrite(IReadOnlyList<object?> arguments)
    {
        try
        {
            var driverCode = RequiredString(arguments, 0, "driverCode");
            var connectionSettings = ToStringDictionary(Argument(arguments, 1));
            var address = RequiredString(arguments, 2, "address");
            var dataType = ParseGatewayDataType(RequiredString(arguments, 3, "dataType"));
            var value = ConvertValue(Argument(arguments, 4));
            var length = Math.Max(1, OptionalInt(arguments, 5, 1));
            var pointSettings = ToStringDictionary(Argument(arguments, 6));

            var driver = _driverRegistry.GetRequiredDriver(driverCode);
            var request = new DriverWriteRequest(address, dataType, value, (ushort)Math.Min(length, ushort.MaxValue), pointSettings);
            var result = driver.WriteAsync(new DriverConnectionContext(driverCode, connectionSettings), request, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            return BuildWriteResult(driverCode, result);
        }
        catch (Exception exception)
        {
            return BuildFailureResult("write", exception.Message);
        }
    }

    private Dictionary<string, object?> ExecuteUpload(IReadOnlyList<object?> arguments)
    {
        try
        {
            var protocol = ParseUploadProtocol(RequiredString(arguments, 0, "protocol"));
            var endpoint = RequiredString(arguments, 1, "endpoint");
            var settings = ToObjectDictionary(Argument(arguments, 2));
            var envelopeData = ToObjectDictionary(Argument(arguments, 3));
            var envelope = BuildUploadEnvelope(envelopeData);
            var channel = new UploadChannel
            {
                Name = $"Script {protocol}",
                Protocol = protocol,
                Endpoint = endpoint,
                SettingsJson = JsonSerializer.Serialize(settings),
                BatchSize = 1,
                BufferingEnabled = false,
                Enabled = true
            };

            var transport = _uploadTransportRegistry.GetRequiredTransport(protocol);
            transport.UploadAsync(channel, envelope, CancellationToken.None).GetAwaiter().GetResult();

            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = true,
                ["status"] = "Succeeded",
                ["protocol"] = protocol.ToString(),
                ["endpoint"] = endpoint,
                ["deviceName"] = envelope.DeviceName,
                ["pointName"] = envelope.PointName,
                ["target"] = envelope.Target,
                ["quality"] = envelope.Quality.ToString(),
                ["uploadedAtUtc"] = DateTime.UtcNow.ToString("O"),
                ["errorMessage"] = string.Empty
            };
        }
        catch (Exception exception)
        {
            return BuildFailureResult("upload", exception.Message);
        }
    }

    private static Dictionary<string, object?> BuildReadResult(string driverCode, DriverReadResult result)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = result.Quality == QualityStatus.Good,
            ["action"] = "read",
            ["driverCode"] = driverCode,
            ["address"] = result.Address,
            ["quality"] = result.Quality.ToString(),
            ["rawValue"] = result.RawValue,
            ["value"] = result.TransformedValue ?? result.RawValue,
            ["timestampUtc"] = result.Timestamp.UtcDateTime.ToString("O"),
            ["errorMessage"] = result.ErrorMessage ?? string.Empty
        };

    private static Dictionary<string, object?> BuildWriteResult(string driverCode, DriverWriteResult result)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = result.Quality == QualityStatus.Good,
            ["action"] = "write",
            ["driverCode"] = driverCode,
            ["address"] = result.Address,
            ["quality"] = result.Quality.ToString(),
            ["value"] = result.Value,
            ["timestampUtc"] = result.Timestamp.UtcDateTime.ToString("O"),
            ["errorMessage"] = result.ErrorMessage ?? string.Empty
        };

    private static Dictionary<string, object?> BuildFailureResult(string action, string message)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = false,
            ["action"] = action,
            ["quality"] = QualityStatus.Bad.ToString(),
            ["errorMessage"] = message
        };

    private static UploadEnvelope BuildUploadEnvelope(IReadOnlyDictionary<string, object?> envelope)
    {
        var deviceName = RequiredString(envelope, "deviceName");
        var pointName = RequiredString(envelope, "pointName");
        var rawValue = envelope.TryGetValue("rawValue", out var raw) ? ConvertValue(raw) : null;
        var value = envelope.TryGetValue("value", out var transformed) ? ConvertValue(transformed) : rawValue;
        var target = OptionalString(envelope, "target") ?? string.Empty;
        var payloadTemplate = OptionalString(envelope, "payloadTemplate") ?? string.Empty;
        var quality = envelope.TryGetValue("quality", out var qualityValue)
            ? ParseQualityStatus(qualityValue)
            : QualityStatus.Good;
        var timestamp = envelope.TryGetValue("timestampUtc", out var timestampValue)
            ? ParseTimestamp(timestampValue)
            : DateTimeOffset.UtcNow;

        return new UploadEnvelope(
            deviceName,
            pointName,
            rawValue,
            value,
            timestamp,
            quality,
            target,
            payloadTemplate);
    }

    private static IReadOnlyDictionary<string, string?> ToStringDictionary(object? value)
    {
        return value switch
        {
            null => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            string text when string.IsNullOrWhiteSpace(text) => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            string text => ParseJsonTextAsStrings(text),
            BasicDictionary dictionary => dictionary.Keys.ToDictionary(
                key => key,
                key => Convert.ToString(ConvertValue(dictionary.Get(key).ToObject()), CultureInfo.InvariantCulture),
                StringComparer.OrdinalIgnoreCase),
            IReadOnlyDictionary<string, object?> readOnlyDictionary => readOnlyDictionary.ToDictionary(
                pair => pair.Key,
                pair => Convert.ToString(ConvertValue(pair.Value), CultureInfo.InvariantCulture),
                StringComparer.OrdinalIgnoreCase),
            IDictionary<string, object?> dictionary => dictionary.ToDictionary(
                pair => pair.Key,
                pair => Convert.ToString(ConvertValue(pair.Value), CultureInfo.InvariantCulture),
                StringComparer.OrdinalIgnoreCase),
            JsonElement element when element.ValueKind == JsonValueKind.Object => ConvertJsonObjectToStrings(element),
            _ => throw new BasicRuntimeException("Value must be a JSON object, BASIC dict, or empty.")
        };
    }

    private static Dictionary<string, object?> ToObjectDictionary(object? value)
    {
        return value switch
        {
            null => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            string text when string.IsNullOrWhiteSpace(text) => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            string text => ParseJsonObject(text),
            BasicDictionary dictionary => dictionary.Keys.ToDictionary(
                key => key,
                key => ConvertValue(dictionary.Get(key).ToObject()),
                StringComparer.OrdinalIgnoreCase),
            IReadOnlyDictionary<string, object?> readOnlyDictionary => readOnlyDictionary.ToDictionary(
                pair => pair.Key,
                pair => ConvertValue(pair.Value),
                StringComparer.OrdinalIgnoreCase),
            IDictionary<string, object?> dictionary => dictionary.ToDictionary(
                pair => pair.Key,
                pair => ConvertValue(pair.Value),
                StringComparer.OrdinalIgnoreCase),
            JsonElement element when element.ValueKind == JsonValueKind.Object => ConvertJsonObject(element),
            _ => throw new BasicRuntimeException("Value must be a JSON object, BASIC dict, or empty.")
        };
    }

    private static Dictionary<string, object?> ConvertJsonObject(JsonElement element)
        => element.EnumerateObject()
            .ToDictionary(pair => pair.Name, pair => ConvertJsonElement(pair.Value), StringComparer.OrdinalIgnoreCase);

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ConvertJsonObject(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.ToString()
        };
    }

    private static Dictionary<string, object?> ParseJsonObject(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new BasicRuntimeException("JSON text must describe an object.");
        }

        return ConvertJsonObject(document.RootElement);
    }

    private static Dictionary<string, string?> ParseJsonTextAsStrings(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new BasicRuntimeException("JSON text must describe an object.");
        }

        return ConvertJsonObjectToStrings(document.RootElement);
    }

    private static Dictionary<string, string?> ConvertJsonObjectToStrings(JsonElement element)
        => element.EnumerateObject()
            .ToDictionary(pair => pair.Name, pair => Convert.ToString(ConvertJsonElement(pair.Value), CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase);

    private static object? ConvertValue(object? value)
    {
        return value switch
        {
            null => null,
            BasicDictionary dictionary => dictionary.Keys.ToDictionary(
                key => key,
                key => ConvertValue(dictionary.Get(key).ToObject()),
                StringComparer.OrdinalIgnoreCase),
            IReadOnlyDictionary<string, object?> readOnlyDictionary => readOnlyDictionary.ToDictionary(
                pair => pair.Key,
                pair => ConvertValue(pair.Value),
                StringComparer.OrdinalIgnoreCase),
            IDictionary<string, object?> dictionary => dictionary.ToDictionary(
                pair => pair.Key,
                pair => ConvertValue(pair.Value),
                StringComparer.OrdinalIgnoreCase),
            object?[] array => array.Select(ConvertValue).ToArray(),
            JsonElement element => ConvertJsonElement(element),
            _ => value
        };
    }

    private static string RequiredString(IReadOnlyList<object?> arguments, int index, string name)
    {
        if (index >= arguments.Count || arguments[index] is null)
        {
            throw new BasicRuntimeException($"Argument '{name}' is required.");
        }

        var text = Convert.ToString(arguments[index], CultureInfo.InvariantCulture)?.Trim();
        return !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new BasicRuntimeException($"Argument '{name}' is required.");
    }

    private static string RequiredString(IReadOnlyDictionary<string, object?> values, string name)
    {
        var text = OptionalString(values, name);
        return !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new BasicRuntimeException($"Envelope field '{name}' is required.");
    }

    private static string? OptionalString(IReadOnlyDictionary<string, object?> values, string name)
        => values.TryGetValue(name, out var value) ? Convert.ToString(ConvertValue(value), CultureInfo.InvariantCulture)?.Trim() : null;

    private static object? Argument(IReadOnlyList<object?> arguments, int index)
        => index < arguments.Count ? arguments[index] : null;

    private static int OptionalInt(IReadOnlyList<object?> arguments, int index, int defaultValue)
        => index < arguments.Count && arguments[index] is not null
            ? int.TryParse(Convert.ToString(arguments[index], CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : defaultValue
            : defaultValue;

    private static GatewayDataType ParseGatewayDataType(string value)
        => value.Trim().ToLowerInvariant() switch
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
            "string" or "text" => GatewayDataType.String,
            _ => throw new BasicRuntimeException($"Data type '{value}' is not supported.")
        };

    private static UploadProtocol ParseUploadProtocol(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "http" => UploadProtocol.Http,
            "mqtt" or "iotsharpmqtt" => UploadProtocol.IotSharpMqtt,
            "devicehttp" or "iotsharpdevicehttp" or "iotsharp" => UploadProtocol.IotSharpDeviceHttp,
            _ => throw new BasicRuntimeException($"Upload protocol '{value}' is not supported.")
        };

    private static QualityStatus ParseQualityStatus(object? value)
    {
        if (value is QualityStatus qualityStatus)
        {
            return qualityStatus;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return Enum.TryParse<QualityStatus>(text, true, out var parsed)
            ? parsed
            : QualityStatus.Good;
    }

    private static DateTimeOffset ParseTimestamp(object? value)
    {
        return value switch
        {
            DateTimeOffset offset => offset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            string text when DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) => parsed,
            _ => DateTimeOffset.UtcNow
        };
    }
}
