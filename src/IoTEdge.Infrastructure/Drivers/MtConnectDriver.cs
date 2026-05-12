namespace IoTEdge.Infrastructure.Drivers;

internal sealed class MtConnectDriver : DeviceDriverBase
{
    public override DriverMetadata Metadata { get; } = new(
        "mt-cnc",
        DriverType.MtCnc,
        "MTConnect 数控",
        "通过 HTTP/XML 读取机床和数控遥测的 MTConnect 当前端点驱动。",
        true,
        false,
        true,
        false,
        new[]
        {
            new ConnectionSettingDefinition("baseUrl", "基础地址", "text", true, "MTConnect 代理基础地址，例如 http://127.0.0.1:5000。"),
            new ConnectionSettingDefinition("device", "设备", "text", false, "可选的 MTConnect 设备名或路径段。"),
            new ConnectionSettingDefinition("timeout", "超时", "number", false, "HTTP 超时时间，单位毫秒。"),
            new ConnectionSettingDefinition("path", "路径", "text", false, "当前端点路径，默认使用 current。")
        });

    public override Task<AddressValidationResult> ValidateAddressAsync(DriverReadRequest request, CancellationToken cancellationToken)
        => Task.FromResult(string.IsNullOrWhiteSpace(request.Address)
            ? new AddressValidationResult(false, "MTConnect 数据项 ID 或名称是必填项。")
            : new AddressValidationResult(true));

    public override async Task<ConnectionTestResult> TestConnectionAsync(DriverConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = CreateClient(context.Settings);
            using var response = await httpClient.GetAsync(BuildCurrentUri(context.Settings), cancellationToken);
            return response.IsSuccessStatusCode
                ? new ConnectionTestResult(true)
                : new ConnectionTestResult(false, $"MTConnect 代理返回了 HTTP {(int)response.StatusCode}。");
        }
        catch (Exception exception)
        {
            return new ConnectionTestResult(false, exception.Message);
        }
    }

    public override async Task<DriverReadResult> ReadAsync(DriverConnectionContext context, DriverReadRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var values = await ReadCurrentValuesAsync(context.Settings, cancellationToken);
            if (!TryResolveValue(values, request.Address, out var rawValue))
            {
                return new DriverReadResult(request.Address, null, null, DateTimeOffset.UtcNow, QualityStatus.Bad, $"未找到 MTConnect 数据项“{request.Address}”。");
            }

            var value = CoerceMtConnectValue(rawValue, request.DataType);
            return new DriverReadResult(request.Address, rawValue, value, DateTimeOffset.UtcNow, QualityStatus.Good);
        }
        catch (Exception exception)
        {
            return FailedRead(request.Address, exception);
        }
    }

    public override async Task<IReadOnlyCollection<DriverReadResult>> ReadBatchAsync(DriverConnectionContext context, DriverBatchReadRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var values = await ReadCurrentValuesAsync(context.Settings, cancellationToken);
            return request.Requests
                .Select(item =>
                {
                    if (!TryResolveValue(values, item.Address, out var rawValue))
                    {
                        return new DriverReadResult(item.Address, null, null, DateTimeOffset.UtcNow, QualityStatus.Bad, $"未找到 MTConnect 数据项“{item.Address}”。");
                    }

                    var value = CoerceMtConnectValue(rawValue, item.DataType);
                    return new DriverReadResult(item.Address, rawValue, value, DateTimeOffset.UtcNow, QualityStatus.Good);
                })
                .ToArray();
        }
        catch (Exception exception)
        {
            return request.Requests
                .Select(item => FailedRead(item.Address, exception))
                .ToArray();
        }
    }

    public override Task<DriverWriteResult> WriteAsync(DriverConnectionContext context, DriverWriteRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new DriverWriteResult(request.Address, request.Value, DateTimeOffset.UtcNow, QualityStatus.Bad, "MTConnect 当前端点为只读。"));

    private static HttpClient CreateClient(IReadOnlyDictionary<string, string?> settings)
        => new()
        {
            Timeout = TimeSpan.FromMilliseconds(Math.Max(Int(settings, "timeout", 3000), 1))
        };

    private static Uri BuildCurrentUri(IReadOnlyDictionary<string, string?> settings)
    {
        var baseUrl = Required(settings, "baseUrl").TrimEnd('/') + "/";
        var device = settings.TryGetValue("device", out var deviceValue) && !string.IsNullOrWhiteSpace(deviceValue)
            ? deviceValue.Trim().Trim('/')
            : string.Empty;
        var path = settings.TryGetValue("path", out var pathValue) && !string.IsNullOrWhiteSpace(pathValue)
            ? pathValue.Trim().TrimStart('/')
            : "current";
        var relative = string.IsNullOrWhiteSpace(device)
            ? path
            : $"{Uri.EscapeDataString(device)}/{path}";

        return new Uri(new Uri(baseUrl, UriKind.Absolute), relative);
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadCurrentValuesAsync(IReadOnlyDictionary<string, string?> settings, CancellationToken cancellationToken)
    {
        using var httpClient = CreateClient(settings);
        await using var stream = await httpClient.GetStreamAsync(BuildCurrentUri(settings), cancellationToken);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in document.Descendants().Where(item => item.HasAttributes && !item.HasElements))
        {
            AddMtConnectValue(values, element, "dataItemId");
            AddMtConnectValue(values, element, "name");
            AddMtConnectValue(values, element, "sequence");
        }

        return values;
    }

    private static void AddMtConnectValue(IDictionary<string, string> values, XElement element, string attributeName)
    {
        var key = element.Attribute(attributeName)?.Value;
        if (!string.IsNullOrWhiteSpace(key))
        {
            values[key] = element.Value;
        }
    }

    private static bool TryResolveValue(IReadOnlyDictionary<string, string> values, string address, out string value)
    {
        if (values.TryGetValue(address, out value!))
        {
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static object? CoerceMtConnectValue(string value, GatewayDataType dataType)
        => dataType switch
        {
            GatewayDataType.Boolean => value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("1", StringComparison.OrdinalIgnoreCase) || value.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase),
            GatewayDataType.Byte => Convert.ToByte(value, CultureInfo.InvariantCulture),
            GatewayDataType.Int16 => Convert.ToInt16(value, CultureInfo.InvariantCulture),
            GatewayDataType.UInt16 => Convert.ToUInt16(value, CultureInfo.InvariantCulture),
            GatewayDataType.Int32 => Convert.ToInt32(value, CultureInfo.InvariantCulture),
            GatewayDataType.UInt32 => Convert.ToUInt32(value, CultureInfo.InvariantCulture),
            GatewayDataType.Int64 => Convert.ToInt64(value, CultureInfo.InvariantCulture),
            GatewayDataType.UInt64 => Convert.ToUInt64(value, CultureInfo.InvariantCulture),
            GatewayDataType.Float => Convert.ToSingle(value, CultureInfo.InvariantCulture),
            GatewayDataType.Double => Convert.ToDouble(value, CultureInfo.InvariantCulture),
            GatewayDataType.String => value,
            _ => value
        };
}
