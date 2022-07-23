using System;

namespace IoTSharp.MqttSdk
{
    public class RpcRequest
    {
        public string DeviceId { get; set; }
        public string Method { get; set; }
        public string RequestId { get; set; }
        public string Params { get; set; }
    }
    public class RpcResponse
    {
        public string DeviceId { get; set; }
        public string Method { get; set; }
        public string ResponseId { get; set; }
        public object Data { get; set; }
    }
    public class AttributeResponse
    {
        public string Id { get; set; }
        public string DeviceName { get; set; }
        public string KeyName { get; set; }

        public string Data { get; set; }
    }

    public class MqttSettings
    {
        public string MqttBroker { get; set; }
        public string DeviceId { get;   set; }
    }
}
