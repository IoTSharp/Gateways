using IoTSharp.Edge.Domain;

namespace IoTSharp.Edge.Application;

/// <summary>
/// 采集协议目录服务。
/// 将驱动目录转换为前端可直接消费的采集协议描述。
/// </summary>
public sealed class CollectionProtocolCatalogService : ICollectionProtocolCatalog
{
    private readonly DriverCatalogService _driverCatalogService;

    public CollectionProtocolCatalogService(DriverCatalogService driverCatalogService)
    {
        _driverCatalogService = driverCatalogService;
    }

    public IReadOnlyCollection<CollectionProtocolDescriptor> GetProtocols()
        => _driverCatalogService.GetDrivers()
            .Select(Map)
            .OrderBy(descriptor => ResolveCategoryOrder(descriptor.DriverType))
            .ThenBy(descriptor => ResolveProtocolOrder(descriptor.Code))
            .ThenBy(descriptor => descriptor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static CollectionProtocolDescriptor Map(DriverDefinition driver)
        => new(
            driver.Code,
            ResolveContractProtocol(driver.Code),
            driver.DriverType,
            driver.DisplayName,
            ResolveCategory(driver.DriverType),
            driver.Description,
            ResolveLifecycle(driver),
            driver.SupportsRead,
            driver.SupportsWrite,
            driver.SupportsBatchRead,
            driver.SupportsBatchWrite,
            driver.RiskLevel,
            driver.ConnectionSettings);

    private static string ResolveContractProtocol(string driverCode)
        => driverCode.ToLowerInvariant() switch
        {
            "modbus" => "Modbus",
            "opc-ua" => "OpcUa",
            "opc-da" => "OpcDa",
            "mt-cnc" => "MtCnc",
            "fanuc-cnc" => "FanucCnc",
            "bacnet" => "Bacnet",
            "iec104" => "Iec104",
            "mqtt" => "Mqtt",
            "siemens-s7" => "SiemensS7",
            "mitsubishi" => "Mitsubishi",
            "omron-fins" => "OmronFins",
            "allen-bradley" => "AllenBradley",
            _ => driverCode
        };

    private static string ResolveCategory(DriverType driverType)
        => driverType switch
        {
            DriverType.MtCnc or DriverType.FanucCnc => "CNC",
            DriverType.Bacnet or DriverType.Iec104 or DriverType.Mqtt or DriverType.OpcDa => "其他协议",
            _ => "PLC"
        };

    private static int ResolveCategoryOrder(DriverType driverType)
        => ResolveCategory(driverType) switch
        {
            "PLC" => 0,
            "CNC" => 1,
            "其他协议" => 2,
            _ => 99
        };

    private static int ResolveProtocolOrder(string code)
        => code.ToLowerInvariant() switch
        {
            "modbus" => 0,
            "siemens-s7" => 1,
            "mitsubishi" => 2,
            "omron-fins" => 3,
            "allen-bradley" => 4,
            "opc-ua" => 5,
            "mt-cnc" => 6,
            "fanuc-cnc" => 7,
            "opc-da" => 8,
            "bacnet" => 9,
            "iec104" => 10,
            "mqtt" => 11,
            _ => 99
        };

    private static string ResolveLifecycle(DriverDefinition driver)
    {
        if (driver.Code is "opc-da" or "fanuc-cnc")
        {
            return "guarded";
        }

        if (driver.RiskLevel.Equals("planned", StringComparison.OrdinalIgnoreCase))
        {
            return "planned";
        }

        if (driver.RiskLevel.Equals("high", StringComparison.OrdinalIgnoreCase))
        {
            return "guarded";
        }

        return "ready";
    }
}
