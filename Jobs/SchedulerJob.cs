using IoTSharp.Gateways.Data;
using Quartz;

namespace IoTSharp.Gateways.Jobs
{
    public class SchedulerJob : IJob
    {
        public const string client_id = "client_id";
        public const string client_name = "client_name";
        private ILogger _logger;
        private ApplicationDbContext _dbContext;
        public SchedulerJob(ILogger<SchedulerJob> logger, ApplicationDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;

        }
        public async Task<IJobDetail> DetectionJob<T>(IJobExecutionContext context) where T : IJob
        {
            var _scheduler = context.Scheduler;
            var jobkey = new JobKey(typeof(T).FullName);
            IJobDetail job;
            var jobexists = await _scheduler.CheckExists(jobkey, context.CancellationToken);
            if (!jobexists)
            {

                job = JobBuilder.Create<T>()
                .WithIdentity(jobkey.Name)
                .StoreDurably()
                .Build();
                await _scheduler.AddJob(job, true, context.CancellationToken);
            }
            else
            {
                job = await _scheduler.GetJobDetail(jobkey, context.CancellationToken);
            }
            return job;
        }
        public async Task Execute(IJobExecutionContext context)
        {
            var _scheduler = context.Scheduler;

            var clients = _dbContext.Clients.ToList();
            foreach (var client in clients)
            {
                IJobDetail job;
                switch (client.Address.Scheme)
                {
                    case "opc":
                        job = await DetectionJob<OPCUAJob>(context);
                        break;
                    case "dtu":
                    case "rtu":
                    case "d2t":
                    default:
                        job = await DetectionJob<ModbusJob>(context);
                        break;
                }

                var triggers = await _scheduler.GetTriggersOfJob(job.Key, context.CancellationToken);
                var clientid = client.Id.ToString();
                int interval = client.TimeInterval == 0 ? 30 : (int)client.TimeInterval;
                if (!triggers.Any(t => t.Key.Name == clientid))
                {
                    var trg = TriggerBuilder.Create()
                     .WithIdentity(clientid)
                     .ForJob(job)
                     .UsingJobData(client_id, clientid)
                     .UsingJobData(client_name, client.DeviceName)
                     .WithSimpleSchedule(x => x.WithIntervalInSeconds(interval).RepeatForever()).StartNow()
                     .Build();
                    await _scheduler.ScheduleJob(trg, context.CancellationToken);
                }
            }
            _logger.LogInformation($"{_scheduler.IsStarted}");
        }
    }
}
