using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Protocol;

namespace IoTEdge.BasicRuntime;

internal static class MqttBuiltInFunctions
{
    public static void Register(BasicRuntime runtime)
    {
        runtime.RegisterInternalFunction("MQTT_CONNECT", (_, args) => Connect(runtime, args));
        runtime.RegisterInternalFunction("MQTT_DISCONNECT", (_, args) => Disconnect(runtime, args));
        runtime.RegisterInternalFunction("MQTT_PING", (_, args) => Ping(runtime, args));
        runtime.RegisterInternalFunction("MQTT_PUBLISH", (_, args) => Publish(runtime, args));
        runtime.RegisterInternalFunction("MQTT_SUBSCRIBE", (_, args) => Subscribe(runtime, args));
        runtime.RegisterInternalFunction("MQTT_UNSUBSCRIBE", (_, args) => Unsubscribe(runtime, args));
        runtime.RegisterInternalFunction("MQTT_RECEIVE", (_, args) => Receive(runtime, args));
        runtime.RegisterInternalFunction("MQTT_LAST_ERROR", (_, args) => GetLastError(runtime, args));
    }

    private static BasicValue Connect(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.MqttState;
        var endpoint = RequiredText(args, 0);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return Fail(state, "需要 MQTT 端点。");
        }

        var requestedClientId = OptionalText(args, 2, $"basic-{Guid.NewGuid():N}");
        var username = OptionalText(args, 3);
        var password = OptionalText(args, 4);
        var keepAliveSeconds = OptionalInt(args, 5, 30);
        var port = OptionalInt(args, 1, 1883);

        IMqttClient? client = null;
        try
        {
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithClientId(requestedClientId);

            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            {
                optionsBuilder = optionsBuilder.WithConnectionUri(uri);
            }
            else
            {
                optionsBuilder = optionsBuilder.WithConnectionUri(new UriBuilder
                {
                    Scheme = "mqtt",
                    Host = endpoint.Trim().Trim('[', ']'),
                    Port = port > 0 ? port : 1883
                }.Uri);
            }

            if (!string.IsNullOrWhiteSpace(username))
            {
                optionsBuilder = optionsBuilder.WithCredentials(username, password ?? string.Empty);
            }

            if (keepAliveSeconds > 0)
            {
                optionsBuilder = optionsBuilder.WithKeepAlivePeriod(TimeSpan.FromSeconds(keepAliveSeconds));
            }

            var factory = new MqttClientFactory();
            client = factory.CreateMqttClient();
            var connectResult = client.ConnectAsync(optionsBuilder.Build(), CancellationToken.None).GetAwaiter().GetResult();
            if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                client.Dispose();
                return Fail(state, $"MQTT 连接失败：{connectResult.ResultCode}{FormatReason(connectResult.ReasonString)}");
            }

            var clientId = string.IsNullOrWhiteSpace(connectResult.AssignedClientIdentifier)
                ? requestedClientId ?? string.Empty
                : connectResult.AssignedClientIdentifier;
            var session = new MqttClientSession(client, clientId);
            var handle = state.Add(session);
            state.ClearLastError();
            return BasicValue.FromNumber(handle);
        }
        catch (Exception ex)
        {
            client?.Dispose();
            return Fail(state, $"MQTT 连接失败：{Unwrap(ex).Message}");
        }
    }

    private static BasicValue Disconnect(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.MqttState;
        if (!TryGetHandle(args, 0, out var handle) || !state.TryRemove(handle, out var session))
        {
            return Fail(state, "未找到 MQTT 句柄。");
        }

        try
        {
            session.CloseAsync().GetAwaiter().GetResult();
            state.ClearLastError();
            return BasicValue.FromBoolean(true);
        }
        catch (Exception ex)
        {
            return Fail(state, $"MQTT 断开失败：{Unwrap(ex).Message}");
        }
    }

    private static BasicValue Ping(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.MqttState;
        if (!TryGetSession(runtime, args, 0, out var session))
        {
            return BasicValue.FromBoolean(false);
        }

        if (!session.Client.IsConnected)
        {
            return Fail(state, "MQTT 客户端未连接。");
        }

        try
        {
            session.Client.PingAsync(CancellationToken.None).GetAwaiter().GetResult();
            state.ClearLastError();
            session.ClearLastError();
            return BasicValue.FromBoolean(true);
        }
        catch (Exception ex)
        {
            return Fail(state, $"MQTT 心跳失败：{Unwrap(ex).Message}", session);
        }
    }

    private static BasicValue Publish(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.MqttState;
        if (!TryGetSession(runtime, args, 0, out var session))
        {
            return BasicValue.FromBoolean(false);
        }

        var topic = RequiredText(args, 1);
        if (string.IsNullOrWhiteSpace(topic))
        {
            return Fail(state, "需要 MQTT 主题。", session);
        }

        if (!session.Client.IsConnected)
        {
            return Fail(state, "MQTT 客户端未连接。", session);
        }

        var payload = args.Count > 2 ? args[2] : BasicValue.Nil;
        var qos = ToQualityOfService(args, 3);
        var retain = ToBoolean(args, 4);

        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(SerializePayload(payload))
                .WithQualityOfServiceLevel(qos)
                .WithRetainFlag(retain)
                .Build();

            var result = session.Client.PublishAsync(message, CancellationToken.None).GetAwaiter().GetResult();
            if (!result.IsSuccess)
            {
                return Fail(state, $"MQTT 发布失败：{result.ReasonCode}{FormatReason(result.ReasonString)}", session);
            }

            state.ClearLastError();
            session.ClearLastError();
            return BasicValue.FromBoolean(true);
        }
        catch (Exception ex)
        {
            return Fail(state, $"MQTT 发布失败：{Unwrap(ex).Message}", session);
        }
    }

    private static BasicValue Subscribe(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.MqttState;
        if (!TryGetSession(runtime, args, 0, out var session))
        {
            return BasicValue.FromBoolean(false);
        }

        var topic = RequiredText(args, 1);
        if (string.IsNullOrWhiteSpace(topic))
        {
            return Fail(state, "需要 MQTT 主题。", session);
        }

        if (!session.Client.IsConnected)
        {
            return Fail(state, "MQTT 客户端未连接。", session);
        }

        var qos = ToQualityOfService(args, 2);

        try
        {
            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(topic, qos, noLocal: false, retainAsPublished: false, MqttRetainHandling.SendAtSubscribe)
                .Build();

            var result = session.Client.SubscribeAsync(subscribeOptions, CancellationToken.None).GetAwaiter().GetResult();
            if (!result.Items.All(item => item.ResultCode is MqttClientSubscribeResultCode.GrantedQoS0 or MqttClientSubscribeResultCode.GrantedQoS1 or MqttClientSubscribeResultCode.GrantedQoS2))
            {
                var reason = result.Items.FirstOrDefault(item => item.ResultCode is not (MqttClientSubscribeResultCode.GrantedQoS0 or MqttClientSubscribeResultCode.GrantedQoS1 or MqttClientSubscribeResultCode.GrantedQoS2))?.ResultCode.ToString()
                    ?? result.ReasonString
                    ?? "Unknown";
                return Fail(state, $"MQTT 订阅失败：{reason}", session);
            }

            state.ClearLastError();
            session.ClearLastError();
            return BasicValue.FromBoolean(true);
        }
        catch (Exception ex)
        {
            return Fail(state, $"MQTT 订阅失败：{Unwrap(ex).Message}", session);
        }
    }

    private static BasicValue Unsubscribe(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.MqttState;
        if (!TryGetSession(runtime, args, 0, out var session))
        {
            return BasicValue.FromBoolean(false);
        }

        var topic = RequiredText(args, 1);
        if (string.IsNullOrWhiteSpace(topic))
        {
            return Fail(state, "需要 MQTT 主题。", session);
        }

        if (!session.Client.IsConnected)
        {
            return Fail(state, "MQTT 客户端未连接。", session);
        }

        try
        {
            var unsubscribeOptions = new MqttClientUnsubscribeOptionsBuilder()
                .WithTopicFilter(topic)
                .Build();

            var result = session.Client.UnsubscribeAsync(unsubscribeOptions, CancellationToken.None).GetAwaiter().GetResult();
            if (!result.Items.All(item => item.ResultCode is MqttClientUnsubscribeResultCode.Success or MqttClientUnsubscribeResultCode.NoSubscriptionExisted))
            {
                var reason = result.Items.FirstOrDefault(item => item.ResultCode is not (MqttClientUnsubscribeResultCode.Success or MqttClientUnsubscribeResultCode.NoSubscriptionExisted))?.ResultCode.ToString()
                    ?? result.ReasonString
                    ?? "Unknown";
                return Fail(state, $"MQTT 取消订阅失败：{reason}", session);
            }

            state.ClearLastError();
            session.ClearLastError();
            return BasicValue.FromBoolean(true);
        }
        catch (Exception ex)
        {
            return Fail(state, $"MQTT 取消订阅失败：{Unwrap(ex).Message}", session);
        }
    }

    private static BasicValue Receive(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        var state = runtime.MqttState;
        if (!TryGetSession(runtime, args, 0, out var session))
        {
            return BasicValue.Nil;
        }

        var timeoutMs = OptionalInt(args, 1, 0);
        var timeout = timeoutMs < 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(timeoutMs);

        try
        {
            var message = session.ReceiveAsync(timeout, CancellationToken.None).GetAwaiter().GetResult();
            if (message is null)
            {
                state.ClearLastError();
                session.ClearLastError();
                return BasicValue.Nil;
            }

            state.ClearLastError();
            session.ClearLastError();
            return BasicValue.FromDictionary(CreateMessageDictionary(message));
        }
        catch (Exception ex)
        {
            return Fail(state, $"MQTT 接收失败：{Unwrap(ex).Message}", session);
        }
    }

    private static BasicValue GetLastError(BasicRuntime runtime, IReadOnlyList<BasicValue> args)
    {
        if (TryGetHandle(args, 0, out var handle) && runtime.MqttState.TryGet(handle, out var session))
        {
            var sessionError = session.LastError;
            if (!string.IsNullOrWhiteSpace(sessionError))
            {
                return BasicValue.FromString(sessionError);
            }
        }

        return BasicValue.FromString(runtime.MqttState.LastError);
    }

    private static BasicValue Fail(MqttRuntimeState state, string message, MqttClientSession? session = null)
    {
        state.SetLastError(message);
        session?.SetLastError(message);
        return BasicValue.FromBoolean(false);
    }

    private static bool TryGetSession(BasicRuntime runtime, IReadOnlyList<BasicValue> args, int index, out MqttClientSession session)
    {
        session = null!;
        if (!TryGetHandle(args, index, out var handle))
        {
            runtime.MqttState.SetLastError("需要 MQTT 句柄。");
            return false;
        }

        if (!runtime.MqttState.TryGet(handle, out session))
        {
            runtime.MqttState.SetLastError("未找到 MQTT 句柄。");
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

    private static string RequiredText(IReadOnlyList<BasicValue> args, int index)
        => index >= 0 && index < args.Count ? args[index].AsString() : string.Empty;

    private static string? OptionalText(IReadOnlyList<BasicValue> args, int index, string? fallback = null)
    {
        if (index < 0 || index >= args.Count)
        {
            return fallback;
        }

        var text = args[index].AsString();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static int OptionalInt(IReadOnlyList<BasicValue> args, int index, int fallback)
    {
        if (index < 0 || index >= args.Count)
        {
            return fallback;
        }

        return (int)args[index].AsNumber();
    }

    private static bool ToBoolean(IReadOnlyList<BasicValue> args, int index)
        => index >= 0 && index < args.Count && args[index].AsNumber() != 0;

    private static MqttQualityOfServiceLevel ToQualityOfService(IReadOnlyList<BasicValue> args, int index)
    {
        var raw = Math.Clamp(OptionalInt(args, index, 0), 0, 2);
        return raw switch
        {
            1 => MqttQualityOfServiceLevel.AtLeastOnce,
            2 => MqttQualityOfServiceLevel.ExactlyOnce,
            _ => MqttQualityOfServiceLevel.AtMostOnce
        };
    }

    private static byte[] SerializePayload(BasicValue value)
    {
        if (TryConvertToBinary(value, out var bytes))
        {
            return bytes;
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteJsonValue(writer, value);
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static bool TryConvertToBinary(BasicValue value, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (value.Kind == BasicValueKind.Nil)
        {
            return true;
        }

        if (value.Kind == BasicValueKind.String)
        {
            bytes = Encoding.UTF8.GetBytes(value.Text);
            return true;
        }

        if (value.Kind == BasicValueKind.List && value.List.Items.All(IsByteValue))
        {
            bytes = value.List.Items.Select(item => (byte)item.AsNumber()).ToArray();
            return true;
        }

        return false;
    }

    private static bool IsByteValue(BasicValue value)
        => value.Kind == BasicValueKind.Number
            && value.AsNumber() >= byte.MinValue
            && value.AsNumber() <= byte.MaxValue
            && Math.Abs(value.AsNumber() % 1) < 0.0000000001d;

    private static void WriteJsonValue(Utf8JsonWriter writer, BasicValue value)
    {
        switch (value.Kind)
        {
            case BasicValueKind.Nil:
                writer.WriteNullValue();
                return;
            case BasicValueKind.Number:
                WriteJsonNumberValue(writer, value.AsNumber());
                return;
            case BasicValueKind.String:
                writer.WriteStringValue(value.Text);
                return;
            case BasicValueKind.List:
                writer.WriteStartArray();
                foreach (var item in value.List.Items)
                {
                    WriteJsonValue(writer, item);
                }

                writer.WriteEndArray();
                return;
            case BasicValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.Array.ToObjectArray())
                {
                    WriteJsonObjectValue(writer, item);
                }

                writer.WriteEndArray();
                return;
            case BasicValueKind.Dictionary:
                writer.WriteStartObject();
                foreach (var key in value.Dictionary.Keys)
                {
                    writer.WritePropertyName(key);
                    WriteJsonValue(writer, value.Dictionary.Get(key));
                }

                writer.WriteEndObject();
                return;
            case BasicValueKind.Iterator:
                writer.WriteStartArray();
                writer.WriteStringValue("iterator");
                writer.WriteNumberValue(value.Iterator.Index);
                writer.WriteEndArray();
                return;
            case BasicValueKind.Class:
            case BasicValueKind.Instance:
                writer.WriteStartObject();
                foreach (var pair in value.ObjectValue.Fields)
                {
                    writer.WritePropertyName(pair.Key);
                    WriteJsonValue(writer, pair.Value);
                }

                writer.WriteEndObject();
                return;
            case BasicValueKind.Callable:
                writer.WriteStringValue(value.AsString());
                return;
            default:
                writer.WriteStringValue(value.AsString());
                return;
        }
    }

    private static void WriteJsonObjectValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return;
            case BasicValue basicValue:
                WriteJsonValue(writer, basicValue);
                return;
            case BasicDictionary dictionary:
                writer.WriteStartObject();
                foreach (var key in dictionary.Keys)
                {
                    writer.WritePropertyName(key);
                    WriteJsonValue(writer, dictionary.Get(key));
                }

                writer.WriteEndObject();
                return;
            case string text:
                writer.WriteStringValue(text);
                return;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                return;
            case byte byteValue:
                writer.WriteNumberValue(byteValue);
                return;
            case sbyte sbyteValue:
                writer.WriteNumberValue(sbyteValue);
                return;
            case short shortValue:
                writer.WriteNumberValue(shortValue);
                return;
            case ushort ushortValue:
                writer.WriteNumberValue(ushortValue);
                return;
            case int intValue:
                writer.WriteNumberValue(intValue);
                return;
            case uint uintValue:
                writer.WriteNumberValue(uintValue);
                return;
            case long longValue:
                writer.WriteNumberValue(longValue);
                return;
            case ulong ulongValue:
                writer.WriteNumberValue(ulongValue);
                return;
            case float floatValue:
                writer.WriteNumberValue(floatValue);
                return;
            case double doubleValue:
                writer.WriteNumberValue(doubleValue);
                return;
            case decimal decimalValue:
                writer.WriteNumberValue(decimalValue);
                return;
            case IEnumerable<byte> bytes:
                writer.WriteStartArray();
                foreach (var byteItem in bytes)
                {
                    writer.WriteNumberValue(byteItem);
                }

                writer.WriteEndArray();
                return;
            case IEnumerable<object?> objects:
                writer.WriteStartArray();
                foreach (var item in objects)
                {
                    WriteJsonObjectValue(writer, item);
                }

                writer.WriteEndArray();
                return;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                return;
        }
    }

    private static void WriteJsonNumberValue(Utf8JsonWriter writer, double value)
    {
        if (double.IsFinite(value) && Math.Abs(value % 1) < 0.0000000001d && value <= long.MaxValue && value >= long.MinValue)
        {
            writer.WriteNumberValue((long)value);
            return;
        }

        writer.WriteNumberValue(value);
    }

    private static BasicDictionary CreateMessageDictionary(MqttReceivedMessage message)
    {
        var dictionary = new BasicDictionary();
        dictionary.Set("clientId", BasicValue.FromString(message.ClientId));
        dictionary.Set("packetId", BasicValue.FromNumber(message.PacketIdentifier));
        dictionary.Set("topic", BasicValue.FromString(message.Topic));
        dictionary.Set("payload", BasicValue.FromString(message.PayloadText));
        dictionary.Set("payloadBytes", BasicValue.FromList(new BasicList(message.PayloadBytes.Select(byteValue => BasicValue.FromNumber(byteValue)))));
        dictionary.Set("qos", BasicValue.FromNumber((int)message.QualityOfService));
        dictionary.Set("retain", BasicValue.FromBoolean(message.Retain));
        dictionary.Set("duplicate", BasicValue.FromBoolean(message.Duplicate));
        return dictionary;
    }

    private static string FormatReason(string? reason)
        => string.IsNullOrWhiteSpace(reason) ? string.Empty : $": {reason}";

    private static Exception Unwrap(Exception exception)
    {
        while (exception is AggregateException aggregate && aggregate.InnerException is not null)
        {
            exception = aggregate.InnerException;
        }

        return exception;
    }
}

internal sealed class MqttRuntimeState : IDisposable
{
    private readonly ConcurrentDictionary<long, MqttClientSession> _sessions = new();
    private long _nextHandle;

    public string LastError { get; private set; } = string.Empty;

    public long Add(MqttClientSession session)
    {
        var handle = Interlocked.Increment(ref _nextHandle);
        session.Handle = handle;
        _sessions[handle] = session;
        return handle;
    }

    public bool TryGet(long handle, out MqttClientSession session)
        => _sessions.TryGetValue(handle, out session!);

    public bool TryRemove(long handle, out MqttClientSession session)
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
                session.CloseAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }
}

internal sealed class MqttClientSession
{
    private readonly ConcurrentQueue<MqttReceivedMessage> _messages = new();
    private readonly SemaphoreSlim _signal = new(0);
    private int _disposed;

    public MqttClientSession(IMqttClient client, string clientId)
    {
        Client = client;
        ClientId = clientId;
        Client.ApplicationMessageReceivedAsync += HandleApplicationMessageReceivedAsync;
    }

    public long Handle { get; set; }

    public IMqttClient Client { get; }

    public string ClientId { get; private set; }

    public string LastError { get; private set; } = string.Empty;

    public void SetLastError(string message)
        => LastError = message;

    public void ClearLastError()
        => LastError = string.Empty;

    public async Task<MqttReceivedMessage?> ReceiveAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (_messages.TryDequeue(out var message))
        {
            return message;
        }

        if (Volatile.Read(ref _disposed) != 0)
        {
            return null;
        }

        try
        {
            var signaled = true;
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                signaled = await _signal.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            }

            if (!signaled)
            {
                return null;
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }

        return _messages.TryDequeue(out message) ? message : null;
    }

    public async Task CloseAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Client.ApplicationMessageReceivedAsync -= HandleApplicationMessageReceivedAsync;
        try
        {
            if (Client.IsConnected)
            {
                await Client.DisconnectAsync(
                    new MqttClientDisconnectOptionsBuilder().WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection).Build(),
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch
        {
            // Disconnect is best-effort during cleanup.
        }
        finally
        {
            Client.Dispose();
            _signal.Dispose();
        }
    }

    private Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return Task.CompletedTask;
        }

        var message = args.ApplicationMessage;
        var payloadBytes = message.Payload.IsEmpty ? Array.Empty<byte>() : message.Payload.ToArray();

        _messages.Enqueue(new MqttReceivedMessage(
            args.ClientId,
            args.PacketIdentifier,
            message.Topic ?? string.Empty,
            payloadBytes,
            message.QualityOfServiceLevel,
            message.Retain,
            message.Dup));

        try
        {
            _signal.Release();
        }
        catch (ObjectDisposedException)
        {
            // Ignore race with shutdown.
        }

        return Task.CompletedTask;
    }
}

internal sealed record MqttReceivedMessage(
    string ClientId,
    ushort PacketIdentifier,
    string Topic,
    byte[] PayloadBytes,
    MqttQualityOfServiceLevel QualityOfService,
    bool Retain,
    bool Duplicate)
{
    public string PayloadText => PayloadBytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(PayloadBytes);
}
