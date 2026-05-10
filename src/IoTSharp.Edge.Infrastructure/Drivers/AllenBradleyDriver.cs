namespace IoTSharp.Edge.Infrastructure.Drivers;

internal sealed class AllenBradleyDriver : DeviceDriverBase
{
    public override DriverMetadata Metadata { get; } = new(
        "allen-bradley",
        DriverType.AllenBradley,
        "Allen-Bradley",
        "IoTClient-backed Allen-Bradley driver.",
        true,
        true,
        true,
        true,
        new[]
        {
            new ConnectionSettingDefinition("host", "Host", "text", true, "PLC host name or IP."),
            new ConnectionSettingDefinition("port", "Port", "number", true, "PLC port."),
            new ConnectionSettingDefinition("slot", "Slot", "number", false, "Backplane slot."),
            new ConnectionSettingDefinition("timeout", "Timeout", "number", false, "Timeout in milliseconds.")
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
                GatewayDataType.Byte => ToReadResult(request.Address, client.ReadByte(request.Address)),
                GatewayDataType.Int16 => ToReadResult(request.Address, client.ReadInt16(request.Address)),
                GatewayDataType.UInt16 => ToReadResult(request.Address, client.ReadUInt16(request.Address)),
                GatewayDataType.Int32 => ToReadResult(request.Address, client.ReadInt32(request.Address)),
                GatewayDataType.UInt32 => ToReadResult(request.Address, client.ReadUInt32(request.Address)),
                GatewayDataType.Int64 => ToReadResult(request.Address, client.ReadInt64(request.Address)),
                GatewayDataType.UInt64 => ToReadResult(request.Address, client.ReadUInt64(request.Address)),
                GatewayDataType.Float => ToReadResult(request.Address, client.ReadFloat(request.Address)),
                GatewayDataType.Double => ToReadResult(request.Address, client.ReadDouble(request.Address)),
                _ => new DriverReadResult(request.Address, null, null, DateTimeOffset.UtcNow, QualityStatus.Bad, $"Unsupported Allen-Bradley data type '{request.DataType}'.")
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
                GatewayDataType.Byte => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToByte(request.Value))),
                GatewayDataType.Int16 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToInt16(request.Value))),
                GatewayDataType.UInt16 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToUInt16(request.Value))),
                GatewayDataType.Int32 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToInt32(request.Value))),
                GatewayDataType.UInt32 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToUInt32(request.Value))),
                GatewayDataType.Int64 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToInt64(request.Value))),
                GatewayDataType.UInt64 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToUInt64(request.Value))),
                GatewayDataType.Float => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToSingle(request.Value))),
                GatewayDataType.Double => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToDouble(request.Value))),
                GatewayDataType.String => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToString(request.Value) ?? string.Empty)),
                _ => new DriverWriteResult(request.Address, request.Value, DateTimeOffset.UtcNow, QualityStatus.Bad, $"Unsupported Allen-Bradley data type '{request.DataType}'.")
            };

            return Task.FromResult(result);
        }
        catch (Exception exception)
        {
            return Task.FromResult(FailedWrite(request.Address, request.Value, exception));
        }
    }

    private static AllenBradleyClient CreateClient(IReadOnlyDictionary<string, string?> settings)
        => new(
            Required(settings, "host"),
            Int(settings, "port", 44818),
            Byte(settings, "slot", 0),
            Int(settings, "timeout", 1500));
}
