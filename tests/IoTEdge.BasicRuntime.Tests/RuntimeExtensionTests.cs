#if EDGE_BASIC_RUNTIME_EXTENSIONS
using IoTEdge.Domain;
#endif

namespace IoTEdge.BasicRuntime.Tests;

public sealed class RuntimeExtensionTests
{
    [Fact]
    public void Runtime_can_register_external_extensions()
    {
        var runtime = new BasicRuntime();
        runtime.Use(new EchoExtension());

        var result = runtime.Execute("""
            return EXT_ECHO("codex")
            """);

        Assert.Equal("echo:codex", result.ReturnValue);
        Assert.Contains("echo", runtime.RegisteredExtensions);
    }

    [Fact]
    public void Native_functions_can_return_dictionary_values()
    {
        var runtime = new BasicRuntime();
        runtime.RegisterFunction("RETURN_DICTIONARY", (_, _) => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["quality"] = "Good",
            ["value"] = 42,
            ["nested"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["inner"] = "yes"
            }
        });

        var result = runtime.Execute("""
            payload = RETURN_DICTIONARY()
            if payload("quality") <> "Good" then
              return "bad quality"
            endif

            if payload("value") <> 42 then
              return "bad value"
            endif

            return payload("nested")("inner")
            """);

        Assert.Equal("yes", result.ReturnValue);
    }

    [Fact]
    public void Runtime_can_return_dictionary_values_to_the_host()
    {
        var runtime = new BasicRuntime();
        var result = runtime.Execute("""
            payload = dict()
            payload("quality") = "Good"
            payload("value") = 42
            nested = dict()
            nested("inner") = "yes"
            payload("nested") = nested
            return payload
            """);

        var payload = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(result.ReturnValue);
        Assert.Equal("Good", payload["quality"]);
        Assert.Equal(42L, payload["value"]);
        var nested = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(payload["nested"]);
        Assert.Equal("yes", nested["inner"]);
    }

#if EDGE_BASIC_RUNTIME_EXTENSIONS
    [Fact]
    public void Gateway_extension_can_bridge_driver_reads_and_uploads()
    {
        var driverRegistry = new LoopbackDriverRegistry();
        var uploadRegistry = new LoopbackUploadTransportRegistry();
        var runtime = new BasicRuntime();
        runtime.Use(new IoTEdge.RuntimeExtensions.GatewayRuntimeExtension(driverRegistry, uploadRegistry));

        var result = runtime.Execute("""
            catalog = EDGE_DRIVER_CATALOG()
            if LEN(catalog) = 0 then
              return "empty catalog"
            endif

            read = EDGE_DRIVER_READ("demo", dict(), "sensor-1", "double", 1, dict())
            if read("success") = 0 then
              return read("errorMessage")
            endif

            transform = dict()
            transform("kind") = "Scale"
            transform("sortOrder") = 0
            transform("enabled") = 1
            transformArgs = dict()
            transformArgs("factor") = 2
            transform("arguments") = transformArgs

            transformed = EDGE_TRANSFORM_APPLY(read("rawValue"), list(transform))
            if transformed("success") = 0 then
              return transformed("errorMessage")
            endif

            envelope = dict()
            envelope("deviceName") = "device-1"
            envelope("pointName") = "temperature"
            envelope("rawValue") = read("rawValue")
            envelope("value") = transformed("value")
            envelope("quality") = read("quality")
            envelope("target") = "telemetry"
            envelope("payloadTemplate") = "{{device}}/{{point}}={{value}}"

            upload = EDGE_UPLOAD("http", "http://127.0.0.1/api/upload", dict(), envelope)
            if upload("success") = 0 then
              return upload("errorMessage")
            endif

            return upload("status")
            """);

        Assert.Equal("Succeeded", result.ReturnValue);
        Assert.Single(driverRegistry.Drivers);
        Assert.Single(uploadRegistry.Uploads);
        Assert.Equal("device-1", uploadRegistry.Uploads[0].Envelope.DeviceName);
        Assert.Equal("temperature", uploadRegistry.Uploads[0].Envelope.PointName);
        Assert.Equal("sensor-1", driverRegistry.Drivers[0].LastReadAddress);
        Assert.Equal(25L, Assert.IsType<long>(uploadRegistry.Uploads[0].Envelope.Value));
    }

    private sealed class LoopbackDriverRegistry : IDeviceDriverRegistry
    {
        public List<LoopbackDriver> Drivers { get; } = [];

        public IReadOnlyCollection<DriverMetadata> GetMetadata() => [LoopbackDriver.Definition];

        public IDeviceDriver GetRequiredDriver(string code)
        {
            if (!string.Equals(code, "demo", StringComparison.OrdinalIgnoreCase))
            {
                throw new KeyNotFoundException($"Driver '{code}' is not registered.");
            }

            var driver = new LoopbackDriver();
            Drivers.Add(driver);
            return driver;
        }
    }

    private sealed class LoopbackDriver : IDeviceDriver
    {
        public static DriverMetadata Definition { get; } = new(
            "demo",
            DriverType.Modbus,
            "Demo",
            "Loopback driver for runtime extension tests.",
            true,
            true,
            false,
            false,
            Array.Empty<ConnectionSettingDefinition>());

        public string LastReadAddress { get; private set; } = string.Empty;

        public DriverMetadata Metadata => Definition;

        public Task<ConnectionTestResult> TestConnectionAsync(DriverConnectionContext context, CancellationToken cancellationToken)
            => Task.FromResult(new ConnectionTestResult(true));

        public Task<AddressValidationResult> ValidateAddressAsync(DriverReadRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new AddressValidationResult(true));

        public Task<DriverReadResult> ReadAsync(DriverConnectionContext context, DriverReadRequest request, CancellationToken cancellationToken)
        {
            LastReadAddress = request.Address;
            return Task.FromResult(new DriverReadResult(request.Address, 12.5d, 12.5d, DateTimeOffset.UtcNow, QualityStatus.Good));
        }

        public Task<IReadOnlyCollection<DriverReadResult>> ReadBatchAsync(DriverConnectionContext context, DriverBatchReadRequest request, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<DriverReadResult>>(request.Requests.Select(item => new DriverReadResult(item.Address, 12.5d, 12.5d, DateTimeOffset.UtcNow, QualityStatus.Good)).ToArray());

        public Task<DriverWriteResult> WriteAsync(DriverConnectionContext context, DriverWriteRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new DriverWriteResult(request.Address, request.Value, DateTimeOffset.UtcNow, QualityStatus.Good));

        public Task<IReadOnlyCollection<DriverWriteResult>> WriteBatchAsync(DriverConnectionContext context, DriverBatchWriteRequest request, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<DriverWriteResult>>(request.Requests.Select(item => new DriverWriteResult(item.Address, item.Value, DateTimeOffset.UtcNow, QualityStatus.Good)).ToArray());
    }

    private sealed class LoopbackUploadTransportRegistry : IUploadTransportRegistry
    {
        public List<LoopbackUploadTransportRecord> Uploads { get; } = [];
        private readonly LoopbackUploadTransport _transport;

        public LoopbackUploadTransportRegistry()
        {
            _transport = new LoopbackUploadTransport(Uploads);
        }

        public IUploadTransport GetRequiredTransport(UploadProtocol protocol)
            => _transport;
    }

    private sealed class LoopbackUploadTransport : IUploadTransport
    {
        private readonly List<LoopbackUploadTransportRecord> _uploads;

        public LoopbackUploadTransport(List<LoopbackUploadTransportRecord> uploads)
        {
            _uploads = uploads;
        }

        public UploadProtocol Protocol => UploadProtocol.Http;

        public Task UploadAsync(UploadChannel channel, UploadEnvelope envelope, CancellationToken cancellationToken)
        {
            _uploads.Add(new LoopbackUploadTransportRecord(channel, envelope));
            return Task.CompletedTask;
        }
    }

    private sealed record LoopbackUploadTransportRecord(UploadChannel Channel, UploadEnvelope Envelope);
#endif

    private sealed class EchoExtension : IBasicRuntimeExtension
    {
        public string Name => "echo";

        public void Register(BasicRuntime runtime)
            => runtime.RegisterFunction("EXT_ECHO", (_, arguments) => $"echo:{arguments[0]}");
    }
}
