namespace IoTEdge.Application;

/// <summary>
/// 网关持久化仓储接口。
/// 负责渠道、设备、点位、轮询任务、转换规则、上传路由和写命令的持久化读写。
/// </summary>
public interface IGatewayRepository
{
    /// <summary>
    /// 获取全部渠道。
    /// </summary>
    Task<IReadOnlyCollection<GatewayChannel>> GetChannelsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 根据标识获取渠道。
    /// </summary>
    Task<GatewayChannel?> GetChannelAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 保存渠道。
    /// </summary>
    Task SaveChannelAsync(GatewayChannel channel, CancellationToken cancellationToken);

    /// <summary>
    /// 删除渠道。
    /// </summary>
    Task DeleteChannelAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 获取全部设备。
    /// </summary>
    Task<IReadOnlyCollection<Device>> GetDevicesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 根据标识获取设备。
    /// </summary>
    Task<Device?> GetDeviceAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 获取指定渠道下的设备。
    /// </summary>
    Task<IReadOnlyCollection<Device>> GetDevicesByChannelAsync(Guid channelId, CancellationToken cancellationToken);

    /// <summary>
    /// 保存设备。
    /// </summary>
    Task SaveDeviceAsync(Device device, CancellationToken cancellationToken);

    /// <summary>
    /// 删除设备。
    /// </summary>
    Task DeleteDeviceAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 获取全部点位。
    /// </summary>
    Task<IReadOnlyCollection<Point>> GetPointsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 根据标识获取点位。
    /// </summary>
    Task<Point?> GetPointAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 获取指定设备下的点位。
    /// </summary>
    Task<IReadOnlyCollection<Point>> GetPointsByDeviceAsync(Guid deviceId, CancellationToken cancellationToken);

    /// <summary>
    /// 保存点位。
    /// </summary>
    Task SavePointAsync(Point point, CancellationToken cancellationToken);

    /// <summary>
    /// 删除点位。
    /// </summary>
    Task DeletePointAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 获取全部轮询任务。
    /// </summary>
    Task<IReadOnlyCollection<PollingTask>> GetPollingTasksAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 根据标识获取轮询任务。
    /// </summary>
    Task<PollingTask?> GetPollingTaskAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 获取指定设备下的轮询任务。
    /// </summary>
    Task<IReadOnlyCollection<PollingTask>> GetPollingTasksByDeviceAsync(Guid deviceId, CancellationToken cancellationToken);

    /// <summary>
    /// 保存轮询任务。
    /// </summary>
    Task SavePollingTaskAsync(PollingTask task, CancellationToken cancellationToken);

    /// <summary>
    /// 删除轮询任务。
    /// </summary>
    Task DeletePollingTaskAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 获取全部转换规则。
    /// </summary>
    Task<IReadOnlyCollection<TransformRule>> GetTransformRulesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 获取指定点位下的转换规则。
    /// </summary>
    Task<IReadOnlyCollection<TransformRule>> GetTransformRulesByPointAsync(Guid pointId, CancellationToken cancellationToken);

    /// <summary>
    /// 保存转换规则。
    /// </summary>
    Task SaveTransformRuleAsync(TransformRule rule, CancellationToken cancellationToken);

    /// <summary>
    /// 删除转换规则。
    /// </summary>
    Task DeleteTransformRuleAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 获取全部上传通道。
    /// </summary>
    Task<IReadOnlyCollection<UploadChannel>> GetUploadChannelsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 根据标识获取上传通道。
    /// </summary>
    Task<UploadChannel?> GetUploadChannelAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 保存上传通道。
    /// </summary>
    Task SaveUploadChannelAsync(UploadChannel channel, CancellationToken cancellationToken);

    /// <summary>
    /// 删除上传通道。
    /// </summary>
    Task DeleteUploadChannelAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 获取全部上传路由。
    /// </summary>
    Task<IReadOnlyCollection<UploadRoute>> GetUploadRoutesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 获取指定点位下的上传路由。
    /// </summary>
    Task<IReadOnlyCollection<UploadRoute>> GetUploadRoutesByPointAsync(Guid pointId, CancellationToken cancellationToken);

    /// <summary>
    /// 保存上传路由。
    /// </summary>
    Task SaveUploadRouteAsync(UploadRoute route, CancellationToken cancellationToken);

    /// <summary>
    /// 删除上传路由。
    /// </summary>
    Task DeleteUploadRouteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 保存写命令。
    /// </summary>
    Task SaveWriteCommandAsync(WriteCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// 获取全部写命令。
    /// </summary>
    Task<IReadOnlyCollection<WriteCommand>> GetWriteCommandsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 用一份完整快照替换当前配置。
    /// </summary>
    Task ReplaceConfigurationAsync(GatewayConfigurationSnapshot snapshot, CancellationToken cancellationToken);
}
