using System.Text;
using IoTClient.Common.Enums;
using IoTClient.Enums;

namespace IoTSharp.Edge.BasicRuntime.Tests;

public sealed class PlcBuiltInFunctionTests
{
    [Fact]
    public void Runtime_can_use_siemens_and_mitsubishi_plc_functions()
    {
        var factory = new LoopbackPlcClientFactory();
        factory.SiemensSession.ConfigureRead("DB1.DBW0", DataTypeEnum.Int16, 0, (short)123);
        factory.SiemensSession.ConfigureRead("DB1.DBX0.0", DataTypeEnum.String, 5, "hello");
        factory.SiemensSession.ConfigureRead(
            "DB1.DBW2",
            DataTypeEnum.Int16,
            2,
            new Dictionary<string, object?>
            {
                ["DB1.DBW2"] = (short)10,
                ["DB1.DBW4"] = (short)20
            });
        factory.MitsubishiSession.ConfigureRead(
            "M0",
            DataTypeEnum.Bool,
            2,
            new Dictionary<string, object?>
            {
                ["M0"] = true,
                ["M1"] = false
            });
        factory.MitsubishiSession.ConfigureRead("D10", DataTypeEnum.Double, 0, 1.5d);

        var runtime = new BasicRuntime(plcClientFactory: factory);
        var result = runtime.Execute("""
            siemens = SIEMENS_CONNECT("127.0.0.1", 102, "S7_1200", 1, 2, 250)
            if siemens = 0 then
              return "siemens connect failed: " + SIEMENS_LAST_ERROR()
            endif

            if SIEMENS_CONNECTED(siemens) = 0 then
              return "siemens not connected"
            endif

            if SIEMENS_VERSION(siemens) <> "S7_1200" then
              return "wrong siemens version: " + SIEMENS_VERSION(siemens)
            endif

            if SIEMENS_WRITE_INT16(siemens, "DB1.DBW0", 321) = 0 then
              return "siemens write failed: " + SIEMENS_LAST_ERROR(siemens)
            endif

            if SIEMENS_READ_INT16(siemens, "DB1.DBW0") <> 123 then
              return "wrong siemens int16"
            endif

            if SIEMENS_READ_STRING(siemens, "DB1.DBX0.0", 5) <> "hello" then
              return "wrong siemens string"
            endif

            multi = SIEMENS_READ_INT16(siemens, "DB1.DBW2", 2)
            if multi("DB1.DBW2") <> 10 or multi("DB1.DBW4") <> 20 then
              return "wrong siemens multi-read"
            endif

            if SIEMENS_CLOSE(siemens) = 0 then
              return "siemens close failed: " + SIEMENS_LAST_ERROR(siemens)
            endif

            mitsubishi = MITSUBISHI_CONNECT("127.0.0.1", 6000, "Qna_3E", 2000)
            if mitsubishi = 0 then
              return "mitsubishi connect failed: " + MITSUBISHI_LAST_ERROR()
            endif

            if MITSUBISHI_WRITE_DOUBLE(mitsubishi, "D10", 2.25) = 0 then
              return "mitsubishi write failed: " + MITSUBISHI_LAST_ERROR(mitsubishi)
            endif

            if MITSUBISHI_READ_DOUBLE(mitsubishi, "D10") <> 1.5 then
              return "wrong mitsubishi double"
            endif

            bools = MITSUBISHI_READ_BOOL(mitsubishi, "M0", 2)
            if bools("M0") = 0 or bools("M1") <> 0 then
              return "wrong mitsubishi multi-read"
            endif

            if MITSUBISHI_CLOSE(mitsubishi) = 0 then
              return "mitsubishi close failed: " + MITSUBISHI_LAST_ERROR(mitsubishi)
            endif

            return "ok"
            """);

        Assert.Equal("ok", result.ReturnValue);
        Assert.Single(factory.SiemensOpenedOptions);
        Assert.Equal("127.0.0.1", factory.SiemensOpenedOptions[0].Host);
        Assert.Equal(102, factory.SiemensOpenedOptions[0].Port);
        Assert.Equal(SiemensVersion.S7_1200, factory.SiemensOpenedOptions[0].Version);
        Assert.Equal(1, factory.SiemensOpenedOptions[0].Rack);
        Assert.Equal(2, factory.SiemensOpenedOptions[0].Slot);
        Assert.Equal(250, factory.SiemensOpenedOptions[0].TimeoutMs);

        Assert.Single(factory.MitsubishiOpenedOptions);
        Assert.Equal("127.0.0.1", factory.MitsubishiOpenedOptions[0].Host);
        Assert.Equal(6000, factory.MitsubishiOpenedOptions[0].Port);
        Assert.Equal(MitsubishiVersion.Qna_3E, factory.MitsubishiOpenedOptions[0].Version);
        Assert.Equal(2000, factory.MitsubishiOpenedOptions[0].TimeoutMs);

        Assert.Contains(factory.SiemensSession.WriteRequests, request =>
            request.Address == "DB1.DBW0"
            && request.DataType == DataTypeEnum.Int16
            && request.Value is short shortValue
            && shortValue == 321);
    }

    [Fact]
    public void Runtime_can_use_batch_read_write_and_send_package()
    {
        var factory = new LoopbackPlcClientFactory();
        factory.OmronSession.BatchReadResponse = new Dictionary<string, object?>
        {
            ["D100"] = 42,
            ["D200"] = 12.5d
        };
        factory.OmronSession.SendPackageResponse = Encoding.UTF8.GetBytes("pong");
        factory.AllenBradleySession.ConfigureRead("Tag1", DataTypeEnum.String, 0, "hello");

        var runtime = new BasicRuntime(plcClientFactory: factory);
        var result = runtime.Execute("""
            omron = OMRON_FINS_CONNECT("10.0.0.1", 9600, 1500, "DCBA")
            if omron = 0 then
              return "omron connect failed: " + OMRON_FINS_LAST_ERROR()
            endif

            readMap = dict()
            readMap("D100") = "int16"
            readMap("D200") = "double"
            values = OMRON_FINS_BATCH_READ(omron, readMap, 7)
            if values("D100") <> 42 or values("D200") <> 12.5 then
              return "wrong omron batch read"
            endif

            writeMap = dict()
            writeMap("D100") = 99
            writeMap("D200") = 1.25
            if OMRON_FINS_BATCH_WRITE(omron, writeMap, 7) = 0 then
              return "omron batch write failed: " + OMRON_FINS_LAST_ERROR(omron)
            endif

            raw = OMRON_FINS_SEND_PACKAGE(omron, "PING")
            if LEN(raw) <> 4 or raw(0) <> 112 then
              return "wrong omron raw payload"
            endif

            if OMRON_FINS_CLOSE(omron) = 0 then
              return "omron close failed: " + OMRON_FINS_LAST_ERROR(omron)
            endif

            ab = ALLEN_BRADLEY_CONNECT("10.0.0.2", 44818, 3, 1800)
            if ab = 0 then
              return "ab connect failed: " + ALLEN_BRADLEY_LAST_ERROR()
            endif

            if ALLEN_BRADLEY_WRITE_STRING(ab, "Tag1", "hello") = 0 then
              return "ab write failed: " + ALLEN_BRADLEY_LAST_ERROR(ab)
            endif

            if ALLEN_BRADLEY_READ_STRING(ab, "Tag1") <> "hello" then
              return "wrong ab string"
            endif

            if ALLEN_BRADLEY_CLOSE(ab) = 0 then
              return "ab close failed: " + ALLEN_BRADLEY_LAST_ERROR(ab)
            endif

            return "ok"
            """);

        Assert.Equal("ok", result.ReturnValue);
        Assert.Single(factory.OmronOpenedOptions);
        Assert.Equal("10.0.0.1", factory.OmronOpenedOptions[0].Host);
        Assert.Equal(9600, factory.OmronOpenedOptions[0].Port);
        Assert.Equal(1500, factory.OmronOpenedOptions[0].TimeoutMs);
        Assert.Equal(EndianFormat.DCBA, factory.OmronOpenedOptions[0].EndianFormat);

        Assert.Single(factory.OmronSession.BatchReadRequests);
        Assert.Equal(DataTypeEnum.Int16, factory.OmronSession.BatchReadRequests[0].Addresses["D100"]);
        Assert.Equal(DataTypeEnum.Double, factory.OmronSession.BatchReadRequests[0].Addresses["D200"]);
        Assert.Equal(7, factory.OmronSession.BatchReadRequests[0].BatchNumber);

        Assert.Single(factory.OmronSession.BatchWriteRequests);
        Assert.Equal(7, factory.OmronSession.BatchWriteRequests[0].BatchNumber);
        Assert.Equal(99L, Convert.ToInt64(factory.OmronSession.BatchWriteRequests[0].Values["D100"]));
        Assert.Equal(1.25d, Convert.ToDouble(factory.OmronSession.BatchWriteRequests[0].Values["D200"]));

        Assert.Single(factory.OmronSession.SendPackageRequests);
        Assert.Equal("PING", Encoding.UTF8.GetString(factory.OmronSession.SendPackageRequests[0]));

        Assert.Single(factory.AllenBradleyOpenedOptions);
        Assert.Equal("10.0.0.2", factory.AllenBradleyOpenedOptions[0].Host);
        Assert.Equal(44818, factory.AllenBradleyOpenedOptions[0].Port);
        Assert.Equal(3, factory.AllenBradleyOpenedOptions[0].Slot);
        Assert.Equal(1800, factory.AllenBradleyOpenedOptions[0].TimeoutMs);

        Assert.Contains(factory.AllenBradleySession.WriteRequests, request =>
            request.Address == "Tag1"
            && request.DataType == DataTypeEnum.String
            && request.Value is string text
            && text == "hello");
    }

    [Fact]
    public void Runtime_exposes_last_plc_error_for_invalid_handle()
    {
        var runtime = new BasicRuntime(plcClientFactory: new LoopbackPlcClientFactory());
        var result = runtime.Execute("""
            if SIEMENS_CONNECTED(999) = 0 then
              return SIEMENS_LAST_ERROR()
            endif

            return "unexpected"
            """);

        Assert.Equal("未找到西门子句柄。", result.ReturnValue);
    }

    private sealed class LoopbackPlcClientFactory : IBasicPlcClientFactory
    {
        public List<BasicSiemensConnectionOptions> SiemensOpenedOptions { get; } = [];

        public List<BasicMitsubishiConnectionOptions> MitsubishiOpenedOptions { get; } = [];

        public List<BasicOmronFinsConnectionOptions> OmronOpenedOptions { get; } = [];

        public List<BasicAllenBradleyConnectionOptions> AllenBradleyOpenedOptions { get; } = [];

        public LoopbackPlcClientSession SiemensSession { get; } = new("Siemens");

        public LoopbackPlcClientSession MitsubishiSession { get; } = new("Mitsubishi");

        public LoopbackPlcClientSession OmronSession { get; } = new("Omron FINS");

        public LoopbackPlcClientSession AllenBradleySession { get; } = new("Allen-Bradley");

        public IBasicPlcClientSession OpenSiemens(BasicSiemensConnectionOptions options)
        {
            SiemensOpenedOptions.Add(options);
            SiemensSession.SetVersion(options.Version.ToString());
            return SiemensSession;
        }

        public IBasicPlcClientSession OpenMitsubishi(BasicMitsubishiConnectionOptions options)
        {
            MitsubishiOpenedOptions.Add(options);
            MitsubishiSession.SetVersion(options.Version.ToString());
            return MitsubishiSession;
        }

        public IBasicPlcClientSession OpenOmronFins(BasicOmronFinsConnectionOptions options)
        {
            OmronOpenedOptions.Add(options);
            OmronSession.SetVersion(options.EndianFormat.ToString());
            return OmronSession;
        }

        public IBasicPlcClientSession OpenAllenBradley(BasicAllenBradleyConnectionOptions options)
        {
            AllenBradleyOpenedOptions.Add(options);
            AllenBradleySession.SetVersion($"slot-{options.Slot}");
            return AllenBradleySession;
        }
    }

    private sealed class LoopbackPlcClientSession : IBasicPlcClientSession
    {
        private int _disposed;

        public LoopbackPlcClientSession(string version)
        {
            Version = version;
        }

        public string Version { get; private set; }

        public bool IsConnected { get; private set; }

        public string LastError { get; private set; } = string.Empty;

        public List<BasicPlcReadRequest> ReadRequests { get; } = [];

        public List<BasicPlcWriteRequest> WriteRequests { get; } = [];

        public List<BasicPlcBatchReadRequest> BatchReadRequests { get; } = [];

        public List<BasicPlcBatchWriteRequest> BatchWriteRequests { get; } = [];

        public List<byte[]> SendPackageRequests { get; } = [];

        public Dictionary<(string Address, DataTypeEnum DataType, int Count), object?> ReadResponses { get; } = [];

        public Dictionary<string, object?>? BatchReadResponse { get; set; }

        public byte[]? SendPackageResponse { get; set; }

        public void ConfigureRead(string address, DataTypeEnum dataType, int count, object? value)
            => ReadResponses[(address, dataType, count)] = value;

        public void SetVersion(string version)
            => Version = version;

        public BasicPlcOperationResult Open()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return FailOperation("Session is closed.");
            }

            IsConnected = true;
            ClearLastError();
            return new BasicPlcOperationResult(true, null);
        }

        public BasicPlcOperationResult Close()
        {
            IsConnected = false;
            ClearLastError();
            return new BasicPlcOperationResult(true, null);
        }

        public BasicPlcReadResult Read(BasicPlcReadRequest request)
        {
            EnsureOpen();
            ReadRequests.Add(request);

            if (ReadResponses.TryGetValue((request.Address, request.DataType, request.Count), out var value)
                || ReadResponses.TryGetValue((request.Address, request.DataType, 0), out value))
            {
                return SuccessRead(value);
            }

            return SuccessRead(DefaultValue(request.DataType));
        }

        public BasicPlcWriteResult Write(BasicPlcWriteRequest request)
        {
            EnsureOpen();
            WriteRequests.Add(request);
            ClearLastError();
            return new BasicPlcWriteResult(true, null);
        }

        public BasicPlcReadResult BatchRead(BasicPlcBatchReadRequest request)
        {
            EnsureOpen();
            BatchReadRequests.Add(request);

            if (BatchReadResponse is not null)
            {
                return SuccessRead(BatchReadResponse);
            }

            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in request.Addresses)
            {
                values[pair.Key] = DefaultValue(pair.Value);
            }

            return SuccessRead(values);
        }

        public BasicPlcWriteResult BatchWrite(BasicPlcBatchWriteRequest request)
        {
            EnsureOpen();
            BatchWriteRequests.Add(request);
            ClearLastError();
            return new BasicPlcWriteResult(true, null);
        }

        public BasicPlcReadResult SendPackage(byte[] command)
        {
            EnsureOpen();
            SendPackageRequests.Add(command.ToArray());
            return SuccessRead(SendPackageResponse ?? command);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _disposed, 1);
            IsConnected = false;
        }

        private BasicPlcReadResult SuccessRead(object? value)
        {
            ClearLastError();
            return new BasicPlcReadResult(true, value, null);
        }

        private BasicPlcOperationResult FailOperation(string message)
        {
            LastError = message;
            return new BasicPlcOperationResult(false, message);
        }

        private void EnsureOpen()
        {
            if (Volatile.Read(ref _disposed) != 0 || !IsConnected)
            {
                throw new InvalidOperationException("Loopback PLC session is closed.");
            }
        }

        private void ClearLastError()
            => LastError = string.Empty;

        private static object? DefaultValue(DataTypeEnum dataType)
            => dataType switch
            {
                DataTypeEnum.Bool => false,
                DataTypeEnum.Byte => (byte)0,
                DataTypeEnum.Int16 => (short)0,
                DataTypeEnum.UInt16 => (ushort)0,
                DataTypeEnum.Int32 => 0,
                DataTypeEnum.UInt32 => 0u,
                DataTypeEnum.Int64 => 0L,
                DataTypeEnum.UInt64 => 0UL,
                DataTypeEnum.Float => 0f,
                DataTypeEnum.Double => 0d,
                DataTypeEnum.String => string.Empty,
                _ => null
            };
    }
}
