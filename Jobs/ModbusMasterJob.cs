using AMWD.Modbus.Common.Interfaces;
using AMWD.Modbus.Common.Structures;
using IoTSharp.Gateways.Data;
using IoTSharp.MqttSdk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Quartz;
using System.IO.Ports;
using System.Web;
using System.Numerics;
using System.Collections.Specialized;
 

namespace IoTSharp.Gateways.Jobs
{

    public class ModbusMasterJob : IJob
    {

        private   ILogger _logger;

        private ApplicationDbContext _dbContext;
        private MQTTClient _client;
        private IMemoryCache _cache;
        private readonly ILoggerFactory _factory;
        private IServiceScope _serviceScope;

        public ModbusMasterJob( ILoggerFactory factory, IServiceScopeFactory scopeFactor, MQTTClient client, IMemoryCache cache)
        {
            _factory = factory;
            _serviceScope = scopeFactor.CreateScope();
            _dbContext = _serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            _client = client;
            _cache = cache;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var slave_id = new Guid( context.Trigger.JobDataMap.GetString("slave_id"));
            var slave_name = context.Trigger.JobDataMap.GetString("slave_name");
         
            var slave = await _dbContext.ModbusSlaves.FirstOrDefaultAsync(m=>m.Id== slave_id);
            if (slave != null)
            {
                _logger = _factory.CreateLogger($"Slaver:{slave_name}({slave.Slave})");
                /// DTU:  dtu://dev.ttyS0/?BaudRate=115200
                /// DTU:  dtu://COM1:115200
                /// TCP:  tcp://www.host.com:602
                /// d2t:  d2t://www.host.com:602
                var client = CreateModbusSlave(slave);
                try
                {

                    if (!client.IsConnected) await client.Connect(context.CancellationToken);
                    if (client.IsConnected)
                    {
                        await ReadDatas(slave, client, context.CancellationToken);
                        await client.Disconnect(context.CancellationToken);
                    }

                }
                catch (Exception ex)
                {
                    var msg = $"SlaveId:{slave_id},Device:{slave.DeviceName},IsConnected:{client?.IsConnected},Message:{ex.Message}";
                    _logger.LogError(ex, msg);
                    throw new Exception(msg, ex);
                }
                finally
                {
                    client.Dispose();
                }
            }
            else
            {
                _logger.LogWarning($"未能找到从机{slave_id}");
            }

        }

        private async Task ReadDatas(ModbusSlave slave, IModbusClient client, CancellationToken stoppingToken)
        {
            var points = _dbContext.PointMappings.Include(p => p.Owner).Where(p => p.Owner == slave).ToList();
            foreach (var point in points)
            {
                try
                {
                    switch (point.FunCode)
                    {
                        case FunCode.ReadCoils:
                            var _coils = await client.ReadCoils(point.SlaveCode, point.Address, point.Length, stoppingToken);
                            await UploadCoils(slave, point, _coils);
                            break;
                        case FunCode.ReadDiscreteInputs:
                            await UploadDiscreteInputs(slave, client, point, stoppingToken);
                            break;

                        case FunCode.ReadMultipleHoldingRegisters:
                            var _registers = await client.ReadHoldingRegisters(point.SlaveCode, point.Address, point.Length, stoppingToken);
                            await UploadRegisters(slave, point, _registers);
                            break;

                        case FunCode.ReadInputRegisters:
                            var _input_registers = await client.ReadInputRegisters(point.SlaveCode, point.Address, point.Length, stoppingToken);
                            await UploadRegisters(slave, point, _input_registers);
                            break;
                        default:
                            break;
                    }
                    _logger.LogInformation($"从{slave.Slave}执行{point.FunCode} 将地址{point.Address}的长度{point.Length}的数据存储到名称{point.DataName}类型{point.DataType}完成。 ");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"从{slave.Slave}执行{point.FunCode} 将地址{point.Address}的长度{point.Length}的数据存储到名称{point.DataName}类型{point.DataType}时遇到错误{ex.Message}。");
                }
            }
        }

        private async Task UploadDiscreteInputs(ModbusSlave slave, IModbusClient? client, PointMapping point, CancellationToken stoppingToken)
        {
            var _discreteInputs = await client.ReadDiscreteInputs(point.SlaveCode, point.Address, point.Length, stoppingToken);
            switch (point.DataType)
            {
                case DataType.Boolean:
                    await UploadData(slave, point, _discreteInputs.First().BoolValue);
                    break;
                case DataType.Double:
                case DataType.Long:
                    await UploadData(slave, point, _discreteInputs.First().RegisterValue + 0.0);
                    break;
                default:
                    break;
            }
        }

        private async Task UploadCoils(ModbusSlave slave, PointMapping point, List<Coil> _coils)
        {
            switch (point.DataType)
            {
                case DataType.Boolean:
                    await UploadData(slave, point, _coils.First().BoolValue);
                    break;
                case DataType.Double:
                case DataType.Long:
                    await UploadData(slave, point, _coils.First().RegisterValue + 0.0);
                    break;
                default:
                    break;
            }
        }

        private async Task UploadRegisters(ModbusSlave slave, PointMapping point, List<Register> _registers)
        {
            switch (point.DataType)
            {
                case DataType.String:
                    await UploadData(slave, point, RegistersToString(point, _registers));
                    break;

                case DataType.Long:
                    if (_registers.Count == 1)
                    {
                        await UploadData(slave, point, _registers.First().RegisterValue);
                    }
                    else if (_registers.Count == 2)
                    {
                        await UploadData(slave, point, RegistersToUint32(_registers));
                    }
                    else if (_registers.Count == 4)
                    {
                        await UploadData(slave, point, RegistersToUint64(_registers));
                    }
                    break;

                case DataType.Double:
                    if (_registers.Count == 2)
                    {
                        await UploadData(slave, point, RegistersToFloat(_registers));
                    }
                    else if (_registers.Count == 4)
                    {
                        await UploadData(slave, point, RegistersToDouble(_registers));
                    }
                    break;

                case DataType.DateTime:

                    break;
                    case DataType.Boolean:
                    await UploadData(slave, point, RegistersToBitVector32(_registers));
                    break;
                default:
                    _logger.LogWarning($"多寄存器读取方式不支持类型{point.DataType}");
                    break;
            }
        }

        private static string RegistersToString(PointMapping point, List<Register> _registers)
        {
            var buffer = new List<byte>();
            _registers.ForEach(p =>
            {
                buffer.Add(p.HiByte);
                buffer.Add(p.LoByte);
            });
            var stringvalue = System.Text.Encoding.GetEncoding(point.CodePage).GetString(buffer.ToArray()).TrimNull();
            return stringvalue;
        }

        private static uint RegistersToUint32(List<Register> _registers)
        {
            var buff = new byte[] { _registers[0].HiByte, _registers[0].LoByte, _registers[1].HiByte, _registers[1].LoByte };
            var uint32 = BitConverter.ToUInt32(buff);
            return uint32;
        }

        private static ulong RegistersToUint64(List<Register> _registers)
        {
            var buff = new byte[] { _registers[0].HiByte, _registers[0].LoByte, _registers[1].HiByte, _registers[1].LoByte
                                            , _registers[2].HiByte, _registers[2].LoByte, _registers[3].HiByte, _registers[3].LoByte};
            var uint64 = BitConverter.ToUInt64(buff);
            return uint64;
        }

        private static float RegistersToFloat(List<Register> _registers)
        {
            var buff = new byte[] { _registers[0].HiByte, _registers[0].LoByte, _registers[1].HiByte, _registers[1].LoByte };
            var uint32 = BitConverter.ToSingle(buff);
            return uint32;
        }

        private static double RegistersToDouble(List<Register> _registers)
        {
            var buff = new byte[] { _registers[0].HiByte, _registers[0].LoByte, _registers[1].HiByte, _registers[1].LoByte
                                            , _registers[2].HiByte, _registers[2].LoByte, _registers[3].HiByte, _registers[3].LoByte};
            var uint64 = BitConverter.ToDouble(buff);
            return uint64;
        }
        private static BitVector32 RegistersToBitVector32(List<Register> _registers)
        {
            BitVector32 vector32=new BitVector32 (0);
            if (_registers.Count == 1)//16位
            {
                vector32= new BitVector32( BitConverter.ToInt32(new byte[] { _registers[0].HiByte, _registers[0].LoByte,0,0 }));
            }
            else if (_registers.Count == 2)//32位 
            {
                vector32 = new BitVector32(BitConverter.ToInt32(new byte[] { _registers[0].HiByte, _registers[0].LoByte, _registers[1].HiByte, _registers[1].LoByte }));
            }
            return vector32;
        }

        private async Task UploadData<T>(ModbusSlave slave, PointMapping point, T? value)
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

        private async Task UploadData(ModbusSlave slave, PointMapping point, BitVector32 value)
        {
            Dictionary<string, short> lst = new Dictionary<string, short>();
            var _format = point.DateTimeFormat ?? $"{point.DataName}_unknow1:8;{point.DataName}_unknow2:8";
            _format.Split(';').ToList().ForEach(s =>
            {
                var sk = s.Split(':');
                lst.Add(sk[0], short.Parse(sk[1]));
            });
            var objx = value.ToDictionary(lst);
            JObject jo = new()
                {
                    { point.DataName, new JValue(value.Data) }
                };
            objx.Keys.ToList().ForEach(s =>
            {
                jo.Add(s, objx[s]);
            });
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

        private IModbusClient? CreateModbusSlave(ModbusSlave slave)
        {
            var url = slave.Slave;
            IModbusClient? client = null;
            switch (url.Scheme)
            {
                case "dtu":
                    string comname = ParseCOMName(url);
                    var dtu = new AMWD.Modbus.Serial.Client.ModbusClient(comname, _logger);
                    ParseDtuParam(url, dtu);
                    client = dtu;
                    break;
                case "tcp":
                    client = new AMWD.Modbus.Tcp.Client.ModbusClient(url.Host, url.Port, _logger);
                    break;
                case "d2t":
                    client = new AMWD.Modbus.SerialOverTCP.Client.ModbusClient(url.Host, url.Port, _logger);
                    break;
                default:
                    break;
            }
            return client;
        }

        private static string ParseCOMName(Uri url)
        {
            var comname = string.Empty;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.Win32NT:
                case PlatformID.WinCE:
                case PlatformID.Xbox:
                    comname = url.Host;
                    break;

                case PlatformID.Other:
                case PlatformID.Unix:
                case PlatformID.MacOSX:

                default:
                    comname = url.Host.Replace('.', '/');
                    break;
            }

            return comname;
        }

        private void ParseDtuParam(Uri url, AMWD.Modbus.Serial.Client.ModbusClient dtu)
        {
            var query = HttpUtility.ParseQueryString(url.Query);
            if (query.HasKeys())
            {
                foreach (string key in query.Keys)
                {
                    switch (key)
                    {
                        case nameof(dtu.BaudRate):
                            if (Enum.TryParse(query.Get(key), true, out AMWD.Modbus.Serial.BaudRate _baudrate))
                            {
                                dtu.BaudRate = _baudrate;
                            }
                            break;

                        case nameof(dtu.Parity):
                            if (Enum.TryParse(query.Get(key), true, out Parity parity))
                            {
                                dtu.Parity = parity;
                            }
                            break;

                        case nameof(dtu.Handshake):
                            if (Enum.TryParse(query.Get(key), true, out Handshake handshake))
                            {
                                dtu.Handshake = handshake;
                            }
                            break;

                        case nameof(dtu.StopBits):
                            if (Enum.TryParse(query.Get(key), true, out StopBits stopBits))
                            {
                                dtu.StopBits = stopBits;
                            }
                            break;

                        case nameof(dtu.DataBits):
                            if (int.TryParse(query.Get(key), out int _DataBits))
                            {
                                dtu.DataBits = _DataBits;
                            }
                            break;

                        default:
                            break;
                    }
                }
            }
        }
      
    }
}
