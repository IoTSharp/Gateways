using IoTClient.Clients.Modbus;
using IoTClient.Enums;
using IoTServer.Servers.Modbus;
using Microsoft.Extensions.Options;

namespace IoTSharp.Edge.DeviceSimulator;

public sealed class ModbusSimulatorOptions
{
    public int Port { get; set; } = 1502;

    public byte StationNumber { get; set; } = 1;

    public int TimeoutMs { get; set; } = 1500;

    public string EndianFormat { get; set; } = "ABCD";

    public bool PlcAddresses { get; set; } = true;
}

public sealed class ModbusSimulatorState
{
    private readonly object _lock = new();
    private float _temperature;
    private float _pressure;
    private short _motorRpm;
    private DateTime _updatedAtUtc;

    public void Update(float temperature, float pressure, short motorRpm)
    {
        lock (_lock)
        {
            _temperature = temperature;
            _pressure = pressure;
            _motorRpm = motorRpm;
            _updatedAtUtc = DateTime.UtcNow;
        }
    }

    public object GetSnapshot()
    {
        lock (_lock)
        {
            return new
            {
                updatedAtUtc = _updatedAtUtc,
                temperature = _temperature,
                pressure = _pressure,
                motorRpm = _motorRpm,
                addresses = new
                {
                    temperature = "40001",
                    pressure = "40003",
                    motorRpm = "40005"
                }
            };
        }
    }
}

public sealed class IoTServerModbusHost : BackgroundService
{
    private readonly IOptions<ModbusSimulatorOptions> _options;
    private readonly ILogger<IoTServerModbusHost> _logger;
    private ModbusTcpServer? _server;

    public IoTServerModbusHost(IOptions<ModbusSimulatorOptions> options, ILogger<IoTServerModbusHost> logger)
    {
        _options = options;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _server = new ModbusTcpServer(_options.Value.Port);
        _server.Start();
        _logger.LogInformation(
            "Started IoTServer.ModbusTcpServer on 0.0.0.0:{Port}.",
            _options.Value.Port);

        return Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _server?.Stop();
        _logger.LogInformation("Stopped IoTServer.ModbusTcpServer.");
        return base.StopAsync(cancellationToken);
    }
}

public sealed class ModbusValueWriter : BackgroundService
{
    private readonly IOptions<ModbusSimulatorOptions> _options;
    private readonly ModbusSimulatorState _state;
    private readonly ILogger<ModbusValueWriter> _logger;

    public ModbusValueWriter(
        IOptions<ModbusSimulatorOptions> options,
        ModbusSimulatorState state,
        ILogger<ModbusValueWriter> logger)
    {
        _options = options;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        var client = CreateClient(_options.Value);
        var open = client.Open();
        if (!open.IsSucceed)
        {
            throw new InvalidOperationException(open.Err ?? "Unable to open IoTServer Modbus loopback client.");
        }

        _logger.LogInformation("Opened IoTClient loopback writer for IoTServer Modbus values.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var seconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
                var temperature = 24.0f + (float)Math.Sin(seconds / 9d) * 3.5f;
                var pressure = 1.6f + (float)Math.Cos(seconds / 13d) * 0.25f;
                var motorRpm = (short)(1420 + Math.Sin(seconds / 7d) * 80);

                WriteRequired(client.Write("40001", temperature, _options.Value.StationNumber, 16), "temperature");
                WriteRequired(client.Write("40003", pressure, _options.Value.StationNumber, 16), "pressure");
                WriteRequired(client.Write("40005", motorRpm, _options.Value.StationNumber, 16), "motor rpm");
                _state.Update(temperature, pressure, motorRpm);

                _logger.LogInformation(
                    "Wrote IoTServer Modbus values: temperature={Temperature:F2}, pressure={Pressure:F2}, motorRpm={MotorRpm}.",
                    temperature,
                    pressure,
                    motorRpm);

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
        finally
        {
            client.Close();
        }
    }

    private static ModbusTcpClient CreateClient(ModbusSimulatorOptions options)
        => new(
            "127.0.0.1",
            options.Port,
            options.TimeoutMs,
            ParseEndianFormat(options.EndianFormat),
            options.PlcAddresses);

    private static EndianFormat ParseEndianFormat(string value)
        => Enum.TryParse<EndianFormat>(value, true, out var parsed) ? parsed : EndianFormat.ABCD;

    private static void WriteRequired(global::IoTClient.Result result, string name)
    {
        if (!result.IsSucceed)
        {
            throw new InvalidOperationException(result.Err ?? $"Unable to write {name} to IoTServer Modbus simulator.");
        }
    }
}
