namespace IoTEdge.Infrastructure.Drivers;

internal sealed class MitsubishiDriver : DeviceDriverBase
{
    public override DriverMetadata Metadata { get; } = new(
        "mitsubishi",
        DriverType.Mitsubishi,
        "三菱 PLC",
        "基于 IoTClient 的三菱 PLC 驱动。",
        true,
        true,
        true,
        true,
        new[]
        {
            new ConnectionSettingDefinition("host", "主机", "text", true, "PLC 主机名或 IP 地址。"),
            new ConnectionSettingDefinition("port", "端口", "number", true, "PLC 端口。"),
            new ConnectionSettingDefinition("model", "型号", "select", true, "三菱型号。", Enum.GetNames<MitsubishiVersion>()),
            new ConnectionSettingDefinition("timeout", "超时", "number", false, "超时时间，单位毫秒。")
        });

    public override Task<ConnectionTestResult> TestConnectionAsync(DriverConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            _ = CreateClient(context.Settings);
            return Task.FromResult(new ConnectionTestResult(true));
        }
        catch (Exception exception)
        {
            return Task.FromResult(new ConnectionTestResult(false, exception.Message));
        }
    }

    public override Task<DriverReadResult> ReadAsync(DriverConnectionContext context, DriverReadRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateClient(context.Settings);
            var result = request.DataType switch
            {
                GatewayDataType.Boolean => ToReadResult(request.Address, client.ReadBoolean(request.Address)),
                GatewayDataType.Int16 => ToReadResult(request.Address, client.ReadInt16(request.Address)),
                GatewayDataType.UInt16 => ToReadResult(request.Address, client.ReadUInt16(request.Address)),
                GatewayDataType.Int32 => ToReadResult(request.Address, client.ReadInt32(request.Address)),
                GatewayDataType.UInt32 => ToReadResult(request.Address, client.ReadUInt32(request.Address)),
                GatewayDataType.Int64 => ToReadResult(request.Address, client.ReadInt64(request.Address)),
                GatewayDataType.UInt64 => ToReadResult(request.Address, client.ReadUInt64(request.Address)),
                GatewayDataType.Float => ToReadResult(request.Address, client.ReadFloat(request.Address)),
                GatewayDataType.Double => ToReadResult(request.Address, client.ReadDouble(request.Address)),
                _ => new DriverReadResult(request.Address, null, null, DateTimeOffset.UtcNow, QualityStatus.Bad, $"不支持的三菱数据类型“{request.DataType}”。")
            };

            return Task.FromResult(result);
        }
        catch (Exception exception)
        {
            return Task.FromResult(FailedRead(request.Address, exception));
        }
    }

    public override Task<DriverWriteResult> WriteAsync(DriverConnectionContext context, DriverWriteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateClient(context.Settings);
            var result = request.DataType switch
            {
                GatewayDataType.Boolean => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToBoolean(request.Value))),
                GatewayDataType.Int16 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToInt16(request.Value))),
                GatewayDataType.UInt16 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToUInt16(request.Value))),
                GatewayDataType.Int32 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToInt32(request.Value))),
                GatewayDataType.UInt32 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToUInt32(request.Value))),
                GatewayDataType.Int64 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToInt64(request.Value))),
                GatewayDataType.UInt64 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToUInt64(request.Value))),
                GatewayDataType.Float => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToSingle(request.Value))),
                GatewayDataType.Double => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToDouble(request.Value))),
                GatewayDataType.String => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToString(request.Value) ?? string.Empty)),
                _ => new DriverWriteResult(request.Address, request.Value, DateTimeOffset.UtcNow, QualityStatus.Bad, $"不支持的三菱数据类型“{request.DataType}”。")
            };

            return Task.FromResult(result);
        }
        catch (Exception exception)
        {
            return Task.FromResult(FailedWrite(request.Address, request.Value, exception));
        }
    }

    private static MitsubishiClient CreateClient(IReadOnlyDictionary<string, string?> settings)
        => new(
            Enum.Parse<MitsubishiVersion>(Required(settings, "model"), true),
            Required(settings, "host"),
            Int(settings, "port", 6000),
            Int(settings, "timeout", 1500));
}
