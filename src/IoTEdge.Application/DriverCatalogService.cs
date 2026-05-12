namespace IoTEdge.Application;

/// <summary>
/// 驱动目录服务。
/// 将驱动注册表中的元数据映射为前端或接口层更易消费的定义对象。
/// </summary>
public sealed class DriverCatalogService
{
    private readonly IDeviceDriverRegistry _registry;

    public DriverCatalogService(IDeviceDriverRegistry registry)
    {
        _registry = registry;
    }

    public IReadOnlyCollection<DriverDefinition> GetDrivers()
        => _registry.GetMetadata()
            .Select(metadata => new DriverDefinition
            {
                Code = metadata.Code,
                DriverType = metadata.DriverType,
                DisplayName = metadata.DisplayName,
                Description = metadata.Description,
                SupportsRead = metadata.SupportsRead,
                SupportsWrite = metadata.SupportsWrite,
                SupportsBatchRead = metadata.SupportsBatchRead,
                SupportsBatchWrite = metadata.SupportsBatchWrite,
                ConnectionSettings = metadata.ConnectionSettings,
                RiskLevel = metadata.RiskLevel
            })
            .ToArray();
}
