namespace IoTSharp.Edge.Infrastructure.Drivers;

internal sealed class ModbusDriver : DeviceDriverBase
{
    public override DriverMetadata Metadata { get; } = new(
        "modbus",
        DriverType.Modbus,
        "Modbus",
        "Supports Modbus TCP, RTU over TCP, serial RTU, and serial ASCII through a unified driver contract.",
        true,
        true,
        true,
        true,
        new[]
        {
            new ConnectionSettingDefinition("transport", "Transport", "select", true, "tcp, rtuOverTcp, serialRtu, or serialAscii.", new[] { "tcp", "rtuOverTcp", "serialRtu", "serialAscii" }),
            new ConnectionSettingDefinition("host", "Host", "text", false, "PLC host name or IP for TCP transports."),
            new ConnectionSettingDefinition("port", "Port", "number", false, "PLC TCP port, usually 502."),
            new ConnectionSettingDefinition("serialPort", "Serial Port", "text", false, "Serial port for Modbus RTU/ASCII, for example COM3 or /dev/ttyUSB0."),
            new ConnectionSettingDefinition("baudRate", "Baud Rate", "number", false, "Serial baud rate, usually 9600."),
            new ConnectionSettingDefinition("dataBits", "Data Bits", "number", false, "Serial data bits, usually 8."),
            new ConnectionSettingDefinition("parity", "Parity", "select", false, "Serial parity.", new[] { "None", "Odd", "Even", "Mark", "Space" }),
            new ConnectionSettingDefinition("stopBits", "Stop Bits", "select", false, "Serial stop bits.", new[] { "One", "OnePointFive", "Two" }),
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
        var transport = NormalizeTransport(Required(settings, "transport"));
        var timeout = Int(settings, "timeout", 1500);
        var endian = ResolveEndian(settings);
        var plcAddresses = Boolean(settings, "plcAddresses", false);

        return transport switch
        {
            "tcp" => new ModbusTcpClient(Required(settings, "host"), Int(settings, "port", 502), timeout, endian, plcAddresses),
            "rtuOverTcp" => new ModbusRtuOverTcpClient(Required(settings, "host"), Int(settings, "port", 502), timeout, endian, plcAddresses),
            "serialRtu" => new ModbusRtuClient(
                RequiredAny(settings, "serialPort", "portName", "comPort"),
                IntAny(settings, 9600, "baudRate", "baud"),
                timeout,
                ParseStopBits(GetAny(settings, "stopBits")),
                ParseParity(GetAny(settings, "parity")),
                IntAny(settings, 8, "dataBits"),
                endian,
                plcAddresses),
            "serialAscii" => new ModbusAsciiClient(
                RequiredAny(settings, "serialPort", "portName", "comPort"),
                IntAny(settings, 9600, "baudRate", "baud"),
                timeout,
                ParseStopBits(GetAny(settings, "stopBits")),
                ParseParity(GetAny(settings, "parity")),
                IntAny(settings, 8, "dataBits"),
                endian,
                plcAddresses),
            _ => throw new NotSupportedException($"Modbus transport '{transport}' is not supported yet.")
        };
    }

    private static string NormalizeTransport(string transport)
    {
        return NormalizeKey(transport) switch
        {
            "tcp" or "modbustcp" => "tcp",
            "rtuovertcp" or "rtutcp" or "modbusrtuovertcp" => "rtuOverTcp",
            "serialrtu" or "rtu" or "modbusrtu" or "rs485" or "rs232" or "serial" or "serialdtu" or "dtu" => "serialRtu",
            "serialascii" or "ascii" or "modbusascii" => "serialAscii",
            var value => throw new NotSupportedException($"Modbus transport '{transport}' is not supported yet.")
        };
    }

    private static string RequiredAny(IReadOnlyDictionary<string, string?> values, params string[] keys)
        => GetAny(values, keys) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"One of connection settings '{string.Join("', '", keys)}' is required.");

    private static string? GetAny(IReadOnlyDictionary<string, string?> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static int IntAny(IReadOnlyDictionary<string, string?> values, int defaultValue, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return defaultValue;
    }

    private static Parity ParseParity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Parity.None;
        }

        return NormalizeKey(value) switch
        {
            "0" or "n" or "none" => Parity.None,
            "1" or "o" or "odd" => Parity.Odd,
            "2" or "e" or "even" => Parity.Even,
            "3" or "m" or "mark" => Parity.Mark,
            "4" or "s" or "space" => Parity.Space,
            _ when Enum.TryParse<Parity>(value, true, out var parity) => parity,
            _ => throw new InvalidOperationException("Modbus serial parity must be None, Odd, Even, Mark, or Space.")
        };
    }

    private static StopBits ParseStopBits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return StopBits.One;
        }

        return NormalizeKey(value) switch
        {
            "1" or "one" => StopBits.One,
            "15" or "onepointfive" or "onepoint5" => StopBits.OnePointFive,
            "2" or "two" => StopBits.Two,
            _ when double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) && Math.Abs(parsed - 1.0d) < 0.0000000001d => StopBits.One,
            _ when double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) && Math.Abs(parsed - 1.5d) < 0.0000000001d => StopBits.OnePointFive,
            _ when double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) && Math.Abs(parsed - 2.0d) < 0.0000000001d => StopBits.Two,
            _ => throw new InvalidOperationException("Modbus serial stop bits must be One, OnePointFive, Two, 1, 1.5, or 2.")
        };
    }

    private static string NormalizeKey(string value)
        => new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
