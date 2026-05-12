namespace IoTEdge.Domain;

/// <summary>
/// 上传传输注册表。
/// 根据协议类型获取对应的上传适配器。
/// </summary>
public interface IUploadTransportRegistry
{
    /// <summary>
    /// 根据上传协议获取已注册的传输适配器。
    /// </summary>
    IUploadTransport GetRequiredTransport(UploadProtocol protocol);
}
