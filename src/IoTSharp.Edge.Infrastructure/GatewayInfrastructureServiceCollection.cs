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
        services.AddSingleton<IUploadTransportRegistry, UploadTransportRegistry>();

        return services;
    }
}
