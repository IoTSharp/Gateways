using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IoTSharp.Edge.Application;
using IoTSharp.Edge.Domain;
using IoTSharp.Edge.Infrastructure.Drivers;
using IoTSharp.Edge.Infrastructure.Persistence;
using IoTSharp.Edge.Infrastructure.Uploads;

namespace IoTSharp.Edge.Infrastructure;

public static class GatewayInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddGatewayInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GatewayStorageOptions>(configuration.GetSection("GatewayStorage"));
        services.AddHttpClient();
        services.AddSingleton<IGatewayDbConnectionFactory, SqliteGatewayConnectionFactory>();
        services.AddSingleton<IGatewaySchemaInitializer, SqliteGatewaySchemaInitializer>();
        services.AddScoped<IGatewayRepository, SqliteGatewayRepository>();

        services.AddSingleton<IDeviceDriver, ModbusDriver>();
        services.AddSingleton<IDeviceDriver, SiemensDriver>();
        services.AddSingleton<IDeviceDriver, MitsubishiDriver>();
        services.AddSingleton<IDeviceDriver, OmronFinsDriver>();
        services.AddSingleton<IDeviceDriver, AllenBradleyDriver>();
        services.AddSingleton<IDeviceDriver, OpcUaDriver>();
        services.AddSingleton<IDeviceDriver, MtConnectDriver>();
        services.AddSingleton<IDeviceDriver>(_ => new UnsupportedDriver(
            new DriverMetadata("bacnet", DriverType.Bacnet, "BACnet", "BACnet/IP collection contract for building automation devices and object properties.", true, true, true, true,
                new[]
                {
                    new ConnectionSettingDefinition("host", "Host", "text", true, "BACnet device host name or IP address."),
                    new ConnectionSettingDefinition("port", "Port", "number", true, "BACnet/IP UDP port, commonly 47808."),
                    new ConnectionSettingDefinition("deviceInstance", "Device Instance", "number", true, "BACnet device instance identifier."),
                    new ConnectionSettingDefinition("networkNumber", "Network Number", "number", false, "Optional BACnet network number."),
                    new ConnectionSettingDefinition("timeout", "Timeout", "number", false, "Timeout in milliseconds.")
                }, "planned"),
            "BACnet/IP collection is planned as a protocol adapter. Add a BACnet stack such as BACnet4J.NET or a native adapter before enabling runtime reads."));
        services.AddSingleton<IDeviceDriver>(_ => new UnsupportedDriver(
            new DriverMetadata("iec104", DriverType.Iec104, "IEC 60870-5-104", "IEC 60870-5-104 telecontrol collection contract for power and SCADA endpoints.", true, true, true, true,
                new[]
                {
                    new ConnectionSettingDefinition("host", "Host", "text", true, "IEC 104 server host name or IP address."),
                    new ConnectionSettingDefinition("port", "Port", "number", true, "IEC 104 TCP port, commonly 2404."),
                    new ConnectionSettingDefinition("commonAddress", "Common Address", "number", true, "ASDU common address."),
                    new ConnectionSettingDefinition("originatorAddress", "Originator Address", "number", false, "Optional originator address."),
                    new ConnectionSettingDefinition("timeout", "Timeout", "number", false, "Timeout in milliseconds.")
                }, "planned"),
            "IEC 60870-5-104 collection is planned as a protocol adapter. Add an IEC 104 stack before enabling runtime reads."));
        services.AddSingleton<IDeviceDriver>(_ => new UnsupportedDriver(
            new DriverMetadata("mqtt", DriverType.Mqtt, "MQTT", "MQTT subscription-based collection contract for topic payload ingestion.", true, false, true, false,
                new[]
                {
                    new ConnectionSettingDefinition("host", "Host", "text", true, "MQTT broker host name or IP address."),
                    new ConnectionSettingDefinition("port", "Port", "number", true, "MQTT broker port, commonly 1883."),
                    new ConnectionSettingDefinition("clientId", "Client ID", "text", true, "Client identifier used by the edge collector."),
                    new ConnectionSettingDefinition("topic", "Topic", "text", true, "Topic or topic filter to subscribe."),
                    new ConnectionSettingDefinition("qos", "QoS", "select", false, "MQTT quality of service.", new[] { "0", "1", "2" }),
                    new ConnectionSettingDefinition("username", "Username", "text", false, "Optional broker username."),
                    new ConnectionSettingDefinition("password", "Password", "password", false, "Optional broker password.")
                }, "planned"),
            "MQTT collection is planned as a subscription adapter. The current MQTT implementation is only registered as an upload transport."));
        services.AddSingleton<IDeviceDriver>(_ => new UnsupportedDriver(
            new DriverMetadata("opc-da", DriverType.OpcDa, "OPC DA", "Windows-only OPC DA Classic COM/DCOM driver contract.", true, true, true, true,
                new[]
                {
                    new ConnectionSettingDefinition("progId", "ProgId", "text", true, "OPC DA server ProgId, for example Matrikon.OPC.Simulation.1."),
                    new ConnectionSettingDefinition("host", "Host", "text", false, "Optional remote OPC DA host."),
                    new ConnectionSettingDefinition("clsid", "CLSID", "text", false, "Optional COM CLSID when ProgId is not enough.")
                }, "high"),
            "OPC DA depends on Windows COM/DCOM. Use a Windows-only adapter package such as TitaniumAS.Opc.Client or bridge OPC DA to OPC UA before enabling it in the cross-platform gateway."));
        services.AddSingleton<IDeviceDriver>(_ => new UnsupportedDriver(
            new DriverMetadata("fanuc-cnc", DriverType.FanucCnc, "Fanuc CNC", "Fanuc FOCAS native SDK driver contract.", true, true, true, true,
                new[]
                {
                    new ConnectionSettingDefinition("host", "Host", "text", true, "CNC IP address or host name."),
                    new ConnectionSettingDefinition("port", "Port", "number", false, "FOCAS TCP port, commonly 8193."),
                    new ConnectionSettingDefinition("timeout", "Timeout", "number", false, "FOCAS timeout in seconds."),
                    new ConnectionSettingDefinition("libraryPath", "FOCAS Library", "text", false, "Optional path to fwlib32/fwlib64 native library.")
                }, "high"),
            "Fanuc CNC support requires the licensed Fanuc FOCAS runtime (fwlib32/fwlib64) and architecture-specific native loading. Keep it as an optional adapter boundary."));
        services.AddSingleton<IDeviceDriverRegistry, DeviceDriverRegistry>();

        services.AddSingleton<IUploadTransport, HttpUploadTransport>();
        services.AddSingleton<IUploadTransport, IotSharpMqttUploadTransport>();
        services.AddSingleton<IUploadTransport, IotSharpDeviceHttpUploadTransport>();
        services.AddSingleton<IUploadTransport, SonnetDbUploadTransport>();
        services.AddSingleton<IUploadTransportRegistry, UploadTransportRegistry>();

        return services;
    }
}
