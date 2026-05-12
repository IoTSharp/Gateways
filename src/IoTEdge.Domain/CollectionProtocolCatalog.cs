namespace IoTEdge.Domain;

/// <summary>
/// 采集协议描述。
/// 用于向前端和配置层暴露可用协议、默认分类和连接字段。
/// </summary>
public sealed record CollectionProtocolDescriptor(
    string Code,
    string ContractProtocol,
    DriverType DriverType,
    string DisplayName,
    string Category,
    string Description,
    string Lifecycle,
    bool SupportsRead,
    bool SupportsWrite,
    bool SupportsBatchRead,
    bool SupportsBatchWrite,
    string RiskLevel,
    IReadOnlyCollection<ConnectionSettingDefinition> ConnectionSettings);

/// <summary>
/// 采集协议目录。
/// 用于获取当前宿主已注册的采集协议能力。
/// </summary>
public interface ICollectionProtocolCatalog
{
    /// <summary>
    /// 获取全部采集协议描述。
    /// </summary>
    IReadOnlyCollection<CollectionProtocolDescriptor> GetProtocols();
}
