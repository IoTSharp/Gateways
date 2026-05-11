namespace IoTSharp.Edge.Domain;

/// <summary>
/// 上传协议描述。
/// 用于向前端和配置层暴露可用的上传协议、分组和连接字段。
/// </summary>
public sealed record UploadProtocolDescriptor(
    string Code,
    string DisplayName,
    string Category,
    string Description,
    string Lifecycle,
    IReadOnlyCollection<ConnectionSettingDefinition> ConnectionSettings);

/// <summary>
/// 上传协议目录。
/// 用于获取当前宿主已注册的上传协议能力。
/// </summary>
public interface IUploadProtocolCatalog
{
    /// <summary>
    /// 获取全部上传协议描述。
    /// </summary>
    IReadOnlyCollection<UploadProtocolDescriptor> GetProtocols();
}
