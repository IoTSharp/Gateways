using IoTSharp.Gateways.Application;

namespace IoTSharp.Gateways.Worker;

public sealed class GatewayPollingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GatewayPollingWorker> _logger;
    private readonly Dictionary<Guid, DateTimeOffset> _nextRuns = new();

    public GatewayPollingWorker(IServiceScopeFactory scopeFactory, ILogger<GatewayPollingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IGatewayRepository>();
                var runtimeService = scope.ServiceProvider.GetRequiredService<GatewayRuntimeService>();
                var tasks = (await repository.GetPollingTasksAsync(stoppingToken)).Where(task => task.Enabled).ToArray();
                var now = DateTimeOffset.UtcNow;
                foreach (var task in tasks)
                {
                    if (_nextRuns.TryGetValue(task.Id, out var nextRun) && nextRun > now)
                    {
                        continue;
                    }

                    var report = await runtimeService.ExecutePollingTaskAsync(task.Id, stoppingToken);
                    _logger.LogInformation("Executed polling task {TaskName} with {SuccessCount} successes and {FailureCount} failures.", report.TaskName, report.SuccessCount, report.FailureCount);
                    _nextRuns[task.Id] = now.AddSeconds(Math.Max(task.IntervalSeconds, 1));
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Gateway polling iteration failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
