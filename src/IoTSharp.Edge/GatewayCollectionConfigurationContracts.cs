using System.Text.Json;
using System.Text.Json.Serialization;

namespace IoTSharp.Edge;

[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum GatewayCollectionProtocolType
{
    Unknown = 0,
    Modbus = 1,
    OpcUa = 2,
    Bacnet = 3,
    IEC104 = 4,
    Mqtt = 5,
    OpcDa = 6,
    MtCnc = 7,
    FanucCnc = 8,
    Custom = 99
}

[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum GatewayCollectionTargetType
{
    Telemetry = 1,
    Attribute = 2,
    AlarmInput = 3,
    CommandFeedback = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum GatewayCollectionValueType
{
    Boolean = 1,
    Int32 = 2,
    Int64 = 3,
    Double = 4,
    Decimal = 5,
    String = 6,
    Enum = 7,
    Json = 8
}

[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum GatewayCollectionTransformType
{
    Scale = 1,
    Offset = 2,
    Expression = 3,
    EnumMap = 4,
    BitExtract = 5,
    WordSwap = 6,
    ByteSwap = 7,
    Clamp = 8,
    DefaultOnError = 9
}

[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum GatewayReportTriggerType
{
    Always = 1,
    OnChange = 2,
    Deadband = 3,
    Interval = 4,
    QualityChange = 5
}

internal sealed record EdgeCollectionConfigurationContract
{
    public string ContractVersion { get; init; } = "edge-collection-v1";
    public Guid EdgeNodeId { get; init; }
    public int Version { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
    public IReadOnlyList<CollectionTaskContract> Tasks { get; init; } = [];
}

internal sealed record CollectionTaskContract
{
    public Guid Id { get; init; }
    public string TaskKey { get; init; } = string.Empty;
    public GatewayCollectionProtocolType Protocol { get; init; } = GatewayCollectionProtocolType.Unknown;
    public int Version { get; init; }
    public Guid EdgeNodeId { get; init; }
    public CollectionConnectionContract Connection { get; init; } = new();
    public IReadOnlyList<CollectionDeviceContract> Devices { get; init; } = [];
    public ReportPolicyContract ReportPolicy { get; init; } = new();
}

internal sealed record CollectionConnectionContract
{
    public string ConnectionKey { get; init; } = string.Empty;
    public string ConnectionName { get; init; } = string.Empty;
    public GatewayCollectionProtocolType Protocol { get; init; } = GatewayCollectionProtocolType.Unknown;
    public string Transport { get; init; } = string.Empty;
    public string? Host { get; init; }
    public int? Port { get; init; }
    public string? SerialPort { get; init; }
    public int TimeoutMs { get; init; } = 3000;
    public int RetryCount { get; init; } = 3;
    public JsonElement? ProtocolOptions { get; init; }
}

internal sealed record CollectionDeviceContract
{
    public string DeviceKey { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
    public string? ExternalKey { get; init; }
    public JsonElement? ProtocolOptions { get; init; }
    public IReadOnlyList<CollectionPointContract> Points { get; init; } = [];
}

internal sealed record CollectionPointContract
{
    public string PointKey { get; init; } = string.Empty;
    public string PointName { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string RawValueType { get; init; } = string.Empty;
    public int Length { get; init; }
    public PollingPolicyContract Polling { get; init; } = new();
    public IReadOnlyList<ValueTransformContract> Transforms { get; init; } = [];
    public PlatformMappingContract Mapping { get; init; } = new();
    public JsonElement? ProtocolOptions { get; init; }
}

internal sealed record PollingPolicyContract
{
    public int ReadPeriodMs { get; init; } = 5000;
    public string? Group { get; init; }
}

internal sealed record ValueTransformContract
{
    public GatewayCollectionTransformType TransformType { get; init; } = GatewayCollectionTransformType.Scale;
    public int Order { get; init; }
    public JsonElement? Parameters { get; init; }
}

internal sealed record PlatformMappingContract
{
    public GatewayCollectionTargetType TargetType { get; init; } = GatewayCollectionTargetType.Telemetry;
    public string TargetName { get; init; } = string.Empty;
    public GatewayCollectionValueType ValueType { get; init; } = GatewayCollectionValueType.Double;
    public string? DisplayName { get; init; }
    public string? Unit { get; init; }
    public string? Group { get; init; }
}

internal sealed record ReportPolicyContract
{
    public GatewayReportTriggerType DefaultTrigger { get; init; } = GatewayReportTriggerType.OnChange;
    public double? Deadband { get; init; }
    public bool IncludeQuality { get; init; } = true;
    public bool IncludeTimestamp { get; init; } = true;
}
