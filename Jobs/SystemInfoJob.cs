using CZGL.SystemInfo;
using IoTSharp.Gateways.Data;
using IoTSharp.MqttSdk;
using Microsoft.Extensions.Caching.Memory;
using Quartz;

namespace IoTSharp.Gateways.Jobs
{
    public class SystemInfoJob : IJob
    {

        private ILogger _logger;

        private ApplicationDbContext _dbContext;
        private MQTTClient _client;
        private IMemoryCache _cache;
        private readonly ILoggerFactory _factory;
        private IServiceScope _serviceScope;

        public SystemInfoJob(ILoggerFactory factory, IServiceScopeFactory scopeFactor, MQTTClient client, IMemoryCache cache)
        {
            _factory = factory;
            _serviceScope = scopeFactor.CreateScope();
            _dbContext = _serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            _client = client;
            _cache = cache;
        }
        int GetCPULoad()
        {
            CPUTime v1 = CPUHelper.GetCPUTime();
            Thread.Sleep(1000);
            var v2 = CPUHelper.GetCPUTime();
            var value = CPUHelper.CalculateCPULoad(v1, v2);
            v1 = v2;
            return (int)(value * 100);
        }
        public async Task Execute(IJobExecutionContext context)
        {
            var network = NetworkInfo.TryGetRealNetworkInfo();
            var memory = MemoryHelper.GetMemoryValue();
            await _client.UploadAttributeAsync(new
            {
                SystemPlatformInfo.MachineName,
                SystemPlatformInfo.OSArchitecture,
                SystemPlatformInfo.OSDescription,
                SystemPlatformInfo.OSPlatformID,
                SystemPlatformInfo.OSVersion,

                SystemPlatformInfo.ProcessArchitecture,
                SystemPlatformInfo.ProcessorCount,
                SystemPlatformInfo.FrameworkVersion,
                SystemPlatformInfo.FrameworkDescription,
                SystemPlatformInfo.GetLogicalDrives,
                SystemPlatformInfo.UserName,
                network.NetworkType ,
                NetworkName =network.Name, 
               NetworkId= network.Id,
                NetworkTrademark= network.Trademark,
                memory.TotalPhysicalMemory,
                memory.TotalVirtualMemory
            });

         await   _client.UploadTelemetryDataAsync(new {
                CPULoad=  GetCPULoad(),
                memory.UsedPercentage,
                memory.AvailableVirtualMemory,
                memory.AvailablePhysicalMemory,
                NetworkSend=network.GetIpv4Speed().SendLength,
                NetworkReceived= network.GetIpv4Speed().ReceivedLength,
                NetworkSpeed=network.Speed

            });
        }
    }
}
