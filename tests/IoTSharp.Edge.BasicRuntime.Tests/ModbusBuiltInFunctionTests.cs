using IoTClient.Enums;
using System.IO.Ports;

namespace IoTSharp.Edge.BasicRuntime.Tests;

public sealed class ModbusBuiltInFunctionTests
{
    [Fact]
    public void Runtime_can_connect_read_write_and_close_modbus_sessions()
    {
        var factory = new LoopbackModbusClientFactory();
        var runtime = new BasicRuntime(modbusClientFactory: factory);
        var result = runtime.Execute("""
            tcp = MODBUS_CONNECT_TCP("127.0.0.1", 502, 250, "CDAB", 1)
            if tcp = 0 then
              return "tcp open failed: " + MODBUS_LAST_ERROR()
            endif

            rtu = MODBUS_CONNECT_RTU("COM1", 19200, 7, "E", 2, 500, "DCBA", 0)
            if rtu = 0 then
              return "rtu open failed: " + MODBUS_LAST_ERROR()
            endif

            if MODBUS_WRITE_BOOL(tcp, "00001", 1, 7, 5) = 0 then
              return "bool write failed: " + MODBUS_LAST_ERROR(tcp)
            endif

            if MODBUS_READ_COIL(tcp, "00001", 7, 1) = 0 then
              return "wrong coil value"
            endif

            if MODBUS_WRITE_INT16(tcp, "40001", 1234, 7, 16) = 0 then
              return "int write failed: " + MODBUS_LAST_ERROR(tcp)
            endif

            if MODBUS_READ_INT16(tcp, "40001", 7, 3) <> 1234 then
              return "wrong int16"
            endif

            if MODBUS_WRITE_STRING(tcp, "40010", "hello", 7, 16, "utf-8") = 0 then
              return "string write failed: " + MODBUS_LAST_ERROR(tcp)
            endif

            if MODBUS_READ_STRING(tcp, "40010", 7, 3, 5, "utf-8") <> "hello" then
              return "wrong string"
            endif

            if MODBUS_WRITE_RAW(tcp, "40020", list(1, 2, 3, 4), 7, 16) = 0 then
              return "raw write failed: " + MODBUS_LAST_ERROR(tcp)
            endif

            raw = MODBUS_READ_RAW(tcp, "40020", 7, 3, 4)
            if LEN(raw) <> 4 then
              return "wrong raw length: " + STR(LEN(raw))
            endif

            if raw(0) <> 1 or raw(3) <> 4 then
              return "wrong raw bytes"
            endif

            if MODBUS_CLOSE(rtu) = 0 then
              return "rtu close failed: " + MODBUS_LAST_ERROR(rtu)
            endif

            if MODBUS_CLOSE(tcp) = 0 then
              return "tcp close failed: " + MODBUS_LAST_ERROR(tcp)
            endif

            return "ok"
            """);

        Assert.Equal("ok", result.ReturnValue);
        Assert.Equal(2, factory.OpenedOptions.Count);

        Assert.Equal(BasicModbusTransport.Tcp, factory.OpenedOptions[0].Transport);
        Assert.Equal("127.0.0.1", factory.OpenedOptions[0].Host);
        Assert.Equal(502, factory.OpenedOptions[0].Port);
        Assert.Equal(250, factory.OpenedOptions[0].TimeoutMs);
        Assert.Equal(EndianFormat.CDAB, factory.OpenedOptions[0].EndianFormat);
        Assert.True(factory.OpenedOptions[0].PlcAddresses);

        Assert.Equal(BasicModbusTransport.Rtu, factory.OpenedOptions[1].Transport);
        Assert.Equal("COM1", factory.OpenedOptions[1].PortName);
        Assert.Equal(19_200, factory.OpenedOptions[1].BaudRate);
        Assert.Equal(7, factory.OpenedOptions[1].DataBits);
        Assert.Equal(Parity.Even, factory.OpenedOptions[1].Parity);
        Assert.Equal(StopBits.Two, factory.OpenedOptions[1].StopBits);
        Assert.Equal(500, factory.OpenedOptions[1].TimeoutMs);
        Assert.Equal(EndianFormat.DCBA, factory.OpenedOptions[1].EndianFormat);
        Assert.False(factory.OpenedOptions[1].PlcAddresses);

        Assert.Equal(2, factory.Sessions.Count);
        var session = factory.Sessions[0];
        Assert.Contains(session.WriteRequests, request => request.ValueKind == BasicModbusValueKind.Bool && request.Address == "00001" && request.FunctionCode == 5 && request.StationNumber == 7 && request.Value is bool boolValue && boolValue);
        Assert.Contains(session.WriteRequests, request => request.ValueKind == BasicModbusValueKind.Int16 && request.Address == "40001" && request.FunctionCode == 16 && request.Value is short shortValue && shortValue == 1234);
        Assert.Contains(session.WriteRequests, request => request.ValueKind == BasicModbusValueKind.String && request.Address == "40010" && request.FunctionCode == 16 && request.Value is string text && text == "hello");
        Assert.Contains(session.WriteRequests, request => request.ValueKind == BasicModbusValueKind.Raw && request.Address == "40020" && request.FunctionCode == 16 && request.Value is byte[] bytes && bytes.SequenceEqual(new byte[] { 1, 2, 3, 4 }));
    }

    [Fact]
    public void Runtime_exposes_last_modbus_error()
    {
        var runtime = new BasicRuntime(modbusClientFactory: new LoopbackModbusClientFactory());
        var result = runtime.Execute("""
            if MODBUS_READ_INT16(999, "40001", 1, 3) = nil then
              return MODBUS_LAST_ERROR()
            endif

            return "unexpected"
            """);

        Assert.Equal("未找到 Modbus 句柄。", result.ReturnValue);
    }

    private sealed class LoopbackModbusClientFactory : IBasicModbusClientFactory
    {
        public List<BasicModbusConnectionOptions> OpenedOptions { get; } = [];

        public List<LoopbackModbusClientSession> Sessions { get; } = [];

        public IBasicModbusClientSession Open(BasicModbusConnectionOptions options)
        {
            OpenedOptions.Add(options);
            var session = new LoopbackModbusClientSession(options);
            Sessions.Add(session);
            return session;
        }
    }

    private sealed class LoopbackModbusClientSession : IBasicModbusClientSession
    {
        private readonly Dictionary<(string Address, BasicModbusValueKind Kind), object?> _values = new();
        private int _disposed;

        public LoopbackModbusClientSession(BasicModbusConnectionOptions options)
        {
            Options = options;
        }

        public BasicModbusConnectionOptions Options { get; }

        public bool IsConnected => Volatile.Read(ref _disposed) == 0;

        public string LastError { get; private set; } = string.Empty;

        public List<BasicModbusReadRequest> ReadRequests { get; } = [];

        public List<BasicModbusWriteRequest> WriteRequests { get; } = [];

        public BasicModbusReadResult Read(BasicModbusReadRequest request)
        {
            EnsureOpen();
            ReadRequests.Add(request);

            if (_values.TryGetValue((request.Address, request.ValueKind), out var stored))
            {
                return new BasicModbusReadResult(true, CloneValue(stored), null);
            }

            return new BasicModbusReadResult(true, DefaultValue(request.ValueKind), null);
        }

        public BasicModbusWriteResult Write(BasicModbusWriteRequest request)
        {
            EnsureOpen();
            WriteRequests.Add(request);
            _values[(request.Address, request.ValueKind)] = CloneValue(request.Value);
            return new BasicModbusWriteResult(true, null);
        }

        public void Close()
        {
            Interlocked.Exchange(ref _disposed, 1);
        }

        public void Dispose()
            => Close();

        private static object? CloneValue(object? value)
            => value switch
            {
                byte[] bytes => bytes.ToArray(),
                _ => value
            };

        private static object? DefaultValue(BasicModbusValueKind kind)
            => kind switch
            {
                BasicModbusValueKind.Bool => false,
                BasicModbusValueKind.String => string.Empty,
                BasicModbusValueKind.Raw => Array.Empty<byte>(),
                _ => 0
            };

        private void EnsureOpen()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Loopback Modbus client is closed.");
            }
        }
    }
}
