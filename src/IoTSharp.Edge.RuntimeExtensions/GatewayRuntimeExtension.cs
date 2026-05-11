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
    private BasicRuntimeHost? _runtime;

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
        _runtime = runtime;
        runtime.RegisterFunction("EDGE_DRIVER_CATALOG", (_, _) => BuildDriverCatalog());
        runtime.RegisterFunction("EDGE_DRIVER_METADATA", (_, arguments) => BuildDriverMetadata(RequiredString(arguments, 0, "driverCode")));
        runtime.RegisterFunction("EDGE_DRIVER_READ", (_, arguments) => ExecuteDriverRead(arguments));
        runtime.RegisterFunction("EDGE_DRIVER_WRITE", (_, arguments) => ExecuteDriverWrite(arguments));
        runtime.RegisterFunction("EDGE_TRANSFORM_APPLY", (_, arguments) => ExecuteTransformApply(arguments));
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

    private Dictionary<string, object?> ExecuteTransformApply(IReadOnlyList<object?> arguments)
    {
        var value = ConvertValue(Argument(arguments, 0));
        try
        {
            var transformed = value;
            foreach (var rule in ToTransformRules(Argument(arguments, 1)).Where(rule => rule.Enabled).OrderBy(rule => rule.SortOrder))
            {
                transformed = ApplyTransform(transformed, rule);
            }

            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = true,
                ["action"] = "transform",
                ["quality"] = QualityStatus.Good.ToString(),
                ["value"] = transformed,
                ["errorMessage"] = string.Empty
            };
        }
        catch (Exception exception)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["action"] = "transform",
                ["quality"] = QualityStatus.Bad.ToString(),
                ["value"] = value,
                ["errorMessage"] = exception.Message
            };
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

    private object? ApplyTransform(object? value, ScriptTransformRule rule)
    {
        if (value is null)
        {
            return null;
        }

        return rule.Kind switch
        {
            TransformationKind.Scale => Scale(value, GetDecimal(rule.Arguments, "factor") ?? 1m),
            TransformationKind.Offset => Offset(value, GetDecimal(rule.Arguments, "offset") ?? 0m),
            TransformationKind.Cast => Cast(value, GetString(rule.Arguments, "type")),
            TransformationKind.BitExtract => BitExtract(value, GetInt32(rule.Arguments, "index") ?? 0),
            TransformationKind.EnumMap => rule.Arguments.TryGetValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, out var mapped) ? ConvertValue(mapped) : value,
            TransformationKind.Expression => ApplyExpression(value, rule.Arguments),
            _ => value
        };
    }

    private object? ApplyExpression(object value, IReadOnlyDictionary<string, object?> arguments)
    {
        var code = GetString(arguments, "expression")
            ?? GetString(arguments, "script")
            ?? GetString(arguments, "code");
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
                variables[pair.Key] = ConvertValue(pair.Value);
            }
        }

        var ownsRuntime = _runtime is null;
        var runtime = _runtime ?? new BasicRuntimeHost();
        try
        {
            var options = new BasicRuntimeOptions
            {
                MaxStatements = 10_000,
                MaxLoopIterations = 10_000
            };

            if (LooksLikeScript(code))
            {
                var result = runtime.Execute(code, variables, options);
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

            return runtime.Evaluate(code, variables, options);
        }
        finally
        {
            if (ownsRuntime)
            {
                runtime.Dispose();
            }
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

    private static IReadOnlyCollection<ScriptTransformRule> ToTransformRules(object? value)
    {
        var normalized = ConvertValue(value);
        return normalized switch
        {
            null => Array.Empty<ScriptTransformRule>(),
            IReadOnlyDictionary<string, object?> dictionary => [ParseTransformRule(dictionary)],
            IDictionary<string, object?> dictionary => [ParseTransformRule(ToObjectDictionary(dictionary))],
            IEnumerable<object?> values => values
                .Select(item => item is null ? null : ParseTransformRule(ToObjectDictionary(item)))
                .Where(rule => rule is not null)
                .Select(rule => rule!)
                .ToArray(),
            _ => Array.Empty<ScriptTransformRule>()
        };
    }

    private static ScriptTransformRule ParseTransformRule(IReadOnlyDictionary<string, object?> dictionary)
    {
        var kindText = GetString(dictionary, "kind")
            ?? GetString(dictionary, "transformType")
            ?? GetString(dictionary, "type")
            ?? "Passthrough";
        if (!Enum.TryParse<TransformationKind>(kindText, true, out var kind))
        {
            kind = TransformationKind.Passthrough;
        }

        var arguments = dictionary.TryGetValue("arguments", out var argumentValue) && argumentValue is not null
            ? ToObjectDictionary(argumentValue)
            : dictionary
                .Where(pair => !IsTransformRuleMetadata(pair.Key))
                .ToDictionary(pair => pair.Key, pair => ConvertValue(pair.Value), StringComparer.OrdinalIgnoreCase);

        return new ScriptTransformRule(
            kind,
            GetInt32(dictionary, "sortOrder") ?? GetInt32(dictionary, "order") ?? 0,
            GetBool(dictionary, "enabled", true),
            arguments);
    }

    private static bool IsTransformRuleMetadata(string key)
        => string.Equals(key, "kind", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "transformType", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "type", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "sortOrder", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "order", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "enabled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "arguments", StringComparison.OrdinalIgnoreCase);

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

    private static string? GetString(IReadOnlyDictionary<string, object?> values, string key)
        => values.TryGetValue(key, out var value) ? Convert.ToString(ConvertValue(value), CultureInfo.InvariantCulture)?.Trim() : null;

    private static int? GetInt32(IReadOnlyDictionary<string, object?> values, string key)
        => values.TryGetValue(key, out var value) && int.TryParse(Convert.ToString(ConvertValue(value), CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static decimal? GetDecimal(IReadOnlyDictionary<string, object?> values, string key)
        => values.TryGetValue(key, out var value) && decimal.TryParse(Convert.ToString(ConvertValue(value), CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static bool GetBool(IReadOnlyDictionary<string, object?> values, string key, bool defaultValue)
        => values.TryGetValue(key, out var value) && bool.TryParse(Convert.ToString(ConvertValue(value), CultureInfo.InvariantCulture), out var parsed)
            ? parsed
            : defaultValue;

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
            "devicehttp" or "iotsharpdevicehttp" => UploadProtocol.IotSharpDeviceHttp,
            "iotsharp" => UploadProtocol.IoTSharp,
            "thingboard" or "thingsboard" => UploadProtocol.ThingsBoard,
            "sonnet" or "sonnetdb" => UploadProtocol.SonnetDb,
            "influx" or "influxdb" => UploadProtocol.InfluxDb,
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

    private sealed record ScriptTransformRule(
        TransformationKind Kind,
        int SortOrder,
        bool Enabled,
        IReadOnlyDictionary<string, object?> Arguments);
}
