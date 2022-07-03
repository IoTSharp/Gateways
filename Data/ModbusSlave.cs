using System.ComponentModel.DataAnnotations;

namespace IoTSharp.Gateway.Modbus.Data
{
    public class ModbusSlave
    {

        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <example>
        /// DTU:  dtu://dev.ttyS0:115200
        /// DTU:  dtu://COM1:115200
        /// TCP:  tcp://www.host.com:602
        // DTU over TCP:   DTUoverTCP://www.host.com:602
        /// </example>
        [Required]
        public Uri Slave { get; set; }

        /// <summary>
        /// 连接超时和读取写入超时 按秒
        /// </summary>
        public int TimeOut { get; set; }

        [Required]
        public string DeviceName { get; set; }

        /// <summary>
        /// 如果为空， 则直接使用 DeviceName , 
        /// 否则， 使用 PointMapping 里面的 映射ID作为设备名称。 {Name}-{ID} , 前提是PointMappings中要有， 如果没有， 则为空。 
        /// </summary>
        public string? DeviceNameFormat { get; set; }

        public List<PointMapping>? PointMappings { get; set; } = new List<PointMapping>();
    }
}
