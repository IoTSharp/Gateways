using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SQLitePCL;
using IoTSharp.Gateways.Application;
using IoTSharp.Gateways.Domain;

namespace IoTSharp.Gateways.Infrastructure.Persistence;

public sealed class GatewayStorageOptions
{
    public string ConnectionString { get; set; } = "Data Source=gateways.db";
}

public interface IGatewaySchemaInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}

public interface IGatewayDbConnectionFactory
{
    Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}

public sealed class SqliteGatewayConnectionFactory : IGatewayDbConnectionFactory
{
    private readonly GatewayStorageOptions _options;

    public SqliteGatewayConnectionFactory(IOptions<GatewayStorageOptions> options)
    {
        _options = options.Value;
        Batteries_V2.Init();
    }

    public async Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

public sealed class SqliteGatewaySchemaInitializer : IGatewaySchemaInitializer
{
    private readonly IGatewayDbConnectionFactory _connectionFactory;

    public SqliteGatewaySchemaInitializer(IGatewayDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
        CREATE TABLE IF NOT EXISTS GatewayChannels (
            Id TEXT PRIMARY KEY,
            DriverCode TEXT NOT NULL,
            Name TEXT NOT NULL,
            Description TEXT NOT NULL,
            ConnectionJson TEXT NOT NULL,
            Enabled INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Devices (
            Id TEXT PRIMARY KEY,
            ChannelId TEXT NOT NULL,
            Name TEXT NOT NULL,
            ExternalId TEXT NOT NULL,
            PollingIntervalSeconds INTEGER NOT NULL,
            SettingsJson TEXT NOT NULL,
            Enabled INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Points (
            Id TEXT PRIMARY KEY,
            DeviceId TEXT NOT NULL,
            Name TEXT NOT NULL,
            Address TEXT NOT NULL,
            DataType INTEGER NOT NULL,
            AccessMode INTEGER NOT NULL,
            Length INTEGER NOT NULL,
            SettingsJson TEXT NOT NULL,
            Enabled INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS PollingTasks (
            Id TEXT PRIMARY KEY,
            DeviceId TEXT NOT NULL,
            Name TEXT NOT NULL,
            IntervalSeconds INTEGER NOT NULL,
            TriggerOnChange INTEGER NOT NULL,
            BatchRead INTEGER NOT NULL,
            Enabled INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS TransformRules (
            Id TEXT PRIMARY KEY,
            PointId TEXT NOT NULL,
            Name TEXT NOT NULL,
            Kind INTEGER NOT NULL,
            SortOrder INTEGER NOT NULL,
            ArgumentsJson TEXT NOT NULL,
            Enabled INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS UploadChannels (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            Protocol INTEGER NOT NULL,
            Endpoint TEXT NOT NULL,
            SettingsJson TEXT NOT NULL,
            BatchSize INTEGER NOT NULL,
            BufferingEnabled INTEGER NOT NULL,
            Enabled INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS UploadRoutes (
            Id TEXT PRIMARY KEY,
            PointId TEXT NOT NULL,
            UploadChannelId TEXT NOT NULL,
            PayloadTemplate TEXT NOT NULL,
            Target TEXT NOT NULL,
            Enabled INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS WriteCommands (
            Id TEXT PRIMARY KEY,
            DeviceId TEXT NOT NULL,
            PointId TEXT NULL,
            Address TEXT NOT NULL,
            ValueJson TEXT NOT NULL,
            RequestedAtUtc TEXT NOT NULL,
            Status INTEGER NOT NULL,
            ErrorMessage TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_Devices_ChannelId ON Devices(ChannelId);
        CREATE INDEX IF NOT EXISTS IX_Points_DeviceId ON Points(DeviceId);
        CREATE INDEX IF NOT EXISTS IX_PollingTasks_DeviceId ON PollingTasks(DeviceId);
        CREATE INDEX IF NOT EXISTS IX_TransformRules_PointId ON TransformRules(PointId);
        CREATE INDEX IF NOT EXISTS IX_UploadRoutes_PointId ON UploadRoutes(PointId);
        """;

        using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }
}

public sealed class SqliteGatewayRepository : IGatewayRepository
{
    private readonly IGatewayDbConnectionFactory _connectionFactory;

    public SqliteGatewayRepository(IGatewayDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<IReadOnlyCollection<GatewayChannel>> GetChannelsAsync(CancellationToken cancellationToken)
        => QueryListAsync<GatewayChannel>("SELECT * FROM GatewayChannels ORDER BY Name;", cancellationToken);

    public Task<GatewayChannel?> GetChannelAsync(Guid id, CancellationToken cancellationToken)
        => QuerySingleAsync<GatewayChannel>("SELECT * FROM GatewayChannels WHERE Id = @Id;", new { Id = id }, cancellationToken);

    public Task SaveChannelAsync(GatewayChannel channel, CancellationToken cancellationToken)
        => ExecuteAsync(
            "REPLACE INTO GatewayChannels (Id, DriverCode, Name, Description, ConnectionJson, Enabled) VALUES (@Id, @DriverCode, @Name, @Description, @ConnectionJson, @Enabled);",
            channel,
            cancellationToken);

    public Task DeleteChannelAsync(Guid id, CancellationToken cancellationToken)
        => ExecuteAsync("DELETE FROM GatewayChannels WHERE Id = @Id;", new { Id = id }, cancellationToken);

    public Task<IReadOnlyCollection<Device>> GetDevicesAsync(CancellationToken cancellationToken)
        => QueryListAsync<Device>("SELECT * FROM Devices ORDER BY Name;", cancellationToken);

    public Task<Device?> GetDeviceAsync(Guid id, CancellationToken cancellationToken)
        => QuerySingleAsync<Device>("SELECT * FROM Devices WHERE Id = @Id;", new { Id = id }, cancellationToken);

    public Task<IReadOnlyCollection<Device>> GetDevicesByChannelAsync(Guid channelId, CancellationToken cancellationToken)
        => QueryListAsync<Device>("SELECT * FROM Devices WHERE ChannelId = @ChannelId ORDER BY Name;", new { ChannelId = channelId }, cancellationToken);

    public Task SaveDeviceAsync(Device device, CancellationToken cancellationToken)
        => ExecuteAsync(
            "REPLACE INTO Devices (Id, ChannelId, Name, ExternalId, PollingIntervalSeconds, SettingsJson, Enabled) VALUES (@Id, @ChannelId, @Name, @ExternalId, @PollingIntervalSeconds, @SettingsJson, @Enabled);",
            device,
            cancellationToken);

    public Task DeleteDeviceAsync(Guid id, CancellationToken cancellationToken)
        => ExecuteAsync("DELETE FROM Devices WHERE Id = @Id;", new { Id = id }, cancellationToken);

    public Task<IReadOnlyCollection<Point>> GetPointsAsync(CancellationToken cancellationToken)
        => QueryListAsync<Point>("SELECT * FROM Points ORDER BY Name;", cancellationToken);

    public Task<Point?> GetPointAsync(Guid id, CancellationToken cancellationToken)
        => QuerySingleAsync<Point>("SELECT * FROM Points WHERE Id = @Id;", new { Id = id }, cancellationToken);

    public Task<IReadOnlyCollection<Point>> GetPointsByDeviceAsync(Guid deviceId, CancellationToken cancellationToken)
        => QueryListAsync<Point>("SELECT * FROM Points WHERE DeviceId = @DeviceId ORDER BY Name;", new { DeviceId = deviceId }, cancellationToken);

    public Task SavePointAsync(Point point, CancellationToken cancellationToken)
        => ExecuteAsync(
            "REPLACE INTO Points (Id, DeviceId, Name, Address, DataType, AccessMode, Length, SettingsJson, Enabled) VALUES (@Id, @DeviceId, @Name, @Address, @DataType, @AccessMode, @Length, @SettingsJson, @Enabled);",
            point,
            cancellationToken);

    public Task DeletePointAsync(Guid id, CancellationToken cancellationToken)
        => ExecuteAsync("DELETE FROM Points WHERE Id = @Id;", new { Id = id }, cancellationToken);

    public Task<IReadOnlyCollection<PollingTask>> GetPollingTasksAsync(CancellationToken cancellationToken)
        => QueryListAsync<PollingTask>("SELECT * FROM PollingTasks ORDER BY Name;", cancellationToken);

    public Task<PollingTask?> GetPollingTaskAsync(Guid id, CancellationToken cancellationToken)
        => QuerySingleAsync<PollingTask>("SELECT * FROM PollingTasks WHERE Id = @Id;", new { Id = id }, cancellationToken);

    public Task<IReadOnlyCollection<PollingTask>> GetPollingTasksByDeviceAsync(Guid deviceId, CancellationToken cancellationToken)
        => QueryListAsync<PollingTask>("SELECT * FROM PollingTasks WHERE DeviceId = @DeviceId ORDER BY Name;", new { DeviceId = deviceId }, cancellationToken);

    public Task SavePollingTaskAsync(PollingTask task, CancellationToken cancellationToken)
        => ExecuteAsync(
            "REPLACE INTO PollingTasks (Id, DeviceId, Name, IntervalSeconds, TriggerOnChange, BatchRead, Enabled) VALUES (@Id, @DeviceId, @Name, @IntervalSeconds, @TriggerOnChange, @BatchRead, @Enabled);",
            task,
            cancellationToken);

    public Task DeletePollingTaskAsync(Guid id, CancellationToken cancellationToken)
        => ExecuteAsync("DELETE FROM PollingTasks WHERE Id = @Id;", new { Id = id }, cancellationToken);

    public Task<IReadOnlyCollection<TransformRule>> GetTransformRulesAsync(CancellationToken cancellationToken)
        => QueryListAsync<TransformRule>("SELECT * FROM TransformRules ORDER BY SortOrder, Name;", cancellationToken);

    public Task<IReadOnlyCollection<TransformRule>> GetTransformRulesByPointAsync(Guid pointId, CancellationToken cancellationToken)
        => QueryListAsync<TransformRule>("SELECT * FROM TransformRules WHERE PointId = @PointId ORDER BY SortOrder, Name;", new { PointId = pointId }, cancellationToken);

    public Task SaveTransformRuleAsync(TransformRule rule, CancellationToken cancellationToken)
        => ExecuteAsync(
            "REPLACE INTO TransformRules (Id, PointId, Name, Kind, SortOrder, ArgumentsJson, Enabled) VALUES (@Id, @PointId, @Name, @Kind, @SortOrder, @ArgumentsJson, @Enabled);",
            rule,
            cancellationToken);

    public Task DeleteTransformRuleAsync(Guid id, CancellationToken cancellationToken)
        => ExecuteAsync("DELETE FROM TransformRules WHERE Id = @Id;", new { Id = id }, cancellationToken);

    public Task<IReadOnlyCollection<UploadChannel>> GetUploadChannelsAsync(CancellationToken cancellationToken)
        => QueryListAsync<UploadChannel>("SELECT * FROM UploadChannels ORDER BY Name;", cancellationToken);

    public Task<UploadChannel?> GetUploadChannelAsync(Guid id, CancellationToken cancellationToken)
        => QuerySingleAsync<UploadChannel>("SELECT * FROM UploadChannels WHERE Id = @Id;", new { Id = id }, cancellationToken);

    public Task SaveUploadChannelAsync(UploadChannel channel, CancellationToken cancellationToken)
        => ExecuteAsync(
            "REPLACE INTO UploadChannels (Id, Name, Protocol, Endpoint, SettingsJson, BatchSize, BufferingEnabled, Enabled) VALUES (@Id, @Name, @Protocol, @Endpoint, @SettingsJson, @BatchSize, @BufferingEnabled, @Enabled);",
            channel,
            cancellationToken);

    public Task DeleteUploadChannelAsync(Guid id, CancellationToken cancellationToken)
        => ExecuteAsync("DELETE FROM UploadChannels WHERE Id = @Id;", new { Id = id }, cancellationToken);

    public Task<IReadOnlyCollection<UploadRoute>> GetUploadRoutesAsync(CancellationToken cancellationToken)
        => QueryListAsync<UploadRoute>("SELECT * FROM UploadRoutes ORDER BY Target;", cancellationToken);

    public Task<IReadOnlyCollection<UploadRoute>> GetUploadRoutesByPointAsync(Guid pointId, CancellationToken cancellationToken)
        => QueryListAsync<UploadRoute>("SELECT * FROM UploadRoutes WHERE PointId = @PointId ORDER BY Target;", new { PointId = pointId }, cancellationToken);

    public Task SaveUploadRouteAsync(UploadRoute route, CancellationToken cancellationToken)
        => ExecuteAsync(
            "REPLACE INTO UploadRoutes (Id, PointId, UploadChannelId, PayloadTemplate, Target, Enabled) VALUES (@Id, @PointId, @UploadChannelId, @PayloadTemplate, @Target, @Enabled);",
            route,
            cancellationToken);

    public Task DeleteUploadRouteAsync(Guid id, CancellationToken cancellationToken)
        => ExecuteAsync("DELETE FROM UploadRoutes WHERE Id = @Id;", new { Id = id }, cancellationToken);

    public Task SaveWriteCommandAsync(WriteCommand command, CancellationToken cancellationToken)
        => ExecuteAsync(
            "REPLACE INTO WriteCommands (Id, DeviceId, PointId, Address, ValueJson, RequestedAtUtc, Status, ErrorMessage) VALUES (@Id, @DeviceId, @PointId, @Address, @ValueJson, @RequestedAtUtc, @Status, @ErrorMessage);",
            command,
            cancellationToken);

    public Task<IReadOnlyCollection<WriteCommand>> GetWriteCommandsAsync(CancellationToken cancellationToken)
        => QueryListAsync<WriteCommand>("SELECT * FROM WriteCommands ORDER BY RequestedAtUtc DESC;", cancellationToken);

    private async Task ExecuteAsync(string sql, object? parameters, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    private async Task<IReadOnlyCollection<T>> QueryListAsync<T>(string sql, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<T>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return items.ToArray();
    }

    private async Task<IReadOnlyCollection<T>> QueryListAsync<T>(string sql, object parameters, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return items.ToArray();
    }

    private async Task<T?> QuerySingleAsync<T>(string sql, object parameters, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }
}
