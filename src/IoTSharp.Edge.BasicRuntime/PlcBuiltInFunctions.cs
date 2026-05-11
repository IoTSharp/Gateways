using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using IoTClient;
using IoTClient.Clients.PLC;
using IoTClient.Common.Enums;
using IoTClient.Enums;
using IoTClient.Interfaces;
using static IoTSharp.Edge.BasicRuntime.PlcBuiltInFunctions;

namespace IoTSharp.Edge.BasicRuntime;

public sealed record BasicSiemensConnectionOptions
{
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 102;

    public SiemensVersion Version { get; init; } = SiemensVersion.S7_1200;

    public byte Rack { get; init; }

    public byte Slot { get; init; }

    public int TimeoutMs { get; init; } = 1_500;
}

public sealed record BasicMitsubishiConnectionOptions
{
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 6_000;

    public MitsubishiVersion Version { get; init; } = MitsubishiVersion.Qna_3E;

    public int TimeoutMs { get; init; } = 1_500;
}

public sealed record BasicOmronFinsConnectionOptions
{
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 9_600;

    public int TimeoutMs { get; init; } = 1_500;

    public EndianFormat EndianFormat { get; init; } = EndianFormat.ABCD;
}

public sealed record BasicAllenBradleyConnectionOptions
{
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 44818;

    public byte Slot { get; init; }

    public int TimeoutMs { get; init; } = 1_500;
}

public sealed record BasicPlcReadRequest(
    string Address,
    DataTypeEnum DataType,
    int Count,
    Encoding Encoding);

public sealed record BasicPlcWriteRequest(
    string Address,
    DataTypeEnum DataType,
    object? Value,
    Encoding Encoding);

public sealed record BasicPlcBatchReadRequest(
    IReadOnlyDictionary<string, DataTypeEnum> Addresses,
    int BatchNumber);

public sealed record BasicPlcBatchWriteRequest(
    IReadOnlyDictionary<string, object?> Values,
    int BatchNumber);

public sealed record BasicPlcOperationResult(bool Success, string? Error);

public sealed record BasicPlcReadResult(bool Success, object? Value, string? Error);

public sealed record BasicPlcWriteResult(bool Success, string? Error);

public interface IBasicPlcClientFactory
{
    IBasicPlcClientSession OpenSiemens(BasicSiemensConnectionOptions options);

    IBasicPlcClientSession OpenMitsubishi(BasicMitsubishiConnectionOptions options);

    IBasicPlcClientSession OpenOmronFins(BasicOmronFinsConnectionOptions options);

    IBasicPlcClientSession OpenAllenBradley(BasicAllenBradleyConnectionOptions options);
}

public interface IBasicPlcClientSession : IDisposable
{
    string Version { get; }

    bool IsConnected { get; }

    string LastError { get; }

    BasicPlcOperationResult Open();

    BasicPlcOperationResult Close();

    BasicPlcReadResult Read(BasicPlcReadRequest request);

    BasicPlcWriteResult Write(BasicPlcWriteRequest request);

    BasicPlcReadResult BatchRead(BasicPlcBatchReadRequest request);

    BasicPlcWriteResult BatchWrite(BasicPlcBatchWriteRequest request);

    BasicPlcReadResult SendPackage(byte[] command);
}

internal static class PlcBuiltInFunctions
{
    private static readonly (string Suffix, DataTypeEnum Type)[] TypedFunctions =
    [
        ("BOOL", DataTypeEnum.Bool),
        ("BOOLEAN", DataTypeEnum.Bool),
        ("BYTE", DataTypeEnum.Byte),
        ("INT16", DataTypeEnum.Int16),
        ("UINT16", DataTypeEnum.UInt16),
        ("INT32", DataTypeEnum.Int32),
        ("UINT32", DataTypeEnum.UInt32),
        ("INT64", DataTypeEnum.Int64),
        ("UINT64", DataTypeEnum.UInt64),
        ("FLOAT", DataTypeEnum.Float),
        ("DOUBLE", DataTypeEnum.Double),
        ("STRING", DataTypeEnum.String)
    ];

    public static void Register(BasicRuntime runtime)
    {
        RegisterProtocol(
            runtime,
            runtime.SiemensState,
            "SIEMENS",
            "西门子",
            args => runtime.PlcClientFactory.OpenSiemens(ParseSiemensOptions(args)));

        RegisterProtocol(
            runtime,
            runtime.MitsubishiState,
            "MITSUBISHI",
            "三菱",
            args => runtime.PlcClientFactory.OpenMitsubishi(ParseMitsubishiOptions(args)));

        RegisterProtocol(
            runtime,
            runtime.OmronFinsState,
            "OMRON_FINS",
            "欧姆龙 FINS",
            args => runtime.PlcClientFactory.OpenOmronFins(ParseOmronFinsOptions(args)));

        RegisterProtocol(
            runtime,
            runtime.AllenBradleyState,
            "ALLEN_BRADLEY",
            "艾伦-布拉德利",
            args => runtime.PlcClientFactory.OpenAllenBradley(ParseAllenBradleyOptions(args)));
    }

    private static void RegisterProtocol(
        BasicRuntime runtime,
        PlcRuntimeState state,
        string prefix,
        string displayName,
        Func<IReadOnlyList<BasicValue>, IBasicPlcClientSession> openSession)
    {
        var functionPrefix = prefix + "_";
        runtime.RegisterInternalFunction(functionPrefix + "CONNECT", (_, args) => Connect(state, displayName, args, openSession));
        runtime.RegisterInternalFunction(functionPrefix + "CLOSE", (_, args) => Close(state, displayName, args));
        runtime.RegisterInternalFunction(functionPrefix + "CONNECTED", (_, args) => Connected(state, displayName, args));
        runtime.RegisterInternalFunction(functionPrefix + "VERSION", (_, args) => Version(state, displayName, args));
        runtime.RegisterInternalFunction(functionPrefix + "LAST_ERROR", (_, args) => GetLastError(state, displayName, args));
        runtime.RegisterInternalFunction(functionPrefix + "READ", (_, args) => Read(state, prefix, args, null, generic: true));
        runtime.RegisterInternalFunction(functionPrefix + "WRITE", (_, args) => Write(state, prefix, args, null, generic: true));
        runtime.RegisterInternalFunction(functionPrefix + "BATCH_READ", (_, args) => BatchRead(state, prefix, args));
        runtime.RegisterInternalFunction(functionPrefix + "BATCH_WRITE", (_, args) => BatchWrite(state, prefix, args));
        runtime.RegisterInternalFunction(functionPrefix + "SEND_PACKAGE", (_, args) => SendPackage(state, prefix, args));

        foreach (var (suffix, type) in TypedFunctions)
        {
            runtime.RegisterInternalFunction(functionPrefix + "READ_" + suffix, (_, args) => Read(state, prefix, args, type, generic: false));
            runtime.RegisterInternalFunction(functionPrefix + "WRITE_" + suffix, (_, args) => Write(state, prefix, args, type, generic: false));
        }
    }

    private static BasicValue Connect(
        PlcRuntimeState state,
        string protocolName,
        IReadOnlyList<BasicValue> args,
        Func<IReadOnlyList<BasicValue>, IBasicPlcClientSession> openSession)
    {
        try
        {
            var session = openSession(args);
            var opened = session.Open();
            if (!opened.Success)
            {
                session.Dispose();
                return Fail(state, $"{protocolName}连接失败：{opened.Error ?? "未知错误"}", BasicValue.FromNumber(0));
            }

            var handle = state.Add(session);
            state.ClearLastError();
            return BasicValue.FromNumber(handle);
        }
        catch (Exception ex)
        {
            return Fail(state, $"{protocolName}连接失败：{Unwrap(ex).Message}", BasicValue.FromNumber(0));
        }
    }

    private static BasicValue Close(PlcRuntimeState state, string protocolName, IReadOnlyList<BasicValue> args)
    {
        if (!TryGetSession(state, args, 0, out var session, out var handle))
        {
            return BasicValue.FromBoolean(false);
        }

        try
        {
            state.TryRemove(handle, out _);
            var result = session.Close();
            if (!result.Success)
            {
                return Fail(state, $"{protocolName}关闭失败：{result.Error ?? "未知错误"}", BasicValue.FromBoolean(false), session);
            }

            state.ClearLastError();
            return BasicValue.FromBoolean(true);
        }
        catch (Exception ex)
        {
            return Fail(state, $"{protocolName}关闭失败：{Unwrap(ex).Message}", BasicValue.FromBoolean(false), session);
        }
    }

    private static BasicValue Connected(PlcRuntimeState state, string protocolName, IReadOnlyList<BasicValue> args)
    {
        if (!TryGetSession(state, args, 0, out var session, out _))
        {
            return Fail(state, $"未找到{protocolName}句柄。", BasicValue.FromBoolean(false));
        }

        try
        {
            state.ClearLastError();
            return BasicValue.FromBoolean(session.IsConnected);
        }
        catch (Exception ex)
        {
            return Fail(state, $"{protocolName}连接状态检查失败：{Unwrap(ex).Message}", BasicValue.FromBoolean(false), session);
        }
    }

    private static BasicValue Version(PlcRuntimeState state, string protocolName, IReadOnlyList<BasicValue> args)
    {
        if (!TryGetSession(state, args, 0, out var session, out _))
        {
            return Fail(state, $"未找到{protocolName}句柄。", BasicValue.FromString(string.Empty));
        }

        try
        {
            state.ClearLastError();
            return BasicValue.FromString(session.Version);
        }
        catch (Exception ex)
        {
            return Fail(state, $"{protocolName}版本读取失败：{Unwrap(ex).Message}", BasicValue.FromString(string.Empty), session);
        }
    }

    private static BasicValue GetLastError(PlcRuntimeState state, string protocolName, IReadOnlyList<BasicValue> args)
    {
        if (TryGetHandle(args, 0, out var handle) && state.TryGet(handle, out var session))
        {
            var sessionError = session.LastError;
            if (!string.IsNullOrWhiteSpace(sessionError))
            {
                return BasicValue.FromString(sessionError);
            }
        }

        return BasicValue.FromString(state.LastError);
    }

    private static BasicValue Read(
        PlcRuntimeState state,
        string protocolName,
        IReadOnlyList<BasicValue> args,
        DataTypeEnum? fixedDataType,
        bool generic)
    {
        if (!TryGetSession(state, args, 0, out var session, out _))
        {
            return BasicValue.Nil;
        }

        try
        {
            var request = CreateReadRequest(args, fixedDataType, generic);
            var result = session.Read(request);
            if (!result.Success)
            {
                return Fail(state, $"{protocolName}读取失败：{result.Error ?? "未知错误"}", BasicValue.Nil, session);
            }

            state.ClearLastError();
            return ToBasicValue(result.Value);
        }
        catch (Exception ex)
        {
            return Fail(state, $"{protocolName}读取失败：{Unwrap(ex).Message}", BasicValue.Nil, session);
        }
    }

    private static BasicValue Write(
        PlcRuntimeState state,
        string protocolName,
        IReadOnlyList<BasicValue> args,
        DataTypeEnum? fixedDataType,
        bool generic)
    {
        if (!TryGetSession(state, args, 0, out var session, out _))
        {
            return BasicValue.FromBoolean(false);
        }

        try
        {
            var request = CreateWriteRequest(args, fixedDataType, generic);
            var result = session.Write(request);
            if (!result.Success)
            {
                return Fail(state, $"{protocolName}写入失败：{result.Error ?? "未知错误"}", BasicValue.FromBoolean(false), session);
            }

            state.ClearLastError();
            return BasicValue.FromBoolean(true);
        }
        catch (Exception ex)
        {
            return Fail(state, $"{protocolName}写入失败：{Unwrap(ex).Message}", BasicValue.FromBoolean(false), session);
        }
    }

    private static BasicValue BatchRead(PlcRuntimeState state, string protocolName, IReadOnlyList<BasicValue> args)
    {
        if (!TryGetSession(state, args, 0, out var session, out _))
        {
            return BasicValue.Nil;
        }

        try
        {
            var request = new BasicPlcBatchReadRequest(CreateBatchReadMap(Arg(args, 1)), OptionalInt(args, 2, 19));
            var result = session.BatchRead(request);
            if (!result.Success)
            {
                return Fail(state, $"{protocolName}批量读取失败：{result.Error ?? "未知错误"}", BasicValue.Nil, session);
            }

            state.ClearLastError();
            return ToBasicValue(result.Value);
        }
        catch (Exception ex)
        {
            return Fail(state, $"{protocolName}批量读取失败：{Unwrap(ex).Message}", BasicValue.Nil, session);
        }
    }

    private static BasicValue BatchWrite(PlcRuntimeState state, string protocolName, IReadOnlyList<BasicValue> args)
    {
        if (!TryGetSession(state, args, 0, out var session, out _))
        {
            return BasicValue.FromBoolean(false);
        }

        try
        {
            var request = new BasicPlcBatchWriteRequest(CreateBatchWriteMap(Arg(args, 1)), OptionalInt(args, 2, 19));
            var result = session.BatchWrite(request);
            if (!result.Success)
            {
                return Fail(state, $"{protocolName}批量写入失败：{result.Error ?? "未知错误"}", BasicValue.FromBoolean(false), session);
            }

            state.ClearLastError();
            return BasicValue.FromBoolean(true);
        }
        catch (Exception ex)
        {
            return Fail(state, $"{protocolName}批量写入失败：{Unwrap(ex).Message}", BasicValue.FromBoolean(false), session);
        }
    }

    private static BasicValue SendPackage(PlcRuntimeState state, string protocolName, IReadOnlyList<BasicValue> args)
    {
        if (!TryGetSession(state, args, 0, out var session, out _))
        {
            return BasicValue.Nil;
        }

        try
        {
            var encoding = ParseEncoding(args, 2);
            var command = ConvertToBytes(Arg(args, 1), encoding);
            var result = session.SendPackage(command);
            if (!result.Success)
            {
                return Fail(state, $"{protocolName}发送数据包失败：{result.Error ?? "未知错误"}", BasicValue.Nil, session);
            }

            state.ClearLastError();
            return ToBasicValue(result.Value);
        }
        catch (Exception ex)
        {
            return Fail(state, $"{protocolName}发送数据包失败：{Unwrap(ex).Message}", BasicValue.Nil, session);
        }
    }

    private static BasicPlcReadRequest CreateReadRequest(IReadOnlyList<BasicValue> args, DataTypeEnum? fixedDataType, bool generic)
    {
        var address = RequiredText(args, 1, "需要 PLC 地址。");
        var dataType = fixedDataType ?? ParseDataType(RequiredText(args, 2, "需要 PLC 数据类型。"));
        var countIndex = generic ? 3 : 2;
        var encodingIndex = generic ? 4 : 3;

        return new BasicPlcReadRequest(address.Trim(), dataType, OptionalInt(args, countIndex, 0), ParseEncoding(args, encodingIndex));
    }

    private static BasicPlcWriteRequest CreateWriteRequest(IReadOnlyList<BasicValue> args, DataTypeEnum? fixedDataType, bool generic)
    {
        var address = RequiredText(args, 1, "需要 PLC 地址。");
        var dataType = fixedDataType ?? ParseDataType(RequiredText(args, 2, "需要 PLC 数据类型。"));
        var valueIndex = generic ? 3 : 2;
        var encodingIndex = generic ? 4 : 3;

        return new BasicPlcWriteRequest(address.Trim(), dataType, ConvertWriteValue(Arg(args, valueIndex), dataType), ParseEncoding(args, encodingIndex));
    }

    private static IReadOnlyDictionary<string, DataTypeEnum> CreateBatchReadMap(BasicValue value)
    {
        var dictionary = ExpectDictionary(value, "PLC 批量读取需要地址到数据类型的字典。");
        var map = new Dictionary<string, DataTypeEnum>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in dictionary.Keys)
        {
            map[key] = ParseDataType(dictionary.Get(key));
        }

        return map;
    }

    private static IReadOnlyDictionary<string, object?> CreateBatchWriteMap(BasicValue value)
    {
        var dictionary = ExpectDictionary(value, "PLC 批量写入需要地址到值的字典。");
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in dictionary.Keys)
        {
            map[key] = ToPlainObject(dictionary.Get(key));
        }

        return map;
    }

    private static BasicSiemensConnectionOptions ParseSiemensOptions(IReadOnlyList<BasicValue> args)
        => new()
        {
            Host = RequiredText(args, 0, "需要西门子主机。").Trim(),
            Port = OptionalInt(args, 1, 102),
            Version = ParseEnum(args, 2, SiemensVersion.S7_1200),
            Rack = OptionalByte(args, 3, 0),
            Slot = OptionalByte(args, 4, 0),
            TimeoutMs = OptionalInt(args, 5, 1_500)
        };

    private static BasicMitsubishiConnectionOptions ParseMitsubishiOptions(IReadOnlyList<BasicValue> args)
        => new()
        {
            Host = RequiredText(args, 0, "需要三菱主机。").Trim(),
            Port = OptionalInt(args, 1, 6_000),
            Version = ParseEnum(args, 2, MitsubishiVersion.Qna_3E),
            TimeoutMs = OptionalInt(args, 3, 1_500)
        };

    private static BasicOmronFinsConnectionOptions ParseOmronFinsOptions(IReadOnlyList<BasicValue> args)
        => new()
        {
            Host = RequiredText(args, 0, "需要欧姆龙 FINS 主机。").Trim(),
            Port = OptionalInt(args, 1, 9_600),
            TimeoutMs = OptionalInt(args, 2, 1_500),
            EndianFormat = ParseEnum(args, 3, EndianFormat.ABCD)
        };

    private static BasicAllenBradleyConnectionOptions ParseAllenBradleyOptions(IReadOnlyList<BasicValue> args)
        => new()
        {
            Host = RequiredText(args, 0, "需要艾伦-布拉德利主机。").Trim(),
            Port = OptionalInt(args, 1, 44_818),
            Slot = OptionalByte(args, 2, 0),
            TimeoutMs = OptionalInt(args, 3, 1_500)
        };

    private static DataTypeEnum ParseDataType(BasicValue value)
    {
        if (value.Kind == BasicValueKind.Number && int.TryParse(value.AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric)
            && Enum.IsDefined(typeof(DataTypeEnum), numeric))
        {
            return (DataTypeEnum)numeric;
        }

        return ParseDataType(value.AsString());
    }

    private static DataTypeEnum ParseDataType(string value)
    {
        var normalized = Normalize(value);
        return normalized switch
        {
            "bool" or "boolean" or "bit" => DataTypeEnum.Bool,
            "byte" => DataTypeEnum.Byte,
            "int16" or "short" => DataTypeEnum.Int16,
            "uint16" or "ushort" => DataTypeEnum.UInt16,
            "int32" or "int" => DataTypeEnum.Int32,
            "uint32" or "uint" => DataTypeEnum.UInt32,
            "int64" or "long" => DataTypeEnum.Int64,
            "uint64" or "ulong" => DataTypeEnum.UInt64,
            "float" or "single" => DataTypeEnum.Float,
            "double" or "decimal" => DataTypeEnum.Double,
            "string" or "text" => DataTypeEnum.String,
            _ when Enum.TryParse<DataTypeEnum>(value, true, out var parsed) && Enum.IsDefined(typeof(DataTypeEnum), parsed) => parsed,
            _ when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw) && Enum.IsDefined(typeof(DataTypeEnum), raw) => (DataTypeEnum)raw,
            _ => throw new BasicRuntimeException($"不支持的 PLC 数据类型“{value}”。")
        };
    }

    private static object? ConvertWriteValue(BasicValue value, DataTypeEnum dataType)
    {
        return dataType switch
        {
            DataTypeEnum.Bool => value.AsNumber() != 0,
            DataTypeEnum.Byte => checked((byte)value.AsNumber()),
            DataTypeEnum.Int16 => checked((short)value.AsNumber()),
            DataTypeEnum.UInt16 => checked((ushort)Math.Max(0, value.AsNumber())),
            DataTypeEnum.Int32 => checked((int)value.AsNumber()),
            DataTypeEnum.UInt32 => checked((uint)Math.Max(0, value.AsNumber())),
            DataTypeEnum.Int64 => checked((long)value.AsNumber()),
            DataTypeEnum.UInt64 => checked((ulong)Math.Max(0, value.AsNumber())),
            DataTypeEnum.Float => (float)value.AsNumber(),
            DataTypeEnum.Double => value.AsNumber(),
            DataTypeEnum.String => value.AsString(),
            _ => throw new BasicRuntimeException($"不支持的 PLC 数据类型“{dataType}”。")
        };
    }

    private static object? ToPlainObject(BasicValue value)
    {
        return value.Kind switch
        {
            BasicValueKind.Nil => null,
            BasicValueKind.Number => value.ToObject(),
            BasicValueKind.String => value.Text,
            BasicValueKind.List => value.List.Items.Select(ToPlainObject).ToArray(),
            BasicValueKind.Array => value.Array.ToObjectArray().Select(ToPlainObjectObject).ToArray(),
            BasicValueKind.Dictionary => value.Dictionary.Keys.ToDictionary(key => key, key => ToPlainObject(value.Dictionary.Get(key)), StringComparer.OrdinalIgnoreCase),
            BasicValueKind.Iterator => new object?[] { "iterator", value.Iterator.Index },
            BasicValueKind.Class or BasicValueKind.Instance => value.ObjectValue.Fields.ToDictionary(pair => pair.Key, pair => ToPlainObject(pair.Value), StringComparer.OrdinalIgnoreCase),
            BasicValueKind.Callable => value.AsString(),
            _ => value.AsString()
        };
    }

    private static object? ToPlainObjectObject(object? value)
    {
        return value switch
        {
            null => null,
            BasicDictionary dictionary => dictionary.Keys.ToDictionary(key => key, key => ToPlainObject(dictionary.Get(key)), StringComparer.OrdinalIgnoreCase),
            BasicValue basicValue => ToPlainObject(basicValue),
            IEnumerable<object?> objects => objects.Select(ToPlainObjectObject).ToArray(),
            IEnumerable<byte> bytes => bytes.ToArray(),
            _ => value
        };
    }

    private static BasicValue ToBasicValue(object? value)
    {
        if (value is null)
        {
            return BasicValue.Nil;
        }

        return value switch
        {
            BasicValue basicValue => basicValue,
            BasicDictionary dictionary => BasicValue.FromDictionary(dictionary),
            IDictionary dictionary => BasicValue.FromDictionary(ToBasicDictionary(dictionary)),
            byte[] bytes => BasicValue.FromList(new BasicList(bytes.Select(byteValue => BasicValue.FromNumber(byteValue)))),
            IEnumerable<byte> bytes => BasicValue.FromList(new BasicList(bytes.Select(byteValue => BasicValue.FromNumber(byteValue)))),
            string text => BasicValue.FromString(text),
            IEnumerable<KeyValuePair<string, object?>> keyValuePairs => BasicValue.FromDictionary(ToBasicDictionary(keyValuePairs)),
            IEnumerable enumerable when value is not string => BasicValue.FromList(new BasicList(enumerable.Cast<object?>().Select(ToBasicValue))),
            _ => BasicValue.FromObject(value)
        };
    }

    private static BasicDictionary ToBasicDictionary(IDictionary dictionary)
    {
        var basicDictionary = new BasicDictionary();
        foreach (DictionaryEntry entry in dictionary)
        {
            basicDictionary.Set(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty, ToBasicValue(entry.Value));
        }

        return basicDictionary;
    }

    private static BasicDictionary ToBasicDictionary(IEnumerable<KeyValuePair<string, object?>> keyValuePairs)
    {
        var basicDictionary = new BasicDictionary();
        foreach (var pair in keyValuePairs)
        {
            basicDictionary.Set(pair.Key, ToBasicValue(pair.Value));
        }

        return basicDictionary;
    }

    private static BasicValue Fail(PlcRuntimeState state, string message, BasicValue failureValue, IBasicPlcClientSession? session = null)
    {
        state.SetLastError(message);
        return failureValue;
    }

    private static BasicValue Arg(IReadOnlyList<BasicValue> args, int index)
        => index >= 0 && index < args.Count ? args[index] : BasicValue.Nil;

    private static string RequiredText(IReadOnlyList<BasicValue> args, int index, string message)
    {
        var value = OptionalText(args, index);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BasicRuntimeException(message);
        }

        return value;
    }

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

    private static string Normalize(string value)
        => value.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();

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

    private static Encoding ParseEncoding(IReadOnlyList<BasicValue> args, int index)
    {
        var name = OptionalText(args, index, Encoding.UTF8.WebName) ?? Encoding.UTF8.WebName;
        try
        {
            return Encoding.GetEncoding(name);
        }
        catch (Exception ex)
        {
            throw new BasicRuntimeException($"编码“{name}”不受支持：{ex.Message}");
        }
    }

    private static TEnum ParseEnum<TEnum>(IReadOnlyList<BasicValue> args, int index, TEnum fallback)
        where TEnum : struct, Enum
    {
        var value = OptionalText(args, index);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (Enum.TryParse<TEnum>(value, true, out var parsed) && Enum.IsDefined(typeof(TEnum), parsed))
        {
            return parsed;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) && Enum.IsDefined(typeof(TEnum), numeric))
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), numeric);
        }

        throw new BasicRuntimeException($"值“{value}”不是受支持的 {typeof(TEnum).Name}。");
    }

    private static BasicDictionary ExpectDictionary(BasicValue value, string message)
    {
        if (value.Kind != BasicValueKind.Dictionary)
        {
            throw new BasicRuntimeException(message);
        }

        return value.Dictionary;
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

    private static bool TryGetSession(PlcRuntimeState state, IReadOnlyList<BasicValue> args, int index, out IBasicPlcClientSession session, out long handle)
    {
        session = null!;
        handle = 0;

        if (!TryGetHandle(args, index, out handle))
        {
            state.SetLastError("需要 PLC 句柄。");
            return false;
        }

        if (!state.TryGet(handle, out session))
        {
            state.SetLastError("未找到 PLC 句柄。");
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

    internal static Exception Unwrap(Exception exception)
    {
        while (exception is AggregateException aggregate && aggregate.InnerException is not null)
        {
            exception = aggregate.InnerException;
        }

        return exception;
    }
}

internal sealed class PlcRuntimeState : IDisposable
{
    private readonly ConcurrentDictionary<long, IBasicPlcClientSession> _sessions = new();
    private long _nextHandle;

    public string LastError { get; private set; } = string.Empty;

    public long Add(IBasicPlcClientSession session)
    {
        var handle = Interlocked.Increment(ref _nextHandle);
        _sessions[handle] = session;
        return handle;
    }

    public bool TryGet(long handle, out IBasicPlcClientSession session)
        => _sessions.TryGetValue(handle, out session!);

    public bool TryRemove(long handle, out IBasicPlcClientSession session)
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

internal sealed class SystemBasicPlcClientFactory : IBasicPlcClientFactory
{
    public IBasicPlcClientSession OpenSiemens(BasicSiemensConnectionOptions options)
        => new SiemensPlcClientSession(new SiemensClient(options.Version, options.Host, options.Port, options.Slot, options.Rack, options.TimeoutMs));

    public IBasicPlcClientSession OpenMitsubishi(BasicMitsubishiConnectionOptions options)
        => new MitsubishiPlcClientSession(new MitsubishiClient(options.Version, options.Host, options.Port, options.TimeoutMs));

    public IBasicPlcClientSession OpenOmronFins(BasicOmronFinsConnectionOptions options)
        => new OmronFinsPlcClientSession(new OmronFinsClient(options.Host, options.Port, options.TimeoutMs, options.EndianFormat));

    public IBasicPlcClientSession OpenAllenBradley(BasicAllenBradleyConnectionOptions options)
        => new AllenBradleyPlcClientSession(new AllenBradleyClient(options.Host, options.Port, options.Slot, options.TimeoutMs));
}

internal abstract class SystemBasicPlcClientSession<TClient> : IBasicPlcClientSession
    where TClient : IIoTClient
{
    private int _disposed;

    protected SystemBasicPlcClientSession(TClient client)
    {
        Client = client;
    }

    protected TClient Client { get; }

    public string Version => Client.Version;

    public bool IsConnected => Volatile.Read(ref _disposed) == 0 && Client.Connected;

    public string LastError { get; private set; } = string.Empty;

    public virtual BasicPlcOperationResult Open()
    {
        try
        {
            EnsureActive();
            var result = Client.Open();
            return ToOperationResult(result);
        }
        catch (Exception ex)
        {
            return FailOperation($"PLC 打开失败：{Unwrap(ex).Message}");
        }
    }

    public virtual BasicPlcOperationResult Close()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return new BasicPlcOperationResult(true, null);
        }

        try
        {
            var result = Client.Close();
            if (!result.IsSucceed)
            {
                return FailOperation($"PLC 关闭失败：{result.Err ?? "未知错误"}");
            }

            ClearLastError();
            return new BasicPlcOperationResult(true, null);
        }
        catch (Exception ex)
        {
            return FailOperation($"PLC 关闭失败：{Unwrap(ex).Message}");
        }
        finally
        {
            DisposeClient();
        }
    }

    public virtual BasicPlcReadResult Read(BasicPlcReadRequest request)
    {
        try
        {
            EnsureActive();
            if (request.Count > 1)
            {
                return FailRead("当前客户端不支持多点读取数量，请使用 BATCH_READ。");
            }

            if (request.DataType == DataTypeEnum.String && request.Count > 0)
            {
                return FailRead("当前客户端不支持指定字符串长度读取。");
            }

            var result = request.DataType switch
            {
                DataTypeEnum.Bool => ToReadResult(Client.ReadBoolean(request.Address)),
                DataTypeEnum.Byte => ToReadResult(Client.ReadByte(request.Address)),
                DataTypeEnum.Int16 => ToReadResult(Client.ReadInt16(request.Address)),
                DataTypeEnum.UInt16 => ToReadResult(Client.ReadUInt16(request.Address)),
                DataTypeEnum.Int32 => ToReadResult(Client.ReadInt32(request.Address)),
                DataTypeEnum.UInt32 => ToReadResult(Client.ReadUInt32(request.Address)),
                DataTypeEnum.Int64 => ToReadResult(Client.ReadInt64(request.Address)),
                DataTypeEnum.UInt64 => ToReadResult(Client.ReadUInt64(request.Address)),
                DataTypeEnum.Float => ToReadResult(Client.ReadFloat(request.Address)),
                DataTypeEnum.Double => ToReadResult(Client.ReadDouble(request.Address)),
                DataTypeEnum.String => ToReadResult(Client.ReadString(request.Address)),
                _ => FailRead($"不支持的 PLC 数据类型“{request.DataType}”。")
            };

            return result;
        }
        catch (Exception ex)
        {
            return FailRead($"PLC read failed: {Unwrap(ex).Message}");
        }
    }

    public virtual BasicPlcWriteResult Write(BasicPlcWriteRequest request)
    {
        try
        {
            EnsureActive();
            var result = request.DataType switch
            {
                DataTypeEnum.Bool => ToWriteResult(Client.Write(request.Address, Convert.ToBoolean(request.Value, CultureInfo.InvariantCulture))),
                DataTypeEnum.Byte => ToWriteResult(Client.Write(request.Address, Convert.ToByte(request.Value, CultureInfo.InvariantCulture))),
                DataTypeEnum.Int16 => ToWriteResult(Client.Write(request.Address, Convert.ToInt16(request.Value, CultureInfo.InvariantCulture))),
                DataTypeEnum.UInt16 => ToWriteResult(Client.Write(request.Address, Convert.ToUInt16(request.Value, CultureInfo.InvariantCulture))),
                DataTypeEnum.Int32 => ToWriteResult(Client.Write(request.Address, Convert.ToInt32(request.Value, CultureInfo.InvariantCulture))),
                DataTypeEnum.UInt32 => ToWriteResult(Client.Write(request.Address, Convert.ToUInt32(request.Value, CultureInfo.InvariantCulture))),
                DataTypeEnum.Int64 => ToWriteResult(Client.Write(request.Address, Convert.ToInt64(request.Value, CultureInfo.InvariantCulture))),
                DataTypeEnum.UInt64 => ToWriteResult(Client.Write(request.Address, Convert.ToUInt64(request.Value, CultureInfo.InvariantCulture))),
                DataTypeEnum.Float => ToWriteResult(Client.Write(request.Address, Convert.ToSingle(request.Value, CultureInfo.InvariantCulture))),
                DataTypeEnum.Double => ToWriteResult(Client.Write(request.Address, Convert.ToDouble(request.Value, CultureInfo.InvariantCulture))),
                DataTypeEnum.String => ToWriteResult(Client.Write(request.Address, Convert.ToString(request.Value, CultureInfo.InvariantCulture) ?? string.Empty)),
                _ => FailWrite($"不支持的 PLC 数据类型“{request.DataType}”。")
            };

            return result;
        }
        catch (Exception ex)
        {
            return FailWrite($"PLC write failed: {Unwrap(ex).Message}");
        }
    }

    public virtual BasicPlcReadResult BatchRead(BasicPlcBatchReadRequest request)
    {
        try
        {
            EnsureActive();
            var addresses = request.Addresses.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            var result = Client.BatchRead(addresses, Math.Max(1, request.BatchNumber));
            if (!result.IsSucceed)
            {
                return FailRead($"PLC 批量读取失败：{result.Err ?? "未知错误"}");
            }

            ClearLastError();
            return new BasicPlcReadResult(true, result.Value, null);
        }
        catch (Exception ex)
        {
            return FailRead($"PLC 批量读取失败：{Unwrap(ex).Message}");
        }
    }

    public virtual BasicPlcWriteResult BatchWrite(BasicPlcBatchWriteRequest request)
    {
        try
        {
            EnsureActive();
            var addresses = request.Values.ToDictionary(pair => pair.Key, pair => pair.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            var result = Client.BatchWrite(addresses, Math.Max(1, request.BatchNumber));
            if (!result.IsSucceed)
            {
                return FailWrite($"PLC 批量写入失败：{result.Err ?? "未知错误"}");
            }

            ClearLastError();
            return new BasicPlcWriteResult(true, null);
        }
        catch (Exception ex)
        {
            return FailWrite($"PLC 批量写入失败：{Unwrap(ex).Message}");
        }
    }

    public virtual BasicPlcReadResult SendPackage(byte[] command)
    {
        try
        {
            EnsureActive();
            var result = Client.SendPackageSingle(command);
            if (!result.IsSucceed)
            {
                return FailRead($"PLC 发送数据包失败：{result.Err ?? "未知错误"}");
            }

            ClearLastError();
            return new BasicPlcReadResult(true, result.Value, null);
        }
        catch (Exception ex)
        {
            return FailRead($"PLC 发送数据包失败：{Unwrap(ex).Message}");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        DisposeClient();
    }

    protected void EnsureActive()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new InvalidOperationException($"PLC 会话“{Client.Version}”已关闭。");
        }
    }

    protected BasicPlcReadResult ToReadResult<T>(Result<T> result)
        => result.IsSucceed
            ? SuccessRead(result.Value)
            : FailRead(result.Err ?? "PLC 读取失败。");

    protected BasicPlcReadResult ToReadDictionaryResult<T>(Result<List<KeyValuePair<string, T>>> result)
    {
        if (!result.IsSucceed)
        {
            return FailRead(result.Err ?? "PLC 读取失败。");
        }

        var dictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in result.Value ?? [])
        {
            dictionary[pair.Key] = pair.Value;
        }

        ClearLastError();
        return new BasicPlcReadResult(true, dictionary, null);
    }

    protected BasicPlcOperationResult ToOperationResult(Result result)
        => result.IsSucceed
            ? SuccessOperation()
            : FailOperation(result.Err ?? "PLC 操作失败。");

    protected BasicPlcWriteResult ToWriteResult(Result result)
        => result.IsSucceed
            ? SuccessWrite()
            : FailWrite(result.Err ?? "PLC 写入失败。");

    protected BasicPlcReadResult SuccessRead(object? value)
    {
        ClearLastError();
        return new BasicPlcReadResult(true, value, null);
    }

    protected BasicPlcWriteResult SuccessWrite()
    {
        ClearLastError();
        return new BasicPlcWriteResult(true, null);
    }

    protected BasicPlcOperationResult SuccessOperation()
    {
        ClearLastError();
        return new BasicPlcOperationResult(true, null);
    }

    protected BasicPlcReadResult FailRead(string message)
    {
        SetLastError(message);
        return new BasicPlcReadResult(false, null, message);
    }

    protected BasicPlcWriteResult FailWrite(string message)
    {
        SetLastError(message);
        return new BasicPlcWriteResult(false, message);
    }

    protected BasicPlcOperationResult FailOperation(string message)
    {
        SetLastError(message);
        return new BasicPlcOperationResult(false, message);
    }

    protected void SetLastError(string message)
        => LastError = message;

    protected void ClearLastError()
        => LastError = string.Empty;

    private void DisposeClient()
    {
        try
        {
            if (Client.Connected)
            {
                Client.Close();
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is AggregateException aggregate && aggregate.InnerException is not null)
        {
            exception = aggregate.InnerException;
        }

        return exception;
    }
}

internal sealed class SiemensPlcClientSession : SystemBasicPlcClientSession<SiemensClient>
{
    public SiemensPlcClientSession(SiemensClient client)
        : base(client)
    {
    }

    public override BasicPlcReadResult Read(BasicPlcReadRequest request)
    {
        if (request.DataType == DataTypeEnum.String && request.Count > 0)
        {
            try
            {
                EnsureActive();
                var result = Client.ReadString(request.Address, checked((ushort)request.Count));
                if (!result.IsSucceed)
                {
                    return FailRead(result.Err ?? "PLC 读取失败。");
                }

                var text = request.Encoding.GetString(result.Value ?? Array.Empty<byte>()).TrimEnd('\0');
                return SuccessRead(text);
            }
            catch (Exception ex)
            {
                return FailRead($"PLC 读取失败：{Unwrap(ex).Message}");
            }
        }

        if (request.Count > 1)
        {
#pragma warning disable CS0618
            try
            {
                EnsureActive();
                var count = checked((ushort)request.Count);
                return request.DataType switch
                {
                    DataTypeEnum.Bool => ToReadDictionaryResult(Client.ReadBoolean(request.Address, count)),
                    DataTypeEnum.Byte => ToReadDictionaryResult(Client.ReadByte(request.Address, count)),
                    DataTypeEnum.Int16 => ToReadDictionaryResult(Client.ReadInt16(request.Address, count)),
                    DataTypeEnum.UInt16 => ToReadDictionaryResult(Client.ReadUInt16(request.Address, count)),
                    DataTypeEnum.Int32 => ToReadDictionaryResult(Client.ReadInt32(request.Address, count)),
                    DataTypeEnum.UInt32 => ToReadDictionaryResult(Client.ReadUInt32(request.Address, count)),
                    DataTypeEnum.Int64 => ToReadDictionaryResult(Client.ReadInt64(request.Address, count)),
                    DataTypeEnum.UInt64 => ToReadDictionaryResult(Client.ReadUInt64(request.Address, count)),
                    DataTypeEnum.Float => ToReadDictionaryResult(Client.ReadFloat(request.Address, count)),
                    DataTypeEnum.Double => ToReadDictionaryResult(Client.ReadDouble(request.Address, count)),
                    _ => FailRead($"西门子批量读取不支持数据类型“{request.DataType}”。")
                };
            }
            catch (Exception ex)
            {
                return FailRead($"PLC 读取失败：{Unwrap(ex).Message}");
            }
#pragma warning restore CS0618
        }

        return base.Read(request);
    }
}

internal sealed class MitsubishiPlcClientSession : SystemBasicPlcClientSession<MitsubishiClient>
{
    public MitsubishiPlcClientSession(MitsubishiClient client)
        : base(client)
    {
    }

    public override BasicPlcReadResult Read(BasicPlcReadRequest request)
    {
        if (request.Count > 1)
        {
            try
            {
                EnsureActive();
                var count = checked((ushort)request.Count);
                return request.DataType switch
                {
                    DataTypeEnum.Bool => ToReadDictionaryResult(Client.ReadBoolean(request.Address, count)),
                    DataTypeEnum.Int16 => ToReadDictionaryResult(Client.ReadInt16(request.Address, count)),
                    _ => FailRead($"三菱批量读取不支持数据类型“{request.DataType}”。")
                };
            }
            catch (Exception ex)
            {
                return FailRead($"PLC 读取失败：{Unwrap(ex).Message}");
            }
        }

        return base.Read(request);
    }
}

internal sealed class OmronFinsPlcClientSession : SystemBasicPlcClientSession<OmronFinsClient>
{
    public OmronFinsPlcClientSession(OmronFinsClient client)
        : base(client)
    {
    }
}

internal sealed class AllenBradleyPlcClientSession : SystemBasicPlcClientSession<AllenBradleyClient>
{
    public AllenBradleyPlcClientSession(AllenBradleyClient client)
        : base(client)
    {
    }
}
