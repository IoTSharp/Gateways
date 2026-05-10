namespace IoTSharp.Edge.Infrastructure.Drivers;

internal sealed class UnsupportedDriver : DeviceDriverBase
{
    private readonly string _message;

    public UnsupportedDriver(DriverMetadata metadata, string message)
    {
        Metadata = metadata;
        _message = message;
    }

    public override DriverMetadata Metadata { get; }

    public override Task<ConnectionTestResult> TestConnectionAsync(DriverConnectionContext context, CancellationToken cancellationToken)
        => Task.FromResult(new ConnectionTestResult(false, _message));

    public override Task<DriverReadResult> ReadAsync(DriverConnectionContext context, DriverReadRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new DriverReadResult(request.Address, null, null, DateTimeOffset.UtcNow, QualityStatus.Bad, _message));

    public override Task<DriverWriteResult> WriteAsync(DriverConnectionContext context, DriverWriteRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new DriverWriteResult(request.Address, request.Value, DateTimeOffset.UtcNow, QualityStatus.Bad, _message));
}
