namespace IoTSharp.Edge.Infrastructure.Drivers;

internal abstract class DeviceDriverBase : IDeviceDriver
{
    public abstract DriverMetadata Metadata { get; }

    public virtual Task<ConnectionTestResult> TestConnectionAsync(DriverConnectionContext context, CancellationToken cancellationToken)
        => Task.FromResult(new ConnectionTestResult(true));

    public virtual Task<AddressValidationResult> ValidateAddressAsync(DriverReadRequest request, CancellationToken cancellationToken)
        => Task.FromResult(string.IsNullOrWhiteSpace(request.Address)
            ? new AddressValidationResult(false, "地址为必填项。")
            : new AddressValidationResult(true));

    public abstract Task<DriverReadResult> ReadAsync(DriverConnectionContext context, DriverReadRequest request, CancellationToken cancellationToken);
    public abstract Task<DriverWriteResult> WriteAsync(DriverConnectionContext context, DriverWriteRequest request, CancellationToken cancellationToken);

    public virtual async Task<IReadOnlyCollection<DriverReadResult>> ReadBatchAsync(DriverConnectionContext context, DriverBatchReadRequest request, CancellationToken cancellationToken)
    {
        var results = new List<DriverReadResult>(request.Requests.Count);
        foreach (var item in request.Requests)
        {
            results.Add(await ReadAsync(context, item, cancellationToken));
        }

        return results;
    }

    public virtual async Task<IReadOnlyCollection<DriverWriteResult>> WriteBatchAsync(DriverConnectionContext context, DriverBatchWriteRequest request, CancellationToken cancellationToken)
    {
        var results = new List<DriverWriteResult>(request.Requests.Count);
        foreach (var item in request.Requests)
        {
            results.Add(await WriteAsync(context, item, cancellationToken));
        }

        return results;
    }

    protected static string Required(IReadOnlyDictionary<string, string?> values, string key)
        => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"连接参数“{key}”为必填项。");

    protected static int Int(IReadOnlyDictionary<string, string?> values, string key, int defaultValue)
        => values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;

    protected static byte Byte(IReadOnlyDictionary<string, string?> values, string key, byte defaultValue)
        => values.TryGetValue(key, out var value) && byte.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;

    protected static bool Boolean(IReadOnlyDictionary<string, string?> values, string key, bool defaultValue)
        => values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;

    protected static Encoding ResolveEncoding(IReadOnlyDictionary<string, string?> values)
    {
        if (values.TryGetValue("codePage", out var codePageValue) && int.TryParse(codePageValue, out var codePage))
        {
            return Encoding.GetEncoding(codePage);
        }

        if (values.TryGetValue("encoding", out var encodingName) && !string.IsNullOrWhiteSpace(encodingName))
        {
            return Encoding.GetEncoding(encodingName);
        }

        return Encoding.UTF8;
    }

    protected static EndianFormat ResolveEndian(IReadOnlyDictionary<string, string?> values)
        => values.TryGetValue("endianFormat", out var value) && Enum.TryParse<EndianFormat>(value, true, out var endian)
            ? endian
            : EndianFormat.ABCD;

    protected static DriverReadResult ToReadResult<T>(string address, Result<T> result)
        => result.IsSucceed
            ? new DriverReadResult(address, result.Value, result.Value, DateTimeOffset.UtcNow, QualityStatus.Good)
            : new DriverReadResult(address, null, null, DateTimeOffset.UtcNow, QualityStatus.Bad, result.Err);

    protected static DriverWriteResult ToWriteResult(string address, object? value, Result result)
        => result.IsSucceed
            ? new DriverWriteResult(address, value, DateTimeOffset.UtcNow, QualityStatus.Good)
            : new DriverWriteResult(address, value, DateTimeOffset.UtcNow, QualityStatus.Bad, result.Err);

    protected static ConnectionTestResult ToConnectionResult(Result result)
        => result.IsSucceed ? new ConnectionTestResult(true) : new ConnectionTestResult(false, result.Err);

    protected static DriverReadResult FailedRead(string address, Exception exception)
        => new(address, null, null, DateTimeOffset.UtcNow, QualityStatus.Bad, exception.Message);

    protected static DriverWriteResult FailedWrite(string address, object? value, Exception exception)
        => new(address, value, DateTimeOffset.UtcNow, QualityStatus.Bad, exception.Message);
}

public sealed class DeviceDriverRegistry : IDeviceDriverRegistry
{
    private readonly Dictionary<string, IDeviceDriver> _drivers;

    public DeviceDriverRegistry(IEnumerable<IDeviceDriver> drivers)
    {
        _drivers = drivers.ToDictionary(driver => driver.Metadata.Code, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<DriverMetadata> GetMetadata() => _drivers.Values.Select(x => x.Metadata).OrderBy(x => x.DisplayName).ToArray();

        public IDeviceDriver GetRequiredDriver(string code)
        => _drivers.TryGetValue(code, out var driver)
            ? driver
            : throw new KeyNotFoundException($"驱动“{code}”未注册。");
}
