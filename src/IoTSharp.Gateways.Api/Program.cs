using System.Text;
using System.Text.Json.Serialization;
using IoTSharp.Gateways.Application;
using IoTSharp.Gateways.Domain;
using IoTSharp.Gateways.Infrastructure;
using IoTSharp.Gateways.Infrastructure.Persistence;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});
builder.Services.AddSingleton<ValueTransformationService>();
builder.Services.AddScoped<GatewayConfigurationService>();
builder.Services.AddScoped<DriverCatalogService>();
builder.Services.AddScoped<GatewayRuntimeService>();
builder.Services.AddGatewayInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseCors();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IGatewaySchemaInitializer>();
    await initializer.InitializeAsync(CancellationToken.None);
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/drivers", (DriverCatalogService service) => Results.Ok(service.GetDrivers()));

app.MapGet("/api/channels", async (GatewayConfigurationService service, CancellationToken ct) => Results.Ok(await service.GetChannelsAsync(ct)));
app.MapPost("/api/channels", async (GatewayChannel channel, GatewayConfigurationService service, CancellationToken ct) =>
{
    if (channel.Id == Guid.Empty) channel.Id = Guid.NewGuid();
    await service.SaveChannelAsync(channel, ct);
    return Results.Ok(channel);
});
app.MapDelete("/api/channels/{id:guid}", async (Guid id, GatewayConfigurationService service, CancellationToken ct) =>
{
    await service.DeleteChannelAsync(id, ct);
    return Results.NoContent();
});

app.MapGet("/api/devices", async (GatewayConfigurationService service, CancellationToken ct) => Results.Ok(await service.GetDevicesAsync(ct)));
app.MapPost("/api/devices", async (Device device, GatewayConfigurationService service, CancellationToken ct) =>
{
    if (device.Id == Guid.Empty) device.Id = Guid.NewGuid();
    await service.SaveDeviceAsync(device, ct);
    return Results.Ok(device);
});
app.MapDelete("/api/devices/{id:guid}", async (Guid id, GatewayConfigurationService service, CancellationToken ct) =>
{
    await service.DeleteDeviceAsync(id, ct);
    return Results.NoContent();
});

app.MapGet("/api/points", async (GatewayConfigurationService service, CancellationToken ct) => Results.Ok(await service.GetPointsAsync(ct)));
app.MapPost("/api/points", async (Point point, GatewayConfigurationService service, CancellationToken ct) =>
{
    if (point.Id == Guid.Empty) point.Id = Guid.NewGuid();
    await service.SavePointAsync(point, ct);
    return Results.Ok(point);
});
app.MapDelete("/api/points/{id:guid}", async (Guid id, GatewayConfigurationService service, CancellationToken ct) =>
{
    await service.DeletePointAsync(id, ct);
    return Results.NoContent();
});

app.MapGet("/api/polling-tasks", async (GatewayConfigurationService service, CancellationToken ct) => Results.Ok(await service.GetPollingTasksAsync(ct)));
app.MapPost("/api/polling-tasks", async (PollingTask task, GatewayConfigurationService service, CancellationToken ct) =>
{
    if (task.Id == Guid.Empty) task.Id = Guid.NewGuid();
    await service.SavePollingTaskAsync(task, ct);
    return Results.Ok(task);
});
app.MapDelete("/api/polling-tasks/{id:guid}", async (Guid id, GatewayConfigurationService service, CancellationToken ct) =>
{
    await service.DeletePollingTaskAsync(id, ct);
    return Results.NoContent();
});

app.MapGet("/api/transform-rules", async (GatewayConfigurationService service, CancellationToken ct) => Results.Ok(await service.GetTransformRulesAsync(ct)));
app.MapPost("/api/transform-rules", async (TransformRule rule, GatewayConfigurationService service, CancellationToken ct) =>
{
    if (rule.Id == Guid.Empty) rule.Id = Guid.NewGuid();
    await service.SaveTransformRuleAsync(rule, ct);
    return Results.Ok(rule);
});
app.MapDelete("/api/transform-rules/{id:guid}", async (Guid id, GatewayConfigurationService service, CancellationToken ct) =>
{
    await service.DeleteTransformRuleAsync(id, ct);
    return Results.NoContent();
});

app.MapGet("/api/upload-channels", async (GatewayConfigurationService service, CancellationToken ct) => Results.Ok(await service.GetUploadChannelsAsync(ct)));
app.MapPost("/api/upload-channels", async (UploadChannel channel, GatewayConfigurationService service, CancellationToken ct) =>
{
    if (channel.Id == Guid.Empty) channel.Id = Guid.NewGuid();
    await service.SaveUploadChannelAsync(channel, ct);
    return Results.Ok(channel);
});
app.MapDelete("/api/upload-channels/{id:guid}", async (Guid id, GatewayConfigurationService service, CancellationToken ct) =>
{
    await service.DeleteUploadChannelAsync(id, ct);
    return Results.NoContent();
});

app.MapGet("/api/upload-routes", async (GatewayConfigurationService service, CancellationToken ct) => Results.Ok(await service.GetUploadRoutesAsync(ct)));
app.MapPost("/api/upload-routes", async (UploadRoute route, GatewayConfigurationService service, CancellationToken ct) =>
{
    if (route.Id == Guid.Empty) route.Id = Guid.NewGuid();
    await service.SaveUploadRouteAsync(route, ct);
    return Results.Ok(route);
});
app.MapDelete("/api/upload-routes/{id:guid}", async (Guid id, GatewayConfigurationService service, CancellationToken ct) =>
{
    await service.DeleteUploadRouteAsync(id, ct);
    return Results.NoContent();
});

app.MapGet("/api/write-commands", async (IGatewayRepository repository, CancellationToken ct) => Results.Ok(await repository.GetWriteCommandsAsync(ct)));
app.MapPost("/api/runtime/read", async (DriverReadOperation operation, GatewayRuntimeService service, CancellationToken ct) =>
{
    var result = await service.ExecuteReadAsync(operation.DriverCode, operation.ConnectionSettings, new DriverReadRequest(operation.Address, operation.DataType, operation.Length, operation.PointSettings), ct);
    return Results.Ok(result);
});
app.MapPost("/api/runtime/write", async (DriverWriteOperation operation, GatewayRuntimeService service, CancellationToken ct) =>
{
    var result = await service.ExecuteWriteAsync(operation.DriverCode, operation.ConnectionSettings, new DriverWriteRequest(operation.Address, operation.DataType, operation.Value, operation.Length, operation.PointSettings), ct);
    return Results.Ok(result);
});
app.MapPost("/api/runtime/poll/{taskId:guid}", async (Guid taskId, GatewayRuntimeService service, CancellationToken ct) => Results.Ok(await service.ExecutePollingTaskAsync(taskId, ct)));
app.MapPost("/api/runtime/devices/{deviceId:guid}/points/{pointId:guid}/write", async (Guid deviceId, Guid pointId, PointWriteOperation operation, GatewayRuntimeService service, CancellationToken ct) =>
    Results.Ok(await service.ExecutePointWriteAsync(deviceId, pointId, operation.Value, ct)));

app.Run();

internal sealed record DriverReadOperation(string DriverCode, Dictionary<string, string?> ConnectionSettings, Dictionary<string, string?> PointSettings, string Address, GatewayDataType DataType, ushort Length = 1);
internal sealed record DriverWriteOperation(string DriverCode, Dictionary<string, string?> ConnectionSettings, Dictionary<string, string?> PointSettings, string Address, GatewayDataType DataType, object? Value, ushort Length = 1);
internal sealed record PointWriteOperation(object? Value);
