using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IoTEdge.Application;
using IoTEdge.Domain;
using IoTEdge.Infrastructure.Drivers;
using IoTEdge.Infrastructure.Persistence;
using IoTEdge.Infrastructure.Uploads;

namespace IoTEdge.Infrastructure;

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
            new DriverMetadata("bacnet", DriverType.Bacnet, "BACnet 协议", "BACnet/IP 采集契约，用于楼宇自动化设备和对象属性。", true, true, true, true,
                new[]
                {
                    new ConnectionSettingDefinition("host", "主机", "text", true, "BACnet 设备主机名或 IP 地址。"),
                    new ConnectionSettingDefinition("port", "端口", "number", true, "BACnet/IP UDP 端口，通常为 47808。"),
                    new ConnectionSettingDefinition("deviceInstance", "设备实例", "number", true, "BACnet 设备实例标识。"),
                    new ConnectionSettingDefinition("networkNumber", "网络号", "number", false, "可选的 BACnet 网络号。"),
                    new ConnectionSettingDefinition("timeout", "超时", "number", false, "超时时间，单位毫秒。")
                }, "planned"),
            "BACnet/IP 采集已规划为协议适配器。启用运行时读取前，请先接入 BACnet 协议栈，例如 BACnet4J.NET 或原生适配器。"));
        services.AddSingleton<IDeviceDriver>(_ => new UnsupportedDriver(
            new DriverMetadata("iec104", DriverType.Iec104, "IEC 104 规约", "IEC 60870-5-104 遥控采集契约，用于电力和 SCADA 终端。", true, true, true, true,
                new[]
                {
                    new ConnectionSettingDefinition("host", "主机", "text", true, "IEC 104 服务器主机名或 IP 地址。"),
                    new ConnectionSettingDefinition("port", "端口", "number", true, "IEC 104 TCP 端口，通常为 2404。"),
                    new ConnectionSettingDefinition("commonAddress", "公共地址", "number", true, "ASDU 公共地址。"),
                    new ConnectionSettingDefinition("originatorAddress", "源地址", "number", false, "可选的源地址。"),
                    new ConnectionSettingDefinition("timeout", "超时", "number", false, "超时时间，单位毫秒。")
                }, "planned"),
            "IEC 60870-5-104 采集已规划为协议适配器。启用运行时读取前，请先接入 IEC 104 协议栈。"));
        services.AddSingleton<IDeviceDriver>(_ => new UnsupportedDriver(
            new DriverMetadata("mqtt", DriverType.Mqtt, "MQTT 订阅", "MQTT 订阅式采集契约，用于接收主题负载。", true, false, true, false,
                new[]
                {
                    new ConnectionSettingDefinition("host", "主机", "text", true, "MQTT 代理主机名或 IP 地址。"),
                    new ConnectionSettingDefinition("port", "端口", "number", true, "MQTT 代理端口，通常为 1883。"),
                    new ConnectionSettingDefinition("clientId", "客户端 ID", "text", true, "边缘采集器使用的客户端标识。"),
                    new ConnectionSettingDefinition("topic", "主题", "text", true, "要订阅的主题或主题过滤器。"),
                    new ConnectionSettingDefinition("qos", "服务质量", "select", false, "MQTT 服务质量。", new[] { "0", "1", "2" }),
                    new ConnectionSettingDefinition("username", "用户名", "text", false, "可选的代理用户名。"),
                    new ConnectionSettingDefinition("password", "密码", "password", false, "可选的代理密码。")
                }, "planned"),
            "MQTT 采集已规划为订阅适配器。当前 MQTT 实现仅作为上传传输层注册。"));
        services.AddSingleton<IDeviceDriver>(_ => new UnsupportedDriver(
            new DriverMetadata("opc-da", DriverType.OpcDa, "OPC DA 协议", "仅限 Windows 的 OPC DA Classic COM/DCOM 驱动契约。", true, true, true, true,
                new[]
                {
                    new ConnectionSettingDefinition("progId", "ProgId", "text", true, "OPC DA 服务器 ProgId，例如 Matrikon.OPC.Simulation.1。"),
                    new ConnectionSettingDefinition("host", "主机", "text", false, "可选的远程 OPC DA 主机。"),
                    new ConnectionSettingDefinition("clsid", "CLSID", "text", false, "当 ProgId 不足时可选的 COM CLSID。")
                }, "high"),
            "OPC DA 依赖 Windows COM/DCOM。请使用仅限 Windows 的适配器包，例如 TitaniumAS.Opc.Client，或先将 OPC DA 桥接到 OPC UA。"));
        services.AddSingleton<IDeviceDriver>(_ => new UnsupportedDriver(
            new DriverMetadata("fanuc-cnc", DriverType.FanucCnc, "发那科 CNC", "发那科 FOCAS 原生 SDK 驱动契约。", true, true, true, true,
                new[]
                {
                    new ConnectionSettingDefinition("host", "主机", "text", true, "CNC IP 地址或主机名。"),
                    new ConnectionSettingDefinition("port", "端口", "number", false, "FOCAS TCP 端口，通常为 8193。"),
                    new ConnectionSettingDefinition("timeout", "超时", "number", false, "FOCAS 超时时间，单位秒。"),
                    new ConnectionSettingDefinition("libraryPath", "FOCAS 库", "text", false, "fwlib32/fwlib64 原生库的可选路径。")
                }, "high"),
            "Fanuc CNC 支持需要授权的 Fanuc FOCAS 运行时（fwlib32/fwlib64）以及与架构相关的原生加载。请将其保持为可选适配器边界。"));
        services.AddSingleton<IDeviceDriverRegistry, DeviceDriverRegistry>();

        services.AddSingleton<IUploadTransport, HttpUploadTransport>();
        services.AddSingleton<IUploadTransport, IoTSharpUploadTransport>();
        services.AddSingleton<IUploadTransport, ThingsBoardUploadTransport>();
        services.AddSingleton<IUploadTransport, InfluxDbUploadTransport>();
        services.AddSingleton<IUploadTransport, IotSharpMqttUploadTransport>();
        services.AddSingleton<IUploadTransport, IotSharpDeviceHttpUploadTransport>();
        services.AddSingleton<IUploadTransport, SonnetDbUploadTransport>();
        services.AddSingleton<IUploadTransportRegistry, UploadTransportRegistry>();

        return services;
    }
}
