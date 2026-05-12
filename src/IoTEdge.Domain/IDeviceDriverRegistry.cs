namespace IoTEdge.Domain;

/// <summary>
/// 设备驱动注册表。
/// 用于按驱动编码查找驱动实例，并获取驱动元数据列表。
/// </summary>
public interface IDeviceDriverRegistry
{
    /// <summary>
    /// 获取全部已注册驱动的元数据。
    /// </summary>
    IReadOnlyCollection<DriverMetadata> GetMetadata();

    /// <summary>
    /// 根据驱动编码获取已注册驱动。
    /// </summary>
    IDeviceDriver GetRequiredDriver(string code);
}
