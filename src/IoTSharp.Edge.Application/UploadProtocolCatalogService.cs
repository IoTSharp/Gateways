using IoTSharp.Edge.Domain;

namespace IoTSharp.Edge.Application;

/// <summary>
/// 上传协议目录服务。
/// 将上传协议目录转换为前端可直接消费的上传协议描述。
/// </summary>
public sealed class UploadProtocolCatalogService : IUploadProtocolCatalog
{
    private static readonly IReadOnlyCollection<UploadProtocolDescriptor> Protocols =
        [
            new(
                "IoTSharp",
                "IoTSharp",
                "平台",
                "IoTSharp 平台上传目标，支持遥测与属性通道自动展开。",
                "ready",
                [
                    new ConnectionSettingDefinition("endpoint", "端点", "text", true, "IoTSharp 平台基础地址或完整上报地址。"),
                    new ConnectionSettingDefinition("token", "访问令牌", "password", true, "IoTSharp Edge 访问令牌或设备令牌。"),
                    new ConnectionSettingDefinition("site", "站点", "text", false, "可选的站点标识，会作为上传标签写入。")
                ]),
            new(
                "ThingsBoard",
                "ThingsBoard",
                "平台",
                "ThingsBoard 上传目标，支持遥测与属性通道自动展开。",
                "ready",
                [
                    new ConnectionSettingDefinition("endpoint", "端点", "text", true, "ThingsBoard 平台基础地址或完整上报地址。"),
                    new ConnectionSettingDefinition("token", "访问令牌", "password", true, "ThingsBoard 设备访问令牌。"),
                    new ConnectionSettingDefinition("site", "站点", "text", false, "可选的站点标识，会作为上传标签写入。")
                ]),
            new(
                "SonnetDb",
                "SonnetDB",
                "时序数据库",
                "SonnetDB 时序写入目标，适合边缘点位的高频落库。",
                "ready",
                [
                    new ConnectionSettingDefinition("endpoint", "端点", "text", true, "SonnetDB 服务地址，或以 sonnetdb+ 开头的完整连接串。"),
                    new ConnectionSettingDefinition("connectionString", "连接串", "text", false, "完整 ADO.NET 连接串。"),
                    new ConnectionSettingDefinition("database", "数据库", "text", false, "写入数据库名称。"),
                    new ConnectionSettingDefinition("token", "访问令牌", "password", false, "SonnetDB 访问令牌。"),
                    new ConnectionSettingDefinition("measurement", "测点集", "text", false, "默认测点集名称。"),
                    new ConnectionSettingDefinition("field", "字段", "text", false, "默认字段名称。"),
                    new ConnectionSettingDefinition("site", "站点", "text", false, "可选站点标签。"),
                    new ConnectionSettingDefinition("includeRawValue", "包含原始值", "boolean", false, "同时写入原始值字段。"),
                    new ConnectionSettingDefinition("rawField", "原始值字段", "text", false, "原始值写入字段名。"),
                    new ConnectionSettingDefinition("flush", "刷新模式", "select", false, "写入刷新模式。", ["async", "sync"])
                ]),
            new(
                "InfluxDb",
                "InfluxDB",
                "时序数据库",
                "InfluxDB line protocol 写入目标，兼容 v2 bucket 与 v1 database 场景。",
                "ready",
                [
                    new ConnectionSettingDefinition("endpoint", "端点", "text", true, "InfluxDB 服务基础地址或完整写入地址。"),
                    new ConnectionSettingDefinition("token", "访问令牌", "password", false, "InfluxDB 写入令牌。"),
                    new ConnectionSettingDefinition("org", "组织", "text", false, "InfluxDB 组织名称。"),
                    new ConnectionSettingDefinition("bucket", "Bucket", "text", false, "InfluxDB v2 bucket。"),
                    new ConnectionSettingDefinition("database", "数据库", "text", false, "InfluxDB v1 database。"),
                    new ConnectionSettingDefinition("measurement", "测量", "text", false, "默认测量名称。"),
                    new ConnectionSettingDefinition("field", "字段", "text", false, "默认字段名称。"),
                    new ConnectionSettingDefinition("precision", "精度", "select", false, "时间精度。", ["ns", "us", "ms", "s"]),
                    new ConnectionSettingDefinition("site", "站点", "text", false, "可选站点标签。"),
                    new ConnectionSettingDefinition("includeRawValue", "包含原始值", "boolean", false, "同时写入原始值字段。"),
                    new ConnectionSettingDefinition("rawField", "原始值字段", "text", false, "原始值写入字段名。")
                ])
        ];

    public IReadOnlyCollection<UploadProtocolDescriptor> GetProtocols()
        => Protocols
            .OrderBy(descriptor => ResolveCategoryOrder(descriptor.Category))
            .ThenBy(descriptor => ResolveProtocolOrder(descriptor.Code))
            .ThenBy(descriptor => descriptor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static int ResolveCategoryOrder(string category)
        => category switch
        {
            "平台" => 0,
            "时序数据库" => 1,
            _ => 99
        };

    private static int ResolveProtocolOrder(string code)
        => code.ToLowerInvariant() switch
        {
            "iotsharp" => 0,
            "thingsboard" => 1,
            "sonnetdb" => 2,
            "influxdb" => 3,
            _ => 99
        };
}
