namespace IoTEdge.Application;

/// <summary>
/// 网关配置服务。
/// 对仓储提供一层更稳定的应用服务封装。
/// </summary>
public sealed class GatewayConfigurationService
{
    private readonly IGatewayRepository _repository;

    public GatewayConfigurationService(IGatewayRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyCollection<GatewayChannel>> GetChannelsAsync(CancellationToken cancellationToken) => _repository.GetChannelsAsync(cancellationToken);
    public Task SaveChannelAsync(GatewayChannel channel, CancellationToken cancellationToken) => _repository.SaveChannelAsync(channel, cancellationToken);
    public Task DeleteChannelAsync(Guid id, CancellationToken cancellationToken) => _repository.DeleteChannelAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<Device>> GetDevicesAsync(CancellationToken cancellationToken) => _repository.GetDevicesAsync(cancellationToken);
    public Task SaveDeviceAsync(Device device, CancellationToken cancellationToken) => _repository.SaveDeviceAsync(device, cancellationToken);
    public Task DeleteDeviceAsync(Guid id, CancellationToken cancellationToken) => _repository.DeleteDeviceAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<Point>> GetPointsAsync(CancellationToken cancellationToken) => _repository.GetPointsAsync(cancellationToken);
    public Task SavePointAsync(Point point, CancellationToken cancellationToken) => _repository.SavePointAsync(point, cancellationToken);
    public Task DeletePointAsync(Guid id, CancellationToken cancellationToken) => _repository.DeletePointAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<PollingTask>> GetPollingTasksAsync(CancellationToken cancellationToken) => _repository.GetPollingTasksAsync(cancellationToken);
    public Task SavePollingTaskAsync(PollingTask task, CancellationToken cancellationToken) => _repository.SavePollingTaskAsync(task, cancellationToken);
    public Task DeletePollingTaskAsync(Guid id, CancellationToken cancellationToken) => _repository.DeletePollingTaskAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<TransformRule>> GetTransformRulesAsync(CancellationToken cancellationToken) => _repository.GetTransformRulesAsync(cancellationToken);
    public Task SaveTransformRuleAsync(TransformRule rule, CancellationToken cancellationToken) => _repository.SaveTransformRuleAsync(rule, cancellationToken);
    public Task DeleteTransformRuleAsync(Guid id, CancellationToken cancellationToken) => _repository.DeleteTransformRuleAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<UploadChannel>> GetUploadChannelsAsync(CancellationToken cancellationToken) => _repository.GetUploadChannelsAsync(cancellationToken);
    public Task SaveUploadChannelAsync(UploadChannel channel, CancellationToken cancellationToken) => _repository.SaveUploadChannelAsync(channel, cancellationToken);
    public Task DeleteUploadChannelAsync(Guid id, CancellationToken cancellationToken) => _repository.DeleteUploadChannelAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<UploadRoute>> GetUploadRoutesAsync(CancellationToken cancellationToken) => _repository.GetUploadRoutesAsync(cancellationToken);
    public Task SaveUploadRouteAsync(UploadRoute route, CancellationToken cancellationToken) => _repository.SaveUploadRouteAsync(route, cancellationToken);
    public Task DeleteUploadRouteAsync(Guid id, CancellationToken cancellationToken) => _repository.DeleteUploadRouteAsync(id, cancellationToken);
}
