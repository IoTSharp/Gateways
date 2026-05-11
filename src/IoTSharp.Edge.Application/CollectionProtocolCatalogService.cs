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
            .OrderBy(descriptor => descriptor.Category, StringComparer.OrdinalIgnoreCase)
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
            _ => "PLC"
        };

    private static string ResolveLifecycle(DriverDefinition driver)
    {
        if (driver.Code is "opc-da" or "fanuc-cnc")
        {
            return "guarded";
        }

        if (driver.RiskLevel.Equals("high", StringComparison.OrdinalIgnoreCase))
        {
            return "guarded";
        }

        return "ready";
    }
}
