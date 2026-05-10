namespace IoTSharp.Edge.Infrastructure.Drivers;

internal sealed class ModbusDriver : DeviceDriverBase
{
    public override DriverMetadata Metadata { get; } = new(
        "modbus",
        DriverType.Modbus,
        "Modbus",
        "Supports Modbus TCP and Modbus RTU over TCP through a unified driver contract.",
        true,
        true,
        true,
        true,
        new[]
        {
            new ConnectionSettingDefinition("transport", "Transport", "select", true, "tcp or rtuOverTcp.", new[] { "tcp", "rtuOverTcp" }),
            new ConnectionSettingDefinition("host", "Host", "text", true, "PLC host name or IP."),
            new ConnectionSettingDefinition("port", "Port", "number", true, "PLC port, usually 502."),
            new ConnectionSettingDefinition("timeout", "Timeout", "number", false, "Timeout in milliseconds."),
            new ConnectionSettingDefinition("endianFormat", "Endian", "select", false, "Word/byte order.", Enum.GetNames<EndianFormat>()),
            new ConnectionSettingDefinition("plcAddresses", "PLC Addresses", "boolean", false, "Treat addresses as PLC-style addresses.")
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
            var station = Byte(request.Settings ?? new Dictionary<string, string?>(), "stationNumber", 1);
            var functionCode = Byte(request.Settings ?? new Dictionary<string, string?>(), "functionCode", request.DataType == GatewayDataType.Boolean ? (byte)1 : (byte)3);
            if (request.DataType == GatewayDataType.String)
            {
                var rawResult = client.Read(request.Address, station, functionCode, request.Length, true);
                if (!rawResult.IsSucceed)
                {
                    return Task.FromResult(new DriverReadResult(request.Address, null, null, DateTimeOffset.UtcNow, QualityStatus.Bad, rawResult.Err));
                }

                var stringValue = ResolveEncoding(request.Settings ?? new Dictionary<string, string?>()).GetString(rawResult.Value).TrimEnd('\0');
                return Task.FromResult(new DriverReadResult(request.Address, stringValue, stringValue, DateTimeOffset.UtcNow, QualityStatus.Good));
            }

            var result = request.DataType switch
            {
                GatewayDataType.Boolean when functionCode == 2 => ToReadResult(request.Address, client.ReadDiscrete(request.Address, station, functionCode)),
                GatewayDataType.Boolean => ToReadResult(request.Address, client.ReadCoil(request.Address, station, functionCode)),
                GatewayDataType.Int16 => ToReadResult(request.Address, client.ReadInt16(request.Address, station, functionCode)),
                GatewayDataType.UInt16 => ToReadResult(request.Address, client.ReadUInt16(request.Address, station, functionCode)),
                GatewayDataType.Int32 => ToReadResult(request.Address, client.ReadInt32(request.Address, station, functionCode)),
                GatewayDataType.UInt32 => ToReadResult(request.Address, client.ReadUInt32(request.Address, station, functionCode)),
                GatewayDataType.Int64 => ToReadResult(request.Address, client.ReadInt64(request.Address, station, functionCode)),
                GatewayDataType.UInt64 => ToReadResult(request.Address, client.ReadUInt64(request.Address, station, functionCode)),
                GatewayDataType.Float => ToReadResult(request.Address, client.ReadFloat(request.Address, station, functionCode)),
                GatewayDataType.Double => ToReadResult(request.Address, client.ReadDouble(request.Address, station, functionCode)),
                _ => new DriverReadResult(request.Address, null, null, DateTimeOffset.UtcNow, QualityStatus.Bad, $"Unsupported Modbus data type '{request.DataType}'.")
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
            var settings = request.Settings ?? new Dictionary<string, string?>();
            var station = Byte(settings, "stationNumber", 1);
            var functionCode = Byte(settings, "functionCode", request.DataType == GatewayDataType.Boolean ? (byte)5 : (byte)16);
            var result = request.DataType switch
            {
                GatewayDataType.Boolean => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToBoolean(request.Value), station, functionCode)),
                GatewayDataType.Int16 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToInt16(request.Value), station, functionCode)),
                GatewayDataType.UInt16 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToUInt16(request.Value), station, functionCode)),
                GatewayDataType.Int32 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToInt32(request.Value), station, functionCode)),
                GatewayDataType.UInt32 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToUInt32(request.Value), station, functionCode)),
                GatewayDataType.Int64 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToInt64(request.Value), station, functionCode)),
                GatewayDataType.UInt64 => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToUInt64(request.Value), station, functionCode)),
                GatewayDataType.Float => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToSingle(request.Value), station, functionCode)),
                GatewayDataType.Double => ToWriteResult(request.Address, request.Value, client.Write(request.Address, Convert.ToDouble(request.Value), station, functionCode)),
                GatewayDataType.String => ToWriteResult(request.Address, request.Value, client.Write(request.Address, ResolveEncoding(settings).GetBytes(Convert.ToString(request.Value) ?? string.Empty), station, functionCode, true)),
                _ => new DriverWriteResult(request.Address, request.Value, DateTimeOffset.UtcNow, QualityStatus.Bad, $"Unsupported Modbus data type '{request.DataType}'.")
            };

            return Task.FromResult(result);
        }
        catch (Exception exception)
        {
            return Task.FromResult(FailedWrite(request.Address, request.Value, exception));
        }
    }

    private static IModbusClient CreateClient(IReadOnlyDictionary<string, string?> settings)
    {
        var transport = Required(settings, "transport");
        var host = Required(settings, "host");
        var port = Int(settings, "port", 502);
        var timeout = Int(settings, "timeout", 1500);
        var endian = ResolveEndian(settings);
        var plcAddresses = Boolean(settings, "plcAddresses", false);

        return transport.ToLowerInvariant() switch
        {
            "tcp" => new ModbusTcpClient(host, port, timeout, endian, plcAddresses),
            "rtuovertcp" => new ModbusRtuOverTcpClient(host, port, timeout, endian, plcAddresses),
            _ => throw new NotSupportedException($"Modbus transport '{transport}' is not supported yet.")
        };
    }
}
