using IoTSharp.Gateways.Data;
using IoTSharp.MqttSdk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Quartz;
using OpcUaHelper;
using Opc.Ua;

namespace IoTSharp.Gateways.Jobs
{
    public class OPCUAJob : IJob
    {
        private ILogger _logger;
        private ApplicationDbContext _dbContext;
        private MQTTClient _client;
        private IMemoryCache _cache;
        private readonly ILoggerFactory _factory;
        private IServiceScope _serviceScope;

        public OPCUAJob(ILoggerFactory factory, IServiceScopeFactory scopeFactor, MQTTClient client, IMemoryCache cache)
        {
            _factory = factory;
            _serviceScope = scopeFactor.CreateScope();
            _dbContext = _serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            _client = client;
            _cache = cache;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var slave_id = new Guid(context.Trigger.JobDataMap.GetString(SchedulerJob.client_id));
            var slave_name = context.Trigger.JobDataMap.GetString(SchedulerJob.client_name);

            var slave = await _dbContext.Clients.FirstOrDefaultAsync(m => m.Id == slave_id);
            if (slave != null)
            {
                _logger = _factory.CreateLogger($"Slaver:{slave_name}({slave.Address})");
                OpcUaClient m_OpcUaClient = new OpcUaClient();

                try
                {
                    var uri = slave.Address;
                    if (!string.IsNullOrEmpty(uri.UserInfo))
                    {
                        var inf = uri.UserInfo.Split(':');
                        switch (inf.Length)
                        {
                            case 1:
                                m_OpcUaClient.UserIdentity = new UserIdentity();
                                break;
                            case 2:
                                m_OpcUaClient.UserIdentity = new UserIdentity(inf[0], inf[1]);
                                break;
                            default:
                                m_OpcUaClient.UserIdentity = new UserIdentity(new AnonymousIdentityToken());
                                break;
                        }
                    }
                    else
                    {
                        m_OpcUaClient.UserIdentity = new UserIdentity(new AnonymousIdentityToken());
                    }
                    await m_OpcUaClient.ConnectServer($"opc.tcp://{uri.Host}/{uri.PathAndQuery}");
                    if (m_OpcUaClient.Connected)
                    {
                        List<NodeId> nodes = new List<NodeId>();
                        var nodeIds = await _dbContext.OPCUAMappings.Include(p => p.Owner).Where(p => p.Owner == slave).ToListAsync();
                        nodeIds.ForEach(m => nodes.Add(new NodeId(m.NodeId)));
                        List<DataValue> dataValues = m_OpcUaClient.ReadNodes(nodes.ToArray());
                        for (int i = 0; i < dataValues.Count; i++)
                        {
                            var nodevalue = dataValues[i];
                            var nodeid = nodes[i];
                            var nodempa = nodeIds[i];
                            await UploadNodeData(slave, nodevalue, nodempa);
                        }
                    }

                }
                catch (Exception ex)
                {
                    var msg = $"SlaveId:{slave_id},Device:{slave.DeviceName},IsConnected:{m_OpcUaClient.Connected},Message:{ex.Message}";
                    _logger.LogError(ex, msg);
                    throw new Exception(msg, ex);
                }
                finally
                {
                }
            }
            else
            {
                _logger.LogWarning($"未能找到从机{slave_id}");
            }

        }

        private async Task UploadNodeData(Client slave, DataValue nodevalue, OPCUAMapping nodempa)
        {
            switch (nodevalue.WrappedValue.TypeInfo.BuiltInType)
            {
                case BuiltInType.Null:
                    break;
                case BuiltInType.Boolean:
                    await UploadData(slave, nodempa, nodevalue.GetValue(false));
                    break;
                case BuiltInType.SByte:
                    await UploadData(slave, nodempa, nodevalue.GetValue((sbyte)0));
                    break;
                case BuiltInType.Byte:
                    await UploadData(slave, nodempa, nodevalue.GetValue((byte)0));
                    break;
                case BuiltInType.Int16:
                    await UploadData(slave, nodempa, nodevalue.GetValue((short)0));
                    break;
                case BuiltInType.UInt16:
                    await UploadData(slave, nodempa, nodevalue.GetValue((ushort)0));
                    break;
                case BuiltInType.Int32:
                    await UploadData(slave, nodempa, nodevalue.GetValue(0));
                    break;
                case BuiltInType.UInt32:
                    await UploadData(slave, nodempa, nodevalue.GetValue((uint)0));
                    break;
                case BuiltInType.Int64:
                    await UploadData(slave, nodempa, nodevalue.GetValue((long)0));
                    break;
                case BuiltInType.UInt64:
                    await UploadData(slave, nodempa, nodevalue.GetValue((ulong)0));
                    break;
                case BuiltInType.Float:
                    await UploadData(slave, nodempa, nodevalue.GetValue((float)0));
                    break;
                case BuiltInType.Double:
                    await UploadData(slave, nodempa, nodevalue.GetValue((double)0));
                    break;
                case BuiltInType.String:
                    await UploadData(slave, nodempa, nodevalue.GetValue(string.Empty));
                    break;
                case BuiltInType.DateTime:
                    await UploadData(slave, nodempa, nodevalue.GetValue(new DateTime(1970, 1, 1, 0, 0, 0)));
                    break;
                case BuiltInType.Guid:
                    await UploadData(slave, nodempa, nodevalue.GetValue(Guid.NewGuid()));
                    break;
                case BuiltInType.ByteString:
                    //await UploadData(slave, nodempa, nodevalue.GetValue(byte[]);
                    break;
                case BuiltInType.XmlElement:
                    break;
                case BuiltInType.NodeId:
                    await UploadData(slave, nodempa, nodevalue.GetValue(string.Empty));
                    break;
                case BuiltInType.ExpandedNodeId:
                    break;
                case BuiltInType.StatusCode:
                    break;
                case BuiltInType.QualifiedName:
                    break;
                case BuiltInType.LocalizedText:
                    break;
                case BuiltInType.ExtensionObject:
                    break;
                case BuiltInType.DataValue:
                    break;
                case BuiltInType.Variant:
                    break;
                case BuiltInType.DiagnosticInfo:
                    break;
                case BuiltInType.Number:
                    await UploadData(slave, nodempa, nodevalue.GetValue((float)0));
                    break;
                case BuiltInType.Integer:
                    await UploadData(slave, nodempa, nodevalue.GetValue(0));
                    break;
                case BuiltInType.UInteger:
                    await UploadData(slave, nodempa, nodevalue.GetValue((uint)0));
                    break;
                case BuiltInType.Enumeration:
                    break;
                default:
                    break;
            }
        }

        private async Task UploadData<T>(Client slave, OPCUAMapping point, T? value)
        {
            if (value != null)
            {
                JObject jo = new()
                {
                    { point.DataName, new JValue(value) }
                };
                switch (point.DataCatalog)
                {
                    case DataCatalog.AttributeData:
                        await _client.UploadAttributeAsync(slave.DeviceName, jo);
                        break;

                    case DataCatalog.TelemetryData:
                        await _client.UploadTelemetryDataAsync(slave.DeviceName, jo);
                        break;

                    default:
                        break;
                }
            }
        }
    }
}
