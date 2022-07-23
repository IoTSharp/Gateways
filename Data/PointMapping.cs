using System.ComponentModel.DataAnnotations;

namespace IoTSharp.Gateway.Modbus.Data
{
    public class PointMapping
    {

        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// 从机站代码
        /// </summary>
        public byte SlaveCode {get;set;}
        [Required]
        public string DataName { get; set; }

        /// <summary>
        /// 如果是DataType为 Long , 如果是Length一个寄存器那么就是int16 ,  如果是2个寄存器 就是 in32, 如果是4个寄存器 int64 ， 4字节就是int64 
        /// 如果是DataType为DateTime   如果Lenght为 4 ， 就是 秒时间戳， 如果超过4 字节， 则需指定时间格式 yyyyMMddHHmmss 
        /// 如果是DataType为String， 则需指定编码格式CodePage， 
        /// 如果是DataType为Double， 如果Length是2个寄存器就是4个字节， 则是float， 如果是4个寄存器那就是8个字节， 就是double， 
        /// </summary>
        
        public DataType DataType { get; set; }
        /// <summary>
        /// 数据分类
        /// </summary>
        public DataCatalog DataCatalog { get; set; }

        public FunCode FunCode { get; set; }

        public ushort Address { get; set; }

        /// <summary>
        /// 如果是读取寄存器， 那么一个寄存器就是2字节， 如果是线圈， 那就是 1位。 一个字节为 8位。 
        /// </summary>
        public ushort Length { get; set; }

        /// <summary>
        /// 默认不转换  
        /// </summary>
        public string? DateTimeFormat { get; set; }
        /// <summary>
        /// 默认 65001 ， 简体中文 936 繁体 950
        /// </summary>
        public int CodePage { get; set; } = 65001;

       public ModbusSlave? Owner { get; set; }

    }
}
