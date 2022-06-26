using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace IoTSharp.Gateway.Modbus.Data
{
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum FunCode
    {
        ReadCoils =1,
        ReadDiscreteInputs=2,
        ReadMultipleHoldingRegisters = 3,
        ReadInputRegisters=4,
        WriteSingleCoil=5,
        WriteSingleHoldingRegister=6,
        WriteMultipleCoils=15,
        WriteMultipleHoldingRegisters=16

    }
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum DataType
    {
        Boolean,
        String,
        Long,
        Double,
        DateTime
    }
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum DataCatalog
    {
        AttributeData,
        TelemetryData,
    }
}
