using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace IoTSharp.Gateway.Modbus.Data
{
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum FunCode
    {
        [Display(Name = "读取线圈")]
        ReadCoils =1,
        [Display(Name = "读取离散量输入")]
        ReadDiscreteInputs =2,
        [Display(Name = "读取保持寄存器")]
        ReadMultipleHoldingRegisters = 3,
        [Display(Name = "读取输入寄存器")]
        ReadInputRegisters =4,
        [Display(Name = "写入单个线圈")]
        WriteSingleCoil =5,
        [Display(Name = "写入单个保持寄存器")]
        WriteSingleHoldingRegister =6,
        [Display(Name = "写入多个线圈")]
        WriteMultipleCoils =15,
        [Display(Name = "写入多个保持寄存器")]
        WriteMultipleHoldingRegisters =16

    }
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    [JsonConverter(typeof(StringEnumConverter))]
 
    public enum DataType
    {
        [Display(Name = "逻辑")]
        Boolean,
        [Display(Name = "字符串")]
        String,
        [Display(Name = "整数")]
        Long,
        [Display(Name = "浮点数")]
        Double,
        [Display(Name = "时间")]
        DateTime
    }
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum DataCatalog
    {
        [Display( Name ="属性数据")]
        AttributeData,
        [Display(Name = "遥测数据")]
        TelemetryData,
    }
}
