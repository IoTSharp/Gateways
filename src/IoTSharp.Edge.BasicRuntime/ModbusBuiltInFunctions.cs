using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using IoTClient.Clients.Modbus;
using IoTClient.Enums;

namespace IoTSharp.Edge.BasicRuntime;

public enum BasicModbusTransport
{
    Tcp,
    RtuOverTcp,
    Rtu,
    Ascii
}

public enum BasicModbusValueKind
{
    Bool,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Float,
    Double,
    String,
    Raw
}

public sealed record BasicModbusConnectionOptions
{
    public BasicModbusTransport Transport { get; init; }

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 502;

    public string PortName { get; init; } = string.Empty;

    public int BaudRate { get; init; } = 9_600;

    public int DataBits { get; init; } = 8;

    public Parity Parity { get; init; } = Parity.None;

    public StopBits StopBits { get; init; } = StopBits.One;

    public int TimeoutMs { get; init; } = 1_500;

    public EndianFormat EndianFormat { get; init; } = EndianFormat.ABCD;

    public bool PlcAddresses { get; init; }
}

public sealed record BasicModbusReadRequest(
    string Address,
    BasicModbusValueKind ValueKind,
    byte StationNumber,
    byte FunctionCode,
    int Length,
    Encoding Encoding);

public sealed record BasicModbusWriteRequest(
    string Address,
    BasicModbusValueKind ValueKind,
    object? Value,
    byte StationNumber,
    byte FunctionCode,
    Encoding Encoding);

public sealed record BasicModbusReadResult(bool Success, object? Value, string? Error);

public sealed record BasicModbusWriteResult(bool Success, string? Error);

public interface IBasicModbusClientFactory
{
    IBasicModbusClientSession Open(BasicModbusConnectionOptions options);
}

public interface IBasicModbusClientSession : IDisposable
{
    BasicModbusConnectionOptions Options { get; }

    bool IsConnected { get; }

    string LastError { get; }

    BasicModbusReadResult Read(BasicModbusReadRequest request);

    BasicModbusWriteResult Write(BasicModbusWriteRequest request);

    void Close();
}

internal static class ModbusBuiltInFunctions
{
    private static readonly BasicModbusTypeSpec CoilSpec = new(BasicModbusValueKind.Bool, 1, 5);
    private static readonly BasicModbusTypeSpec DiscreteSpec = new(BasicModbusValueKind.Bool, 2, 5);
    private static readonly BasicModbusTypeSpec Int16Spec = new(BasicModbusValueKind.Int16, 3, 16);
    private static readonly BasicModbusTypeSpec UInt16Spec = new(BasicModbusValueKind.UInt16, 3, 16);
    private static readonly BasicModbusTypeSpec Int32Spec = new(BasicModbusValueKind.Int32, 3, 16);
    private static readonly BasicModbusTypeSpec UInt32Spec = new(BasicModbusValueKind.UInt32, 3, 16);
    private static readonly BasicModbusTypeSpec Int64Spec = new(BasicModbusValueKind.Int64, 3, 16);
    private static readonly BasicModbusTypeSpec UInt64Spec = new(BasicModbusValueKind.UInt64, 3, 16);
    private static readonly BasicModbusTypeSpec FloatSpec = new(BasicModbusValueKind.Float, 3, 16);
    private static readonly BasicModbusTypeSpec DoubleSpec = new(BasicModbusValueKind.Double, 3, 16);
    private static readonly BasicModbusTypeSpec StringSpec = new(BasicModbusValueKind.String, 3, 16);
    private static readonly BasicModbusTypeSpec RawSpec = new(BasicModbusValueKind.Raw, 3, 16);

    public static void Register(BasicRuntime runtime)
    {
        runtime.RegisterInternalFunction("MODBUS_CONNECT_TCP", (_, args) => Connect(runtime, args, BasicModbusTransport.Tcp));
        runtime.RegisterInternalFunction("MODBUS_CONNECT_RTU_OVER_TCP", (_, args) => Connect(runtime, args, BasicModbusTransport.RtuOverTcp));
        runtime.RegisterInternalFunction("MODBUS_CONNECT_RTU", (_, args) => Connect(runtime, args, BasicModbusTransport.Rtu));
        runtime.RegisterInternalFunction("MODBUS_CONNECT_ASCII", (_, args) => Connect(runtime, args, BasicModbusTransport.Ascii));
        runtime.RegisterInternalFunction("MODBUS_CLOSE", (_, args) => Close(runtime, args));
        runtime.RegisterInternalFunction("MODBUS_LAST_ERROR", (_, args) => GetLastError(runtime, args));
        runtime.RegisterInternalFunction("MODBUS_READ", (_, args) => Read(runtime, args, null));
        runtime.RegisterInternalFunction("MODBUS_WRITE", (_, args) => Write(runtime, args, null));
        runtime.RegisterInternalFunction("MODBUS_READ_BOOL", (_, args) => Read(runtime, args, CoilSpec));
        runtime.RegisterInternalFunction("MODBUS_READ_COIL", (_, args) => Read(runtime, args, CoilSpec));
        runtime.RegisterInternalFunction("MODBUS_READ_DISCRETE", (_, args) => Read(runtime, args, DiscreteSpec));
        runtime.RegisterInternalFunction("MODBUS_READ_INT16", (_, args) => Read(runtime, args, Int16Spec));
        runtime.RegisterInternalFunction("MODBUS_READ_UINT16", (_, args) => Read(runtime, args, UInt16Spec));
        runtime.RegisterInternalFunction("MODBUS_READ_INT32", (_, args) => Read(runtime, args, Int32Spec));
        runtime.RegisterInternalFunction("MODBUS_READ_UINT32", (_, args) => Read(runtime, args, UInt32Spec));
        runtime.RegisterInternalFunction("MODBUS_READ_INT64", (_, args) => Read(runtime, args, Int64Spec));
        runtime.RegisterInternalFunction("MODBUS_READ_UINT64", (_, args) => Read(runtime, args, UInt64Spec));
        runtime.RegisterInternalFunction("MODBUS_READ_FLOAT", (_, args) => Read(runtime, args, FloatSpec));
        runtime.RegisterInternalFunction("MODBUS_READ_DOUBLE", (_, args) => Read(runtime, args, DoubleSpec));
        runtime.RegisterInternalFunction("MODBUS_READ_STRING", (_, args) => Read(runtime, args, StringSpec));
        runtime.RegisterInternalFunction("MODBUS_READ_RAW", (_, args) => Read(runtime, args, RawSpec));
        runtime.RegisterInternalFunction("MODBUS_WRITE_BOOL", (_, args) => Write(runtime, args, CoilSpec));
        runtime.RegisterInternalFunction("MODBUS_WRITE_COIL", (_, args) => Write(runtime, args, CoilSpec));
        runtime.RegisterInternalFunction("MODBUS_WRITE_INT16", (_, args) => Write(runtime, args, Int16Spec));
        runtime.RegisterInternalFunction("MODBUS_WRITE_UINT16", (_, args) => Write(runtime, args, UInt16Spec));
        runtime.RegisterInternalFunction("MODBUS_WRITE_INT32", (_, args) => Write(runtime, args, Int32Spec));
        runtime.RegisterInternalFunction("MODBUS_WRITE_UINT32", (_, args) => Write(runtime, args, UInt32Spec));
        runtime.RegisterInternalFunction("MODBUS_WRITE_INT64", (_, args) => Write(runtime, args, Int64Spec));
        runtime.RegisterInternalFunction("MODBUS_WRITE_UINT64", (_, args) => Write(runtime, args, UInt64Spec));
        runtime.RegisterInternalFunction("MODBUS_WRITE_FLOAT", (_, args) => Write(runtime, args, FloatSpec));
        runtime.RegisterInternalFunction("MODBUS_WRITE_DOUBLE", (_, args) => Write(runtime, args, DoubleSpec));
        runtime.RegisterInternalFunction("MODBUS_WRITE_STRING", (_, args) => Write(runtime, args, StringSpec));
        runtime.RegisterInternalFunction("MODBUS_WRITE_RAW", (_, args) => Write(runtime, args, RawSpec));
    }

    private static BasicValue Connect(BasicRuntime runtime, IReadOnlyList<BasicValue> args, BasicModbusTransport transport)
    {
        var state = runtime.ModbusState;
        try
        {
            var options = CreateOptions(args, transport);
            var session = runtime.ModbusClientFactory.Open(options);
            var handle = state.Add(session);
            state.ClearLastError();
            return BasicValue.FromNumber(handle);
        }
        catch (Exception ex)
        {
            return Fail(state, $"Modbus 连接失败：{Unwrap(ex).Message}", BasicValue.FromNumber(0));
        }
    }

    private static BasicValue Close(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.ModbusState;
        if (!TryGetSession(runtime, args, 0, out var session, out var handle))
        {
            return BasicValue.FromBoolean(false);
        }

        try
        {
            state.TryRemove(handle, out _);
            session.Close();
            state.ClearLastError();
            session.ClearLastError();
            return BasicValue.FromBoolean(true);
        }
        catch (Exception ex)
        {
            return Fail(state, $"Modbus 关闭失败：{Unwrap(ex).Message}", BasicValue.FromBoolean(false), session);
        }
    }

    private static BasicValue Read(BasicRuntime runtime, IReadOnlyList<BasicValue> args, BasicModbusTypeSpec? fixedSpec)
    {
        var state = runtime.ModbusState;
        if (!TryGetSession(runtime, args, 0, out var session, out _))
        {
            return BasicValue.Nil;
        }

        try
        {
            var request = fixedSpec is null
                ? CreateReadRequest(args, ParseDataType(RequiredText(args, 2)), generic: true)
                : CreateReadRequest(args, fixedSpec, generic: false);
            var result = session.Read(request);
            if (!result.Success)
            {
                return Fail(state, $"Modbus 读取失败：{result.Error ?? "未知错误"}", BasicValue.Nil, session);
            }

            state.ClearLastError();
            session.ClearLastError();
            return ToBasicValue(result.Value);
        }
        catch (Exception ex)
        {
            return Fail(state, $"Modbus 读取失败：{Unwrap(ex).Message}", BasicValue.Nil, session);
        }
    }

    private static BasicValue Write(BasicRuntime runtime, IReadOnlyList<BasicValue> args, BasicModbusTypeSpec? fixedSpec)
    {
        var state = runtime.ModbusState;
        if (!TryGetSession(runtime, args, 0, out var session, out _))
        {
            return BasicValue.FromBoolean(false);
        }

        try
        {
            var request = fixedSpec is null
                ? CreateWriteRequest(args, ParseDataType(RequiredText(args, 3)), generic: true)
                : CreateWriteRequest(args, fixedSpec, generic: false);
            var result = session.Write(request);
            if (!result.Success)
            {
                return Fail(state, $"Modbus 写入失败：{result.Error ?? "未知错误"}", BasicValue.FromBoolean(false), session);
            }

            state.ClearLastError();
            session.ClearLastError();
            return BasicValue.FromBoolean(true);
        }
        catch (Exception ex)
        {
            return Fail(state, $"Modbus 写入失败：{Unwrap(ex).Message}", BasicValue.FromBoolean(false), session);
        }
    }

    private static BasicValue GetLastError(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        if (TryGetHandle(args, 0, out var handle) && runtime.ModbusState.TryGet(handle, out var session))
        {
            var sessionError = session.LastError;
            if (!string.IsNullOrWhiteSpace(sessionError))
            {
                return BasicValue.FromString(sessionError);
            }
        }

        return BasicValue.FromString(runtime.ModbusState.LastError);
    }

    private static BasicModbusConnectionOptions CreateOptions(IReadOnlyList<BasicValue> args, BasicModbusTransport transport)
    {
        return transport switch
        {
            BasicModbusTransport.Tcp => new BasicModbusConnectionOptions
            {
                Transport = transport,
                Host = RequiredText(args, 0).Trim(),
                Port = OptionalInt(args, 1, 502),
                TimeoutMs = OptionalInt(args, 2, 1_500),
                EndianFormat = ParseEndianFormat(args, 3),
                PlcAddresses = OptionalBool(args, 4, false)
            },
            BasicModbusTransport.RtuOverTcp => new BasicModbusConnectionOptions
            {
                Transport = transport,
                Host = RequiredText(args, 0).Trim(),
                Port = OptionalInt(args, 1, 502),
                TimeoutMs = OptionalInt(args, 2, 1_500),
                EndianFormat = ParseEndianFormat(args, 3),
                PlcAddresses = OptionalBool(args, 4, false)
            },
            BasicModbusTransport.Rtu => new BasicModbusConnectionOptions
            {
                Transport = transport,
                PortName = RequiredText(args, 0).Trim(),
                BaudRate = OptionalInt(args, 1, 9_600),
                DataBits = OptionalInt(args, 2, 8),
                Parity = ParseParity(args, 3),
                StopBits = ParseStopBits(args, 4),
                TimeoutMs = OptionalInt(args, 5, 1_500),
                EndianFormat = ParseEndianFormat(args, 6),
                PlcAddresses = OptionalBool(args, 7, false)
            },
            BasicModbusTransport.Ascii => new BasicModbusConnectionOptions
            {
                Transport = transport,
                PortName = RequiredText(args, 0).Trim(),
                BaudRate = OptionalInt(args, 1, 9_600),
                DataBits = OptionalInt(args, 2, 8),
                Parity = ParseParity(args, 3),
                StopBits = ParseStopBits(args, 4),
                TimeoutMs = OptionalInt(args, 5, 1_500),
                EndianFormat = ParseEndianFormat(args, 6),
                PlcAddresses = OptionalBool(args, 7, false)
            },
            _ => throw new BasicRuntimeException("不支持的 Modbus 传输方式。")
        };
    }

    private static BasicModbusReadRequest CreateReadRequest(IReadOnlyList<BasicValue> args, BasicModbusTypeSpec spec, bool generic)
    {
        var addressIndex = 1;
        var stationIndex = generic ? 3 : 2;
        var functionIndex = generic ? 4 : 3;
        var lengthIndex = generic ? 5 : 4;
        var encodingIndex = generic ? 6 : 5;

        return new BasicModbusReadRequest(
            RequiredText(args, addressIndex).Trim(),
            spec.Kind,
            OptionalByte(args, stationIndex, 1),
            OptionalByte(args, functionIndex, spec.DefaultReadFunctionCode),
            Math.Max(0, OptionalInt(args, lengthIndex, 1)),
            ParseEncoding(args, encodingIndex));
    }

    private static BasicModbusWriteRequest CreateWriteRequest(IReadOnlyList<BasicValue> args, BasicModbusTypeSpec spec, bool generic)
    {
        var addressIndex = 1;
        var valueIndex = 2;
        var stationIndex = generic ? 4 : 3;
        var functionIndex = generic ? 5 : 4;
        var encodingIndex = generic ? 6 : 5;
        var encoding = ParseEncoding(args, encodingIndex);

        return new BasicModbusWriteRequest(
            RequiredText(args, addressIndex).Trim(),
            spec.Kind,
            ConvertWriteValue(Arg(args, valueIndex), spec, encoding),
            OptionalByte(args, stationIndex, 1),
            OptionalByte(args, functionIndex, spec.DefaultWriteFunctionCode),
            encoding);
    }

    private static BasicModbusTypeSpec ParseDataType(string value)
    {
        var normalized = Normalize(value);
        return normalized switch
        {
            "bool" or "boolean" or "bit" or "coil" => CoilSpec,
            "int16" or "short" => Int16Spec,
            "uint16" or "ushort" => UInt16Spec,
            "int32" or "int" => Int32Spec,
            "uint32" or "uint" => UInt32Spec,
            "int64" or "long" => Int64Spec,
            "uint64" or "ulong" => UInt64Spec,
            "float" or "single" => FloatSpec,
            "double" or "decimal" => DoubleSpec,
            "string" or "text" => StringSpec,
            "raw" or "bytes" or "binary" => RawSpec,
            _ => throw new BasicRuntimeException($"不支持的 Modbus 数据类型“{value}”。")
        };
    }

    private static BasicValue ToBasicValue(object? value)
        => value switch
        {
            null => BasicValue.Nil,
            byte[] bytes => BasicValue.FromList(new BasicList(bytes.Select(byteValue => BasicValue.FromNumber(byteValue)))),
            IEnumerable<byte> bytes => BasicValue.FromList(new BasicList(bytes.Select(byteValue => BasicValue.FromNumber(byteValue)))),
            _ => BasicValue.FromObject(value)
        };

    private static object? ConvertWriteValue(BasicValue value, BasicModbusTypeSpec spec, Encoding encoding)
    {
        return spec.Kind switch
        {
            BasicModbusValueKind.Bool => value.AsNumber() != 0,
            BasicModbusValueKind.Int16 => checked((short)value.AsNumber()),
            BasicModbusValueKind.UInt16 => checked((ushort)Math.Max(0, value.AsNumber())),
            BasicModbusValueKind.Int32 => checked((int)value.AsNumber()),
            BasicModbusValueKind.UInt32 => checked((uint)Math.Max(0, value.AsNumber())),
            BasicModbusValueKind.Int64 => checked((long)value.AsNumber()),
            BasicModbusValueKind.UInt64 => checked((ulong)Math.Max(0, value.AsNumber())),
            BasicModbusValueKind.Float => (float)value.AsNumber(),
            BasicModbusValueKind.Double => value.AsNumber(),
            BasicModbusValueKind.String => value.AsString(),
            BasicModbusValueKind.Raw => ConvertToBytes(value, encoding),
            _ => throw new BasicRuntimeException($"不支持的 Modbus 数据类型“{spec.Kind}”。")
        };
    }

    private static byte[] ConvertToBytes(BasicValue value, Encoding encoding)
    {
        if (value.Kind == BasicValueKind.Nil)
        {
            return Array.Empty<byte>();
        }

        if (value.Kind == BasicValueKind.String)
        {
            return encoding.GetBytes(value.Text);
        }

        if (value.Kind == BasicValueKind.Number && IsByteValue(value))
        {
            return [(byte)value.AsNumber()];
        }

        if (value.Kind == BasicValueKind.List && TryConvertValuesToBytes(value.List.Items, out var listBytes))
        {
            return listBytes;
        }

        if (value.Kind == BasicValueKind.Array && TryConvertObjectsToBytes(value.Array.ToObjectArray(), out var arrayBytes))
        {
            return arrayBytes;
        }

        return encoding.GetBytes(value.AsString());
    }

    private static bool TryConvertValuesToBytes(IEnumerable<BasicValue> values, out byte[] bytes)
    {
        var buffer = new List<byte>();
        foreach (var item in values)
        {
            if (!IsByteValue(item))
            {
                bytes = Array.Empty<byte>();
                return false;
            }

            buffer.Add((byte)item.AsNumber());
        }

        bytes = buffer.ToArray();
        return true;
    }

    private static bool TryConvertObjectsToBytes(IEnumerable<object?> values, out byte[] bytes)
    {
        var buffer = new List<byte>();
        foreach (var value in values)
        {
            if (!TryConvertObjectToByte(value, out var byteValue))
            {
                bytes = Array.Empty<byte>();
                return false;
            }

            buffer.Add(byteValue);
        }

        bytes = buffer.ToArray();
        return true;
    }

    private static bool TryConvertObjectToByte(object? value, out byte byteValue)
    {
        byteValue = 0;
        switch (value)
        {
            case byte b:
                byteValue = b;
                return true;
            case sbyte sb when sb >= byte.MinValue:
                byteValue = (byte)sb;
                return true;
            case short s when s >= byte.MinValue && s <= byte.MaxValue:
                byteValue = (byte)s;
                return true;
            case ushort us when us <= byte.MaxValue:
                byteValue = (byte)us;
                return true;
            case int i when i >= byte.MinValue && i <= byte.MaxValue:
                byteValue = (byte)i;
                return true;
            case uint ui when ui <= byte.MaxValue:
                byteValue = (byte)ui;
                return true;
            case long l when l >= byte.MinValue && l <= byte.MaxValue:
                byteValue = (byte)l;
                return true;
            case ulong ul when ul <= byte.MaxValue:
                byteValue = (byte)ul;
                return true;
            case float f when Math.Abs(f % 1) < 0.0000000001d && f >= byte.MinValue && f <= byte.MaxValue:
                byteValue = (byte)f;
                return true;
            case double d when Math.Abs(d % 1) < 0.0000000001d && d >= byte.MinValue && d <= byte.MaxValue:
                byteValue = (byte)d;
                return true;
            case decimal m when decimal.Truncate(m) == m && m >= byte.MinValue && m <= byte.MaxValue:
                byteValue = (byte)m;
                return true;
            default:
                return false;
        }
    }

    private static bool IsByteValue(BasicValue value)
        => value.Kind == BasicValueKind.Number
            && value.AsNumber() >= byte.MinValue
            && value.AsNumber() <= byte.MaxValue
            && Math.Abs(value.AsNumber() % 1) < 0.0000000001d;

    private static Encoding ParseEncoding(IReadOnlyList<BasicValue> args, int index)
    {
        var name = OptionalText(args, index, Encoding.UTF8.WebName) ?? Encoding.UTF8.WebName;
        try
        {
            return Encoding.GetEncoding(name);
        }
        catch (Exception ex)
        {
            throw new BasicRuntimeException($"Modbus 编码“{name}”不受支持：{ex.Message}");
        }
    }

    private static EndianFormat ParseEndianFormat(IReadOnlyList<BasicValue> args, int index)
    {
        var value = OptionalText(args, index, EndianFormat.ABCD.ToString()) ?? EndianFormat.ABCD.ToString();
        if (Enum.TryParse<EndianFormat>(value, true, out var format))
        {
            return format;
        }

        throw new BasicRuntimeException("Modbus 字节序必须是 ABCD、BADC、CDAB 或 DCBA。");
    }

    private static Parity ParseParity(IReadOnlyList<BasicValue> args, int index)
    {
        var value = OptionalText(args, index);
        if (string.IsNullOrWhiteSpace(value))
        {
            return Parity.None;
        }

        var normalized = Normalize(value);
        return normalized switch
        {
            "0" or "n" or "none" => Parity.None,
            "1" or "o" or "odd" => Parity.Odd,
            "2" or "e" or "even" => Parity.Even,
            "3" or "m" or "mark" => Parity.Mark,
            "4" or "s" or "space" => Parity.Space,
            _ when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw) && Enum.IsDefined(typeof(Parity), raw) => (Parity)raw,
            _ => throw new BasicRuntimeException("Modbus 校验位必须是 N/E/O/M/S 或 0-4。")
        };
    }

    private static StopBits ParseStopBits(IReadOnlyList<BasicValue> args, int index)
    {
        var value = OptionalText(args, index);
        if (string.IsNullOrWhiteSpace(value))
        {
            return StopBits.One;
        }

        var normalized = Normalize(value);
        return normalized switch
        {
            "1" or "one" => StopBits.One,
            "1.5" or "onepointfive" or "onepoint5" => StopBits.OnePointFive,
            "2" or "two" => StopBits.Two,
            _ when double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var raw) && Math.Abs(raw - 1.0) < 0.0000000001d => StopBits.One,
            _ when double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var rawHalf) && Math.Abs(rawHalf - 1.5d) < 0.0000000001d => StopBits.OnePointFive,
            _ when double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var rawTwo) && Math.Abs(rawTwo - 2d) < 0.0000000001d => StopBits.Two,
            _ => throw new BasicRuntimeException("Modbus 停止位必须是 1、1.5 或 2。")
        };
    }

    private static BasicValue Fail(ModbusRuntimeState state, string message, BasicValue failureValue, ModbusClientSession? session = null)
    {
        state.SetLastError(message);
        session?.SetLastError(message);
        return failureValue;
    }

    private static bool TryGetSession(BasicRuntime runtime, IReadOnlyList<BasicValue> args, int index, out ModbusClientSession session, out long handle)
    {
        session = null!;
        handle = 0;

        if (!TryGetHandle(args, index, out handle))
        {
            runtime.ModbusState.SetLastError("需要 Modbus 句柄。");
            return false;
        }

        if (!runtime.ModbusState.TryGet(handle, out session))
        {
            runtime.ModbusState.SetLastError("未找到 Modbus 句柄。");
            return false;
        }

        return true;
    }

    private static bool TryGetHandle(IReadOnlyList<BasicValue> args, int index, out long handle)
    {
        handle = 0;
        if (index < 0 || index >= args.Count)
        {
            return false;
        }

        var value = args[index];
        if (value.Kind == BasicValueKind.Nil)
        {
            return false;
        }

        if (value.Kind == BasicValueKind.Number)
        {
            handle = (long)value.AsNumber();
            return handle > 0;
        }

        if (long.TryParse(value.AsString(), NumberStyles.Any, CultureInfo.InvariantCulture, out handle))
        {
            return handle > 0;
        }

        return false;
    }

    private static BasicValue Arg(IReadOnlyList<BasicValue> args, int index)
        => index >= 0 && index < args.Count ? args[index] : BasicValue.Nil;

    private static string RequiredText(IReadOnlyList<BasicValue> args, int index)
        => index >= 0 && index < args.Count ? args[index].AsString() : string.Empty;

    private static string? OptionalText(IReadOnlyList<BasicValue> args, int index, string? fallback = null)
    {
        if (index < 0 || index >= args.Count)
        {
            return fallback;
        }

        var value = args[index];
        if (value.Kind == BasicValueKind.Nil)
        {
            return fallback;
        }

        var text = value.AsString();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static int OptionalInt(IReadOnlyList<BasicValue> args, int index, int fallback)
    {
        if (index < 0 || index >= args.Count || args[index].Kind == BasicValueKind.Nil)
        {
            return fallback;
        }

        return (int)args[index].AsNumber();
    }

    private static byte OptionalByte(IReadOnlyList<BasicValue> args, int index, byte fallback)
    {
        if (index < 0 || index >= args.Count || args[index].Kind == BasicValueKind.Nil)
        {
            return fallback;
        }

        return (byte)Math.Clamp((int)args[index].AsNumber(), byte.MinValue, byte.MaxValue);
    }

    private static bool OptionalBool(IReadOnlyList<BasicValue> args, int index, bool fallback)
    {
        if (index < 0 || index >= args.Count || args[index].Kind == BasicValueKind.Nil)
        {
            return fallback;
        }

        return args[index].AsNumber() != 0;
    }

    private static string Normalize(string value)
        => value.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();

    private static Exception Unwrap(Exception exception)
    {
        while (exception is AggregateException aggregate && aggregate.InnerException is not null)
        {
            exception = aggregate.InnerException;
        }

        return exception;
    }

    private sealed record BasicModbusTypeSpec(BasicModbusValueKind Kind, byte DefaultReadFunctionCode, byte DefaultWriteFunctionCode);
}

internal sealed class ModbusRuntimeState : IDisposable
{
    private readonly ConcurrentDictionary<long, ModbusClientSession> _sessions = new();
    private long _nextHandle;

    public string LastError { get; private set; } = string.Empty;

    public long Add(IBasicModbusClientSession session)
    {
        var handle = Interlocked.Increment(ref _nextHandle);
        _sessions[handle] = new ModbusClientSession(session);
        return handle;
    }

    public bool TryGet(long handle, out ModbusClientSession session)
        => _sessions.TryGetValue(handle, out session!);

    public bool TryRemove(long handle, out ModbusClientSession session)
        => _sessions.TryRemove(handle, out session!);

    public void SetLastError(string message)
        => LastError = message;

    public void ClearLastError()
        => LastError = string.Empty;

    public void Dispose()
    {
        foreach (var pair in _sessions.ToArray())
        {
            if (!_sessions.TryRemove(pair.Key, out var session))
            {
                continue;
            }

            try
            {
                session.Dispose();
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }
}

internal sealed class ModbusClientSession : IDisposable
{
    private int _disposed;

    public ModbusClientSession(IBasicModbusClientSession client)
    {
        Client = client;
    }

    public IBasicModbusClientSession Client { get; }

    public string LastError { get; private set; } = string.Empty;

    public void SetLastError(string message)
        => LastError = message;

    public void ClearLastError()
        => LastError = string.Empty;

    public BasicModbusReadResult Read(BasicModbusReadRequest request)
    {
        EnsureActive();
        return Client.Read(request);
    }

    public BasicModbusWriteResult Write(BasicModbusWriteRequest request)
    {
        EnsureActive();
        return Client.Write(request);
    }

    public void Close()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            Client.Close();
        }
        finally
        {
            Client.Dispose();
        }
    }

    public void Dispose()
        => Close();

    private void EnsureActive()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new InvalidOperationException("Modbus 客户端已关闭。");
        }
    }
}

internal sealed class SystemBasicModbusClientFactory : IBasicModbusClientFactory
{
    public IBasicModbusClientSession Open(BasicModbusConnectionOptions options)
        => new SystemBasicModbusClientSession(options);
}

internal sealed class SystemBasicModbusClientSession : IBasicModbusClientSession
{
    private readonly IModbusClient _client;
    private readonly BasicModbusConnectionOptions _options;
    private int _disposed;

    public SystemBasicModbusClientSession(BasicModbusConnectionOptions options)
    {
        _options = options;
        _client = CreateClient(options);
        EnsureOpen();
    }

    public BasicModbusConnectionOptions Options => _options;

    public bool IsConnected => Volatile.Read(ref _disposed) == 0 && _client.Connected;

    public string LastError { get; private set; } = string.Empty;

    public BasicModbusReadResult Read(BasicModbusReadRequest request)
    {
        try
        {
            EnsureOpen();
            var result = request.ValueKind switch
            {
                BasicModbusValueKind.Bool => ReadBool(request),
                BasicModbusValueKind.Int16 => ReadTyped(_client.ReadInt16(request.Address, request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.UInt16 => ReadTyped(_client.ReadUInt16(request.Address, request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.Int32 => ReadTyped(_client.ReadInt32(request.Address, request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.UInt32 => ReadTyped(_client.ReadUInt32(request.Address, request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.Int64 => ReadTyped(_client.ReadInt64(request.Address, request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.UInt64 => ReadTyped(_client.ReadUInt64(request.Address, request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.Float => ReadTyped(_client.ReadFloat(request.Address, request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.Double => ReadTyped(_client.ReadDouble(request.Address, request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.String => ReadString(request),
                BasicModbusValueKind.Raw => ReadRaw(request),
                _ => new BasicModbusReadResult(false, null, $"不支持的 Modbus 数据类型“{request.ValueKind}”。")
            };

            if (!result.Success)
            {
                return result;
            }

            ClearLastError();
            return result;
        }
        catch (Exception ex)
        {
            return FailRead($"Modbus 读取失败：{Unwrap(ex).Message}");
        }
    }

    public BasicModbusWriteResult Write(BasicModbusWriteRequest request)
    {
        try
        {
            EnsureOpen();
            var result = request.ValueKind switch
            {
                BasicModbusValueKind.Bool => ToWriteResult(_client.Write(request.Address, Convert.ToBoolean(request.Value), request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.Int16 => ToWriteResult(_client.Write(request.Address, Convert.ToInt16(request.Value, CultureInfo.InvariantCulture), request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.UInt16 => ToWriteResult(_client.Write(request.Address, Convert.ToUInt16(request.Value, CultureInfo.InvariantCulture), request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.Int32 => ToWriteResult(_client.Write(request.Address, Convert.ToInt32(request.Value, CultureInfo.InvariantCulture), request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.UInt32 => ToWriteResult(_client.Write(request.Address, Convert.ToUInt32(request.Value, CultureInfo.InvariantCulture), request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.Int64 => ToWriteResult(_client.Write(request.Address, Convert.ToInt64(request.Value, CultureInfo.InvariantCulture), request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.UInt64 => ToWriteResult(_client.Write(request.Address, Convert.ToUInt64(request.Value, CultureInfo.InvariantCulture), request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.Float => ToWriteResult(_client.Write(request.Address, Convert.ToSingle(request.Value, CultureInfo.InvariantCulture), request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.Double => ToWriteResult(_client.Write(request.Address, Convert.ToDouble(request.Value, CultureInfo.InvariantCulture), request.StationNumber, request.FunctionCode)),
                BasicModbusValueKind.String => ToWriteResult(_client.Write(request.Address, ConvertToBytes(request.Value?.ToString() ?? string.Empty, request.Encoding), request.StationNumber, request.FunctionCode, _options.PlcAddresses)),
                BasicModbusValueKind.Raw => ToWriteResult(_client.Write(request.Address, ConvertToBytes(request.Value, request.Encoding), request.StationNumber, request.FunctionCode, _options.PlcAddresses)),
                _ => new BasicModbusWriteResult(false, $"不支持的 Modbus 数据类型“{request.ValueKind}”。")
            };

            if (!result.Success)
            {
                return result;
            }

            ClearLastError();
            return result;
        }
        catch (Exception ex)
        {
            return FailWrite($"Modbus 写入失败：{Unwrap(ex).Message}");
        }
    }

    public void Close()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            if (_client.Connected)
            {
                var result = _client.Close();
                if (!result.IsSucceed)
                {
                    throw new InvalidOperationException(result.Err ?? "Modbus 关闭失败。");
                }
            }
        }
        finally
        {
            if (_client is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public void Dispose()
        => Close();

    public void SetLastError(string message)
        => LastError = message;

    public void ClearLastError()
        => LastError = string.Empty;

    private BasicModbusReadResult ReadBool(BasicModbusReadRequest request)
    {
        var result = request.FunctionCode == 2
            ? _client.ReadDiscrete(request.Address, request.StationNumber, request.FunctionCode)
            : _client.ReadCoil(request.Address, request.StationNumber, request.FunctionCode);

        return result.IsSucceed
            ? new BasicModbusReadResult(true, result.Value, null)
            : FailRead(result.Err ?? "Modbus 布尔读取失败。");
    }

    private BasicModbusReadResult ReadTyped<T>(IoTClient.Result<T> result)
        => result.IsSucceed
            ? new BasicModbusReadResult(true, result.Value, null)
            : FailRead(result.Err ?? "Modbus 读取失败。");

    private BasicModbusReadResult ReadString(BasicModbusReadRequest request)
    {
        var result = _client.Read(request.Address, request.StationNumber, request.FunctionCode, checked((ushort)Math.Max(0, request.Length)), _options.PlcAddresses);
        if (!result.IsSucceed)
        {
            return FailRead(result.Err ?? "Modbus 字符串读取失败。");
        }

        var bytes = result.Value ?? Array.Empty<byte>();
        var text = bytes.Length == 0 ? string.Empty : request.Encoding.GetString(bytes).TrimEnd('\0');
        return new BasicModbusReadResult(true, text, null);
    }

    private BasicModbusReadResult ReadRaw(BasicModbusReadRequest request)
    {
        var result = _client.Read(request.Address, request.StationNumber, request.FunctionCode, checked((ushort)Math.Max(0, request.Length)), _options.PlcAddresses);
        return result.IsSucceed
            ? new BasicModbusReadResult(true, result.Value ?? Array.Empty<byte>(), null)
            : FailRead(result.Err ?? "Modbus 原始字节读取失败。");
    }

    private static BasicModbusWriteResult ToWriteResult(IoTClient.Result result)
        => result.IsSucceed
            ? new BasicModbusWriteResult(true, null)
            : new BasicModbusWriteResult(false, result.Err ?? "Modbus 写入失败。");

    private BasicModbusReadResult FailRead(string message)
    {
        SetLastError(message);
        return new BasicModbusReadResult(false, null, message);
    }

    private BasicModbusWriteResult FailWrite(string message)
    {
        SetLastError(message);
        return new BasicModbusWriteResult(false, message);
    }

    private void EnsureOpen()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new InvalidOperationException($"Modbus 会话“{_options.Transport}”已关闭。");
        }

        if (_client.Connected)
        {
            return;
        }

        var result = _client.Open();
        if (!result.IsSucceed)
        {
            throw new InvalidOperationException(result.Err ?? "Modbus 连接失败。");
        }
    }

    private static IModbusClient CreateClient(BasicModbusConnectionOptions options)
    {
        return options.Transport switch
        {
            BasicModbusTransport.Tcp => new ModbusTcpClient(options.Host, options.Port, options.TimeoutMs, options.EndianFormat, options.PlcAddresses),
            BasicModbusTransport.RtuOverTcp => new ModbusRtuOverTcpClient(options.Host, options.Port, options.TimeoutMs, options.EndianFormat, options.PlcAddresses),
            BasicModbusTransport.Rtu => new ModbusRtuClient(options.PortName, options.BaudRate, options.TimeoutMs, options.StopBits, options.Parity, options.DataBits, options.EndianFormat, options.PlcAddresses),
            BasicModbusTransport.Ascii => new ModbusAsciiClient(options.PortName, options.BaudRate, options.TimeoutMs, options.StopBits, options.Parity, options.DataBits, options.EndianFormat, options.PlcAddresses),
            _ => throw new BasicRuntimeException("不支持的 Modbus 传输方式。")
        };
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is AggregateException aggregate && aggregate.InnerException is not null)
        {
            exception = aggregate.InnerException;
        }

        return exception;
    }

    private static byte[] ConvertToBytes(object? value, Encoding encoding)
    {
        if (value is null)
        {
            return Array.Empty<byte>();
        }

        if (value is byte[] bytes)
        {
            return bytes;
        }

        if (value is string text)
        {
            return encoding.GetBytes(text);
        }

        if (TryConvertObjectToByte(value, out var byteValue))
        {
            return [byteValue];
        }

        if (value is IEnumerable<object?> objects && TryConvertObjectsToBytes(objects, out var objectBytes))
        {
            return objectBytes;
        }

        if (value is IEnumerable<byte> byteValues)
        {
            return byteValues.ToArray();
        }

        return encoding.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static bool TryConvertObjectsToBytes(IEnumerable<object?> values, out byte[] bytes)
    {
        var buffer = new List<byte>();
        foreach (var value in values)
        {
            if (!TryConvertObjectToByte(value, out var byteValue))
            {
                bytes = Array.Empty<byte>();
                return false;
            }

            buffer.Add(byteValue);
        }

        bytes = buffer.ToArray();
        return true;
    }

    private static bool TryConvertObjectToByte(object? value, out byte byteValue)
    {
        byteValue = 0;
        switch (value)
        {
            case byte b:
                byteValue = b;
                return true;
            case sbyte sb when sb >= byte.MinValue:
                byteValue = (byte)sb;
                return true;
            case short s when s >= byte.MinValue && s <= byte.MaxValue:
                byteValue = (byte)s;
                return true;
            case ushort us when us <= byte.MaxValue:
                byteValue = (byte)us;
                return true;
            case int i when i >= byte.MinValue && i <= byte.MaxValue:
                byteValue = (byte)i;
                return true;
            case uint ui when ui <= byte.MaxValue:
                byteValue = (byte)ui;
                return true;
            case long l when l >= byte.MinValue && l <= byte.MaxValue:
                byteValue = (byte)l;
                return true;
            case ulong ul when ul <= byte.MaxValue:
                byteValue = (byte)ul;
                return true;
            case float f when Math.Abs(f % 1) < 0.0000000001d && f >= byte.MinValue && f <= byte.MaxValue:
                byteValue = (byte)f;
                return true;
            case double d when Math.Abs(d % 1) < 0.0000000001d && d >= byte.MinValue && d <= byte.MaxValue:
                byteValue = (byte)d;
                return true;
            case decimal m when decimal.Truncate(m) == m && m >= byte.MinValue && m <= byte.MaxValue:
                byteValue = (byte)m;
                return true;
            default:
                return false;
        }
    }
}
