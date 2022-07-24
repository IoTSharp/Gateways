using AMWD.Modbus.Common.Interfaces;
using AMWD.Modbus.Common.Structures;
using IoTSharp.Gateway.Modbus.Data;
using IoTSharp.MqttSdk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using System.IO.Ports;
using System.Web;

namespace IoTSharp.Gateway.Modbus.Services
{
    public class ModbusMaster : BackgroundService
    {
        private readonly ILoggerFactory _factory;
        private readonly ILogger _logger;

        private ApplicationDbContext _dbContext;
        private MQTTClient _client;
        private IMemoryCache _cache;
        private IServiceScope _serviceScope;
        private Dictionary<Guid,IModbusClient> _modbusclients = new Dictionary<Guid,IModbusClient>();
        public ModbusMaster(ILogger<ModbusMaster> logger, ILoggerFactory factory, IServiceScopeFactory scopeFactor, MQTTClient client,IMemoryCache  cache)
        {
            _factory = factory;
            _logger = logger;
            _serviceScope = scopeFactor.CreateScope();
            _dbContext = _serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            _client = client;
            _cache = cache;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            do
            {
                stoppingToken.ThrowIfCancellationRequested();
                var pm = await _dbContext.Database.GetPendingMigrationsAsync();
                if (pm?.Count()>0)
                {
                    _logger.LogWarning($"有挂起的数据库结构未合并。");
                  await    Task.Delay(TimeSpan.FromMinutes(1));
                }
                else
                {
                    var slaves = _cache.GetOrCreate("db_ModbusSlaves", fc => _dbContext.ModbusSlaves.ToList());

                    foreach (var slave in slaves)
                    {
                        /// DTU:  dtu://dev.ttyS0/?BaudRate=115200
                        /// DTU:  dtu://COM1:115200
                        /// TCP:  tcp://www.host.com:602
                        if (!_modbusclients.ContainsKey(slave.Id))
                        {
                            _modbusclients.Add(slave.Id, CreateModbusSlave(slave));
                        }
                        if (_modbusclients.TryGetValue(slave.Id,out var client))
                        {
                            try
                            {
                                
                                      if (!client.IsConnected) await client.Connect(stoppingToken);
                                if (client.IsConnected)
                                {
                                    await ReadDatas(slave, client, stoppingToken);
                                    await client.Disconnect(stoppingToken);
                                }

                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"未能连接设备。{ex.Message}");
                                await Task.Delay(TimeSpan.FromSeconds(5));
                            }
                            finally
                            {
                                client.Dispose();
                            }
                        }
                        else
                        {
                            _logger.LogWarning( $"从机对象异常，基本信息：{slave.Id}-{slave.DeviceName}-{slave.Slave}。");
                        }
                    }
                }
            } while (!stoppingToken.IsCancellationRequested);
        }

        private async Task ReadDatas(ModbusSlave slave, IModbusClient client, CancellationToken stoppingToken)
        {
            var points = _dbContext.PointMappings.Include(p => p.Owner).Where(p => p.Owner == slave).ToList();
            foreach (var point in points)
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

        private IModbusClient? CreateModbusSlave(ModbusSlave slave)
        {
            var mlog = _factory.CreateLogger($"ModbusSlave{slave.Id}");
            var url = slave.Slave;
            IModbusClient? client = null;
            switch (url.Scheme)
            {
                case "dtu":
                    string comname = ParseCOMName(url);
                    var dtu = new AMWD.Modbus.Serial.Client.ModbusClient(comname, mlog);
                    ParseDtuParam(url, dtu);
                    client = dtu;
                    break;
                case "tcp":
                    client = new AMWD.Modbus.Tcp.Client.ModbusClient(url.Host, url.Port, mlog);
                    break;
                case "d2t":
                    client = new AMWD.Modbus.SerialOverTCP.Client.ModbusClient(url.Host, url.Port, mlog);
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
                            if (Enum.TryParse(query.Get(key), true, out int _DataBits))
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