using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Ports;
using System.Text;

namespace IoTSharp.Edge.BasicRuntime;

public enum BasicSerialBusMode
{
    Rs232,
    Rs485
}

public sealed record BasicSerialPortOptions
{
    public string PortName { get; init; } = string.Empty;

    public int BaudRate { get; init; } = 9_600;

    public int DataBits { get; init; } = 8;

    public Parity Parity { get; init; } = Parity.None;

    public StopBits StopBits { get; init; } = StopBits.One;

    public Handshake Handshake { get; init; } = Handshake.None;

    public BasicSerialBusMode Mode { get; init; } = BasicSerialBusMode.Rs232;

    public int ReadTimeoutMs { get; init; } = 1_000;

    public int WriteTimeoutMs { get; init; } = 1_000;

    public Encoding TextEncoding { get; init; } = Encoding.UTF8;

    public string NewLine { get; init; } = "\n";

    public bool DtrEnable { get; init; }

    public bool RtsEnable { get; init; }
}

public interface IBasicSerialPortFactory
{
    IBasicSerialPortSession Open(BasicSerialPortOptions options);
}

public interface IBasicSerialPortSession : IDisposable
{
    string PortName { get; }

    bool IsOpen { get; }

    int BytesToRead { get; }

    int BytesToWrite { get; }

    Encoding TextEncoding { get; }

    string NewLine { get; }

    int Read(byte[] buffer, int offset, int count);

    string ReadExisting();

    string ReadLine();

    void Write(byte[] buffer, int offset, int count);

    void Write(string text);

    void WriteLine(string text);

    void DiscardInBuffer();

    void DiscardOutBuffer();

    void SetDtrEnable(bool enabled);

    void SetRtsEnable(bool enabled);

    void Close();
}

internal static class SerialBuiltInFunctions
{
    public static void Register(BasicRuntime runtime)
    {
        runtime.RegisterInternalFunction("SERIAL_OPEN", (_, args) => Open(runtime, args));
        runtime.RegisterInternalFunction("SERIAL_CLOSE", (_, args) => Close(runtime, args));
        runtime.RegisterInternalFunction("SERIAL_AVAILABLE", (_, args) => Available(runtime, args));
        runtime.RegisterInternalFunction("SERIAL_PENDING", (_, args) => Pending(runtime, args));
        runtime.RegisterInternalFunction("SERIAL_READ", (_, args) => Read(runtime, args));
        runtime.RegisterInternalFunction("SERIAL_READ_TEXT", (_, args) => ReadText(runtime, args));
        runtime.RegisterInternalFunction("SERIAL_READ_LINE", (_, args) => ReadLine(runtime, args));
        runtime.RegisterInternalFunction("SERIAL_WRITE", (_, args) => Write(runtime, args));
        runtime.RegisterInternalFunction("SERIAL_WRITE_LINE", (_, args) => WriteLine(runtime, args));
        runtime.RegisterInternalFunction("SERIAL_SET_DTR", (_, args) => SetDtr(runtime, args));
        runtime.RegisterInternalFunction("SERIAL_SET_RTS", (_, args) => SetRts(runtime, args));
        runtime.RegisterInternalFunction("SERIAL_LAST_ERROR", (_, args) => GetLastError(runtime, args));
    }

    private static BasicValue Open(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.SerialState;
        try
        {
            var options = CreateOptions(args);
            var session = runtime.SerialPortFactory.Open(options);
            var handle = state.Add(session);
            state.ClearLastError();
            return BasicValue.FromNumber(handle);
        }
        catch (Exception ex)
        {
            return Fail(state, $"Serial open failed: {Unwrap(ex).Message}", BasicValue.FromNumber(0));
        }
    }

    private static BasicValue Close(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.SerialState;
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
            return Fail(state, $"Serial close failed: {Unwrap(ex).Message}", BasicValue.FromBoolean(false), session);
        }
    }

    private static BasicValue Available(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.SerialState;
        if (!TryGetSession(runtime, args, 0, out var session, out _))
        {
            return BasicValue.FromNumber(0);
        }

        try
        {
            state.ClearLastError();
            session.ClearLastError();
            return BasicValue.FromNumber(Math.Max(0, session.Port.BytesToRead));
        }
        catch (Exception ex)
        {
            return Fail(state, $"Serial available failed: {Unwrap(ex).Message}", BasicValue.FromNumber(0), session);
        }
    }

    private static BasicValue Pending(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.SerialState;
        if (!TryGetSession(runtime, args, 0, out var session, out _))
        {
            return BasicValue.FromNumber(0);
        }

        try
        {
            state.ClearLastError();
            session.ClearLastError();
            return BasicValue.FromNumber(Math.Max(0, session.Port.BytesToWrite));
        }
        catch (Exception ex)
        {
            return Fail(state, $"Serial pending failed: {Unwrap(ex).Message}", BasicValue.FromNumber(0), session);
        }
    }

    private static BasicValue Read(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.SerialState;
        if (!TryGetSession(runtime, args, 0, out var session, out _))
        {
            return BasicValue.Nil;
        }

        try
        {
            var bytes = ReadBytes(session.Port, OptionalInt(args, 1, 0));
            state.ClearLastError();
            session.ClearLastError();
            return BasicValue.FromList(new BasicList(bytes.Select(byteValue => BasicValue.FromNumber(byteValue))));
        }
        catch (Exception ex)
        {
            return Fail(state, $"Serial read failed: {Unwrap(ex).Message}", BasicValue.Nil, session);
        }
    }

    private static BasicValue ReadText(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.SerialState;
        if (!TryGetSession(runtime, args, 0, out var session, out _))
        {
            return BasicValue.Nil;
        }

        try
        {
            var count = OptionalInt(args, 1, 0);
            var text = count <= 0
                ? session.Port.ReadExisting()
                : session.Port.TextEncoding.GetString(ReadBytes(session.Port, count));

            state.ClearLastError();
            session.ClearLastError();
            return BasicValue.FromString(text);
        }
        catch (Exception ex)
        {
            return Fail(state, $"Serial text read failed: {Unwrap(ex).Message}", BasicValue.Nil, session);
        }
    }

    private static BasicValue ReadLine(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.SerialState;
        if (!TryGetSession(runtime, args, 0, out var session, out _))
        {
            return BasicValue.Nil;
        }

        try
        {
            state.ClearLastError();
            session.ClearLastError();
            return BasicValue.FromString(session.Port.ReadLine());
        }
        catch (Exception ex)
        {
            return Fail(state, $"Serial line read failed: {Unwrap(ex).Message}", BasicValue.Nil, session);
        }
    }

    private static BasicValue Write(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.SerialState;
        if (!TryGetSession(runtime, args, 0, out var session, out _))
        {
            return BasicValue.FromNumber(0);
        }

        try
        {
            var bytesWritten = WriteValue(session.Port, Arg(args, 1));
            state.ClearLastError();
            session.ClearLastError();
            return BasicValue.FromNumber(bytesWritten);
        }
        catch (Exception ex)
        {
            return Fail(state, $"Serial write failed: {Unwrap(ex).Message}", BasicValue.FromNumber(0), session);
        }
    }

    private static BasicValue WriteLine(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.SerialState;
        if (!TryGetSession(runtime, args, 0, out var session, out _))
        {
            return BasicValue.FromNumber(0);
        }

        try
        {
            var text = Arg(args, 1).AsString();
            session.Port.WriteLine(text);
            var bytesWritten = session.Port.TextEncoding.GetByteCount(text + session.Port.NewLine);
            state.ClearLastError();
            session.ClearLastError();
            return BasicValue.FromNumber(bytesWritten);
        }
        catch (Exception ex)
        {
            return Fail(state, $"Serial line write failed: {Unwrap(ex).Message}", BasicValue.FromNumber(0), session);
        }
    }

    private static BasicValue SetDtr(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.SerialState;
        if (!TryGetSession(runtime, args, 0, out var session, out _))
        {
            return BasicValue.FromBoolean(false);
        }

        try
        {
            session.Port.SetDtrEnable(ToBoolean(args, 1));
            state.ClearLastError();
            session.ClearLastError();
            return BasicValue.FromBoolean(true);
        }
        catch (Exception ex)
        {
            return Fail(state, $"Serial DTR update failed: {Unwrap(ex).Message}", BasicValue.FromBoolean(false), session);
        }
    }

    private static BasicValue SetRts(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.SerialState;
        if (!TryGetSession(runtime, args, 0, out var session, out _))
        {
            return BasicValue.FromBoolean(false);
        }

        try
        {
            session.Port.SetRtsEnable(ToBoolean(args, 1));
            state.ClearLastError();
            session.ClearLastError();
            return BasicValue.FromBoolean(true);
        }
        catch (Exception ex)
        {
            return Fail(state, $"Serial RTS update failed: {Unwrap(ex).Message}", BasicValue.FromBoolean(false), session);
        }
    }

    private static BasicValue GetLastError(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        if (TryGetHandle(args, 0, out var handle) && runtime.SerialState.TryGet(handle, out var session))
        {
            var sessionError = session.LastError;
            if (!string.IsNullOrWhiteSpace(sessionError))
            {
                return BasicValue.FromString(sessionError);
            }
        }

        return BasicValue.FromString(runtime.SerialState.LastError);
    }

    private static BasicValue Fail(SerialRuntimeState state, string message, BasicValue failureValue, SerialClientSession? session = null)
    {
        state.SetLastError(message);
        session?.SetLastError(message);
        return failureValue;
    }

    private static bool TryGetSession(BasicRuntime runtime, IReadOnlyList<BasicValue> args, int index, out SerialClientSession session, out long handle)
    {
        session = null!;
        handle = 0;

        if (!TryGetHandle(args, index, out handle))
        {
            runtime.SerialState.SetLastError("Serial handle is required.");
            return false;
        }

        if (!runtime.SerialState.TryGet(handle, out session))
        {
            runtime.SerialState.SetLastError("Serial handle not found.");
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

    private static BasicSerialPortOptions CreateOptions(IReadOnlyList<BasicValue> args)
    {
        var portName = RequiredText(args, 0);
        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new BasicRuntimeException("Serial port name is required.");
        }

        var mode = ParseMode(args, 6);
        var handshake = HasArgument(args, 5) && args[5].Kind != BasicValueKind.Nil
            ? ParseHandshake(args, 5)
            : mode == BasicSerialBusMode.Rs485
                ? Handshake.RequestToSend
                : Handshake.None;

        return new BasicSerialPortOptions
        {
            PortName = portName.Trim(),
            BaudRate = OptionalInt(args, 1, 9_600),
            DataBits = OptionalInt(args, 2, 8),
            Parity = ParseParity(args, 3),
            StopBits = ParseStopBits(args, 4),
            Handshake = handshake,
            Mode = mode,
            ReadTimeoutMs = OptionalInt(args, 7, 1_000),
            WriteTimeoutMs = OptionalInt(args, 8, 1_000),
            TextEncoding = ParseEncoding(args, 9),
            NewLine = OptionalText(args, 10, "\n") ?? "\n",
            DtrEnable = OptionalBool(args, 11, false),
            RtsEnable = OptionalBool(args, 12, false)
        };
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
            _ => throw new BasicRuntimeException("Serial parity must be one of N/E/O/M/S or 0-4.")
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
            _ => throw new BasicRuntimeException("Serial stop bits must be 1, 1.5, or 2.")
        };
    }

    private static Handshake ParseHandshake(IReadOnlyList<BasicValue> args, int index)
    {
        var value = OptionalText(args, index);
        if (string.IsNullOrWhiteSpace(value))
        {
            return Handshake.None;
        }

        var normalized = Normalize(value);
        return normalized switch
        {
            "0" or "none" or "no" => Handshake.None,
            "1" or "xonxoff" or "software" => Handshake.XOnXOff,
            "2" or "rts" or "rtscts" or "requesttosend" or "hardware" => Handshake.RequestToSend,
            "3" or "rtsxonxoff" or "requesttosendxonxoff" => Handshake.RequestToSendXOnXOff,
            _ when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw) && Enum.IsDefined(typeof(Handshake), raw) => (Handshake)raw,
            _ => throw new BasicRuntimeException("Serial handshake must be none, xonxoff, rtscts, or requesttosendxonxoff.")
        };
    }

    private static BasicSerialBusMode ParseMode(IReadOnlyList<BasicValue> args, int index)
    {
        var value = OptionalText(args, index);
        if (string.IsNullOrWhiteSpace(value))
        {
            return BasicSerialBusMode.Rs232;
        }

        var normalized = Normalize(value);
        return normalized switch
        {
            "0" or "232" or "rs232" or "ttl" => BasicSerialBusMode.Rs232,
            "1" or "485" or "rs485" or "halfduplex" => BasicSerialBusMode.Rs485,
            _ => throw new BasicRuntimeException("Serial mode must be rs232 or rs485.")
        };
    }

    private static Encoding ParseEncoding(IReadOnlyList<BasicValue> args, int index)
    {
        var value = OptionalText(args, index, Encoding.UTF8.WebName) ?? Encoding.UTF8.WebName;
        try
        {
            return Encoding.GetEncoding(value);
        }
        catch (Exception ex)
        {
            throw new BasicRuntimeException($"Serial encoding '{value}' is not supported: {ex.Message}");
        }
    }

    private static byte[] ReadBytes(IBasicSerialPortSession port, int requestedCount)
    {
        if (requestedCount <= 0)
        {
            requestedCount = Math.Max(0, port.BytesToRead);
        }

        if (requestedCount == 0)
        {
            return Array.Empty<byte>();
        }

        var buffer = new byte[requestedCount];
        var read = port.Read(buffer, 0, buffer.Length);
        if (read <= 0)
        {
            return Array.Empty<byte>();
        }

        if (read == buffer.Length)
        {
            return buffer;
        }

        var exact = new byte[read];
        Array.Copy(buffer, exact, read);
        return exact;
    }

    private static int WriteValue(IBasicSerialPortSession port, BasicValue value)
    {
        if (value.Kind == BasicValueKind.Nil)
        {
            return 0;
        }

        if (value.Kind == BasicValueKind.String)
        {
            port.Write(value.Text);
            return port.TextEncoding.GetByteCount(value.Text);
        }

        if (TryConvertToBytes(value, out var bytes))
        {
            if (bytes.Length > 0)
            {
                port.Write(bytes, 0, bytes.Length);
            }

            return bytes.Length;
        }

        var text = value.AsString();
        port.Write(text);
        return port.TextEncoding.GetByteCount(text);
    }

    private static bool TryConvertToBytes(BasicValue value, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();

        if (value.Kind == BasicValueKind.Number && IsByteValue(value))
        {
            bytes = [(byte)value.AsNumber()];
            return true;
        }

        if (value.Kind == BasicValueKind.List && TryConvertValuesToBytes(value.List.Items, out bytes))
        {
            return true;
        }

        if (value.Kind == BasicValueKind.Array && TryConvertObjectsToBytes(value.Array.ToObjectArray(), out bytes))
        {
            return true;
        }

        return false;
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

    private static BasicValue Arg(IReadOnlyList<BasicValue> args, int index)
        => index >= 0 && index < args.Count ? args[index] : BasicValue.Nil;

    private static bool HasArgument(IReadOnlyList<BasicValue> args, int index)
        => index >= 0 && index < args.Count;

    private static string RequiredText(IReadOnlyList<BasicValue> args, int index)
        => index >= 0 && index < args.Count ? args[index].AsString() : string.Empty;

    private static string? OptionalText(IReadOnlyList<BasicValue> args, int index, string? fallback = null)
    {
        if (!HasArgument(args, index))
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
        if (!HasArgument(args, index) || args[index].Kind == BasicValueKind.Nil)
        {
            return fallback;
        }

        return (int)args[index].AsNumber();
    }

    private static bool OptionalBool(IReadOnlyList<BasicValue> args, int index, bool fallback)
    {
        if (!HasArgument(args, index) || args[index].Kind == BasicValueKind.Nil)
        {
            return fallback;
        }

        return ToBoolean(args, index);
    }

    private static bool ToBoolean(IReadOnlyList<BasicValue> args, int index)
        => HasArgument(args, index) && args[index].AsNumber() != 0;

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
}

internal sealed class SerialRuntimeState : IDisposable
{
    private readonly ConcurrentDictionary<long, SerialClientSession> _sessions = new();
    private long _nextHandle;

    public string LastError { get; private set; } = string.Empty;

    public long Add(IBasicSerialPortSession session)
    {
        var handle = Interlocked.Increment(ref _nextHandle);
        _sessions[handle] = new SerialClientSession(session);
        return handle;
    }

    public bool TryGet(long handle, out SerialClientSession session)
        => _sessions.TryGetValue(handle, out session!);

    public bool TryRemove(long handle, out SerialClientSession session)
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

internal sealed class SerialClientSession : IDisposable
{
    private int _disposed;

    public SerialClientSession(IBasicSerialPortSession port)
    {
        Port = port;
    }

    public IBasicSerialPortSession Port { get; }

    public string LastError { get; private set; } = string.Empty;

    public void SetLastError(string message)
        => LastError = message;

    public void ClearLastError()
        => LastError = string.Empty;

    public void Close()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            Port.Close();
        }
        finally
        {
            Port.Dispose();
        }
    }

    public void Dispose()
        => Close();
}

internal sealed class SystemBasicSerialPortFactory : IBasicSerialPortFactory
{
    public IBasicSerialPortSession Open(BasicSerialPortOptions options)
        => new SystemBasicSerialPortSession(options);
}

internal sealed class SystemBasicSerialPortSession : IBasicSerialPortSession
{
    private readonly SerialPort _port;
    private readonly BasicSerialPortOptions _options;
    private int _disposed;

    public SystemBasicSerialPortSession(BasicSerialPortOptions options)
    {
        _options = options;
        _port = new SerialPort(options.PortName, options.BaudRate, options.Parity, options.DataBits, options.StopBits)
        {
            Handshake = options.Handshake,
            ReadTimeout = options.ReadTimeoutMs,
            WriteTimeout = options.WriteTimeoutMs,
            Encoding = options.TextEncoding,
            NewLine = options.NewLine,
            DtrEnable = options.DtrEnable,
            RtsEnable = options.RtsEnable
        };
        _port.Open();
    }

    public string PortName => _port.PortName;

    public bool IsOpen => Volatile.Read(ref _disposed) == 0 && _port.IsOpen;

    public int BytesToRead => IsOpen ? SafeRead(() => _port.BytesToRead) : 0;

    public int BytesToWrite => IsOpen ? SafeRead(() => _port.BytesToWrite) : 0;

    public Encoding TextEncoding => _options.TextEncoding;

    public string NewLine => _options.NewLine;

    public int Read(byte[] buffer, int offset, int count)
    {
        EnsureOpen();
        return _port.Read(buffer, offset, count);
    }

    public string ReadExisting()
    {
        EnsureOpen();
        return _port.ReadExisting();
    }

    public string ReadLine()
    {
        EnsureOpen();
        return _port.ReadLine();
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        EnsureOpen();
        _port.Write(buffer, offset, count);
    }

    public void Write(string text)
    {
        EnsureOpen();
        _port.Write(text);
    }

    public void WriteLine(string text)
    {
        EnsureOpen();
        _port.WriteLine(text);
    }

    public void DiscardInBuffer()
    {
        EnsureOpen();
        _port.DiscardInBuffer();
    }

    public void DiscardOutBuffer()
    {
        EnsureOpen();
        _port.DiscardOutBuffer();
    }

    public void SetDtrEnable(bool enabled)
    {
        EnsureOpen();
        _port.DtrEnable = enabled;
    }

    public void SetRtsEnable(bool enabled)
    {
        EnsureOpen();
        _port.RtsEnable = enabled;
    }

    public void Close()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            if (_port.IsOpen)
            {
                _port.Close();
            }
        }
        finally
        {
            _port.Dispose();
        }
    }

    public void Dispose()
        => Close();

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException($"Serial port '{PortName}' is closed.");
        }
    }

    private static int SafeRead(Func<int> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return 0;
        }
    }
}
