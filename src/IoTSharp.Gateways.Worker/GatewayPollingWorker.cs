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
            DateTimeOffset soonest = DateTimeOffset.UtcNow.AddSeconds(30);
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IGatewayRepository>();
                var runtimeService = scope.ServiceProvider.GetRequiredService<GatewayRuntimeService>();
                var tasks = (await repository.GetPollingTasksAsync(stoppingToken)).Where(task => task.Enabled).ToArray();
                var now = DateTimeOffset.UtcNow;

                // Prune _nextRuns entries for tasks that no longer exist or are disabled.
                var activeIds = tasks.Select(t => t.Id).ToHashSet();
                foreach (var stale in _nextRuns.Keys.Where(id => !activeIds.Contains(id)).ToList())
                {
                    _nextRuns.Remove(stale);
                }

                foreach (var task in tasks)
                {
                    if (_nextRuns.TryGetValue(task.Id, out var nextRun) && nextRun > now)
                    {
                        if (nextRun < soonest)
                        {
                            soonest = nextRun;
                        }
                        continue;
                    }

                    var report = await runtimeService.ExecutePollingTaskAsync(task.Id, stoppingToken);
                    _logger.LogInformation("Executed polling task {TaskName} with {SuccessCount} successes and {FailureCount} failures.", report.TaskName, report.SuccessCount, report.FailureCount);
                    var taskNext = now.AddSeconds(Math.Max(task.IntervalSeconds, 1));
                    _nextRuns[task.Id] = taskNext;
                    if (taskNext < soonest)
                    {
                        soonest = taskNext;
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Gateway polling iteration failed.");
            }

            var delay = soonest - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }
        }
    }
}
