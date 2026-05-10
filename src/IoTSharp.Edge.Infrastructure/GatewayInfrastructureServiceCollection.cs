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
        services.AddSingleton<IDeviceDriver>(_ => new UnsupportedDriver(
            new DriverMetadata(
                "opc-ua",
                DriverType.OpcUa,
                "OPC UA",
                "Reserved contract for OPC UA integration behind the unified driver interface.",
                true,
                true,
                true,
                true,
                new[]
                {
                    new ConnectionSettingDefinition("endpoint", "Endpoint", "text", true, "OPC UA endpoint URL."),
                    new ConnectionSettingDefinition("username", "Username", "text", false, "Username for authenticated sessions."),
                    new ConnectionSettingDefinition("password", "Password", "password", false, "Password for authenticated sessions.")
                }),
            "OPC UA implementation is intentionally isolated and will be added as a dedicated adapter in a later increment."));
        services.AddSingleton<IDeviceDriver>(_ => new UnsupportedDriver(
            new DriverMetadata("opc-da", DriverType.OpcDa, "OPC DA", "Windows-only OPC DA driver contract.", true, true, true, true,
                new[] { new ConnectionSettingDefinition("progId", "ProgId", "text", true, "OPC DA ProgId.") }, "high"),
            "OPC DA depends on Windows COM and is intentionally isolated from the cross-platform AOT path."));
        services.AddSingleton<IDeviceDriver>(_ => new UnsupportedDriver(
            new DriverMetadata("mt-cnc", DriverType.MtCnc, "MT CNC", "Reserved MT machine driver contract.", true, true, true, true,
                new[] { new ConnectionSettingDefinition("host", "Host", "text", true, "Machine host or endpoint.") }, "high"),
            "MT CNC support requires vendor-specific protocol validation and is not enabled in this scaffold."));
        services.AddSingleton<IDeviceDriver>(_ => new UnsupportedDriver(
            new DriverMetadata("fanuc-cnc", DriverType.FanucCnc, "Fanuc CNC", "Reserved Fanuc CNC driver contract.", true, true, true, true,
                new[] { new ConnectionSettingDefinition("host", "Host", "text", true, "Machine host or endpoint.") }, "high"),
            "Fanuc CNC support requires vendor SDK validation and is not enabled in this scaffold."));
        services.AddSingleton<IDeviceDriverRegistry, DeviceDriverRegistry>();

        services.AddSingleton<IUploadTransport, HttpUploadTransport>();
        services.AddSingleton<IUploadTransport, IotSharpMqttUploadTransport>();
        services.AddSingleton<IUploadTransport, IotSharpDeviceHttpUploadTransport>();
        services.AddSingleton<IUploadTransportRegistry, UploadTransportRegistry>();

        return services;
    }
}
