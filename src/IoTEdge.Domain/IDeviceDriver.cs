namespace IoTEdge.Domain;

/// <summary>
/// 设备驱动的统一抽象。
/// 负责连接测试、地址校验、单点读写和批量读写。
/// </summary>
public interface IDeviceDriver
{
    /// <summary>
    /// 获取驱动元数据，用于注册、展示和能力声明。
    /// </summary>
    DriverMetadata Metadata { get; }

    /// <summary>
    /// 测试当前连接配置是否可用。
    /// </summary>
    Task<ConnectionTestResult> TestConnectionAsync(DriverConnectionContext context, CancellationToken cancellationToken);

    /// <summary>
    /// 校验单个读点地址是否符合驱动要求。
    /// </summary>
    Task<AddressValidationResult> ValidateAddressAsync(DriverReadRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 读取单个点位数据。
    /// </summary>
    Task<DriverReadResult> ReadAsync(DriverConnectionContext context, DriverReadRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 批量读取多个点位数据。
    /// </summary>
    Task<IReadOnlyCollection<DriverReadResult>> ReadBatchAsync(DriverConnectionContext context, DriverBatchReadRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 写入单个点位数据。
    /// </summary>
    Task<DriverWriteResult> WriteAsync(DriverConnectionContext context, DriverWriteRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 批量写入多个点位数据。
    /// </summary>
    Task<IReadOnlyCollection<DriverWriteResult>> WriteBatchAsync(DriverConnectionContext context, DriverBatchWriteRequest request, CancellationToken cancellationToken);
}
