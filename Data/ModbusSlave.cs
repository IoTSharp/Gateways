using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace IoTSharp.Gateways.Data
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
        // DTU over TCP:   d2t://www.host.com:602
        /// </example>
        [Required]
        [DisplayName("从机地址")]
        [Description("Linux下串口格式 dtu://dev.ttyS0:115200  ， Windows下串口格式 dtu://COM1:115200 TCP格式 d2t://www.host.com:602")]
        public Uri Slave { get; set; }

        /// <summary>
        /// 连接超时和读取写入超时 按秒
        /// </summary>
        [DisplayName("连接和读写超时")]
        public int TimeOut { get; set; }

        [Required]
        [DisplayName("设备名称")]
        public string DeviceName { get; set; }

        /// <summary>
        /// 如果为空， 则直接使用 DeviceName , 
        /// 否则， 使用 PointMapping 里面的 映射ID作为设备名称。 {Name}-{ID} , 前提是PointMappings中要有， 如果没有， 则为空。 
        /// </summary>
        [DisplayName("设备名称格式")]
        public string? DeviceNameFormat { get; set; }

        /// <summary>
        /// 时间间隔 秒为单位。
        /// </summary>
        [DisplayName("采集间隔")]
        public float TimeInterval { get; set; }

        public List<PointMapping>? PointMappings { get; set; } = new List<PointMapping>();
    }
}
