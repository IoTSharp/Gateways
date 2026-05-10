using System.Text.Json.Serialization;

namespace IoTSharp.Gateways.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DriverType
{
    Modbus,
    SiemensS7,
    Mitsubishi,
    OmronFins,
    AllenBradley,
    OpcUa,
    OpcDa,
    MtCnc,
    FanucCnc
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GatewayDataType
{
    Boolean,
    Byte,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Float,
    Double,
    String
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PointAccessMode
{
    Read,
    Write,
    ReadWrite
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UploadProtocol
{
    Http,
    IotSharpMqtt,
    IotSharpDeviceHttp
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransformationKind
{
    Passthrough = 0,
    Scale = 1,
    Offset = 2,
    Cast = 3,
    BitExtract = 4,
    EnumMap = 5,
    Expression = 6
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QualityStatus
{
    Good,
    Bad,
    Unknown
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WriteCommandStatus
{
    Pending,
    Succeeded,
    Failed
}

public sealed record ConnectionSettingDefinition(
    string Key,
    string Label,
    string ValueType,
    bool Required,
    string Description,
    IReadOnlyCollection<string>? Options = null);

public sealed record DriverMetadata(
    string Code,
    DriverType DriverType,
    string DisplayName,
    string Description,
    bool SupportsRead,
    bool SupportsWrite,
    bool SupportsBatchRead,
    bool SupportsBatchWrite,
    IReadOnlyCollection<ConnectionSettingDefinition> ConnectionSettings,
    string RiskLevel = "normal");

public sealed class DriverDefinition
{
    public string Code { get; set; } = string.Empty;
    public DriverType DriverType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool SupportsRead { get; set; }
    public bool SupportsWrite { get; set; }
    public bool SupportsBatchRead { get; set; }
    public bool SupportsBatchWrite { get; set; }
    public IReadOnlyCollection<ConnectionSettingDefinition> ConnectionSettings { get; set; } = Array.Empty<ConnectionSettingDefinition>();
    public string RiskLevel { get; set; } = "normal";
}

public sealed class GatewayChannel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DriverCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ConnectionJson { get; set; } = "{}";
    public bool Enabled { get; set; } = true;
}

public sealed class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChannelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public int PollingIntervalSeconds { get; set; } = 30;
    public string SettingsJson { get; set; } = "{}";
    public bool Enabled { get; set; } = true;
}

public sealed class Point
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public GatewayDataType DataType { get; set; }
    public PointAccessMode AccessMode { get; set; }
    public ushort Length { get; set; } = 1;
    public string SettingsJson { get; set; } = "{}";
    public bool Enabled { get; set; } = true;
}

public sealed class PollingTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int IntervalSeconds { get; set; } = 30;
    public string PointIdsJson { get; set; } = "[]";
    public bool TriggerOnChange { get; set; }
    public bool BatchRead { get; set; } = true;
    public bool Enabled { get; set; } = true;
}

public sealed class TransformRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PointId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TransformationKind Kind { get; set; }
    public int SortOrder { get; set; }
    public string ArgumentsJson { get; set; } = "{}";
    public bool Enabled { get; set; } = true;
}

public sealed class UploadChannel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public UploadProtocol Protocol { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string SettingsJson { get; set; } = "{}";
    public int BatchSize { get; set; } = 1;
    public bool BufferingEnabled { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class UploadRoute
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PointId { get; set; }
    public Guid UploadChannelId { get; set; }
    public string PayloadTemplate { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

public sealed class WriteCommand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public Guid? PointId { get; set; }
    public string Address { get; set; } = string.Empty;
    public string ValueJson { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public WriteCommandStatus Status { get; set; } = WriteCommandStatus.Pending;
    public string ErrorMessage { get; set; } = string.Empty;
}

public sealed record DriverConnectionContext(
    string DriverCode,
    IReadOnlyDictionary<string, string?> Settings);

public sealed record DriverReadRequest(
    string Address,
    GatewayDataType DataType,
    ushort Length = 1,
    IReadOnlyDictionary<string, string?>? Settings = null);

public sealed record DriverBatchReadRequest(
    IReadOnlyCollection<DriverReadRequest> Requests);

public sealed record DriverWriteRequest(
    string Address,
    GatewayDataType DataType,
    object? Value,
    ushort Length = 1,
    IReadOnlyDictionary<string, string?>? Settings = null);

public sealed record DriverBatchWriteRequest(
    IReadOnlyCollection<DriverWriteRequest> Requests);

public sealed record DriverReadResult(
    string Address,
    object? RawValue,
    object? TransformedValue,
    DateTimeOffset Timestamp,
    QualityStatus Quality,
    string? ErrorMessage = null);

public sealed record DriverWriteResult(
    string Address,
    object? Value,
    DateTimeOffset Timestamp,
    QualityStatus Quality,
    string? ErrorMessage = null);

public sealed record ConnectionTestResult(bool Success, string? ErrorMessage = null);

public sealed record AddressValidationResult(bool IsValid, string? ErrorMessage = null);

public sealed record UploadEnvelope(
    string DeviceName,
    string PointName,
    object? RawValue,
    object? Value,
    DateTimeOffset Timestamp,
    QualityStatus Quality,
    string Target,
    string PayloadTemplate);

public interface IDeviceDriver
{
    DriverMetadata Metadata { get; }
    Task<ConnectionTestResult> TestConnectionAsync(DriverConnectionContext context, CancellationToken cancellationToken);
    Task<AddressValidationResult> ValidateAddressAsync(DriverReadRequest request, CancellationToken cancellationToken);
    Task<DriverReadResult> ReadAsync(DriverConnectionContext context, DriverReadRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<DriverReadResult>> ReadBatchAsync(DriverConnectionContext context, DriverBatchReadRequest request, CancellationToken cancellationToken);
    Task<DriverWriteResult> WriteAsync(DriverConnectionContext context, DriverWriteRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<DriverWriteResult>> WriteBatchAsync(DriverConnectionContext context, DriverBatchWriteRequest request, CancellationToken cancellationToken);
}

public interface IDeviceDriverRegistry
{
    IReadOnlyCollection<DriverMetadata> GetMetadata();
    IDeviceDriver GetRequiredDriver(string code);
}

public interface IUploadTransport
{
    UploadProtocol Protocol { get; }
    Task UploadAsync(UploadChannel channel, UploadEnvelope envelope, CancellationToken cancellationToken);
}

public interface IUploadTransportRegistry
{
    IUploadTransport GetRequiredTransport(UploadProtocol protocol);
}
