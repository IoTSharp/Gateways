namespace IoTSharp.Edge.Application;

/// <summary>
/// 网关完整配置快照。
/// 用于一次性替换渠道、设备、点位、任务和路由数据。
/// </summary>
public sealed record GatewayConfigurationSnapshot(
    IReadOnlyCollection<GatewayChannel> Channels,
    IReadOnlyCollection<Device> Devices,
    IReadOnlyCollection<Point> Points,
    IReadOnlyCollection<PollingTask> PollingTasks,
    IReadOnlyCollection<TransformRule> TransformRules,
    IReadOnlyCollection<UploadChannel> UploadChannels,
    IReadOnlyCollection<UploadRoute> UploadRoutes);
