namespace IoTEdge.Domain;

/// <summary>
/// 上传传输适配器。
/// 负责把上传包发送到指定协议和目标端点。
/// </summary>
public interface IUploadTransport
{
    /// <summary>
    /// 获取当前传输适配器支持的协议类型。
    /// </summary>
    UploadProtocol Protocol { get; }

    /// <summary>
    /// 发送一条上传消息。
    /// </summary>
    Task UploadAsync(UploadChannel channel, UploadEnvelope envelope, CancellationToken cancellationToken);
}
