using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using IoTSharp.Edge;
using IoTSharp.Edge.Application;
using IoTSharp.Edge.BasicRuntime;
using IoTSharp.Edge.Domain;
using IoTSharp.Edge.Infrastructure;
using IoTSharp.Edge.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("bootstrap.json", optional: true, reloadOnChange: true);

builder.Services.Configure<Microsoft.Extensions.Hosting.HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});
builder.Services.Configure<EdgeReportingOptions>(builder.Configuration.GetSection("EdgeReporting"));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});
builder.Services.AddSingleton<BootstrapConfigurationService>();
builder.Services.AddSingleton<CollectionConfigurationSyncState>();
#if EDGE_BASIC_RUNTIME_EXTENSIONS
builder.Services.AddSingleton<IBasicRuntimeExtension>(sp => new IoTSharp.Edge.RuntimeExtensions.GatewayRuntimeExtension(
    sp.GetRequiredService<IDeviceDriverRegistry>(),
    sp.GetRequiredService<IUploadTransportRegistry>()));
#endif
builder.Services.AddSingleton<BasicRuntime>(sp =>
{
    var runtime = new BasicRuntime();
    runtime.Use(sp.GetServices<IBasicRuntimeExtension>());
    return runtime;
});
builder.Services.AddSingleton<ValueTransformationService>();
builder.Services.AddScoped<DriverCatalogService>();
builder.Services.AddScoped<GatewayRuntimeService>();
builder.Services.AddGatewayInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IEdgeTaskReceiptReporter, EdgeTaskReceiptExample>();
builder.Services.AddHostedService<GatewayPollingWorker>();
builder.Services.AddHostedService<GatewayCollectionConfigurationWorker>();
builder.Services.AddHostedService<EdgeRuntimeReportingWorker>();

var app = builder.Build();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IGatewaySchemaInitializer>();
    await initializer.InitializeAsync(CancellationToken.None);
}

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    application = "IoTSharp.Edge",
    timestampUtc = DateTime.UtcNow
}));

app.MapGet("/api/bootstrap/config", async (BootstrapConfigurationService service, CancellationToken ct) =>
    Results.Ok(await service.GetAsync(ct)));

app.MapPost("/api/bootstrap/config", async (BootstrapConfigUpdateRequest request, BootstrapConfigurationService service, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Json))
    {
        return Results.BadRequest(new { message = "Bootstrap config JSON is required." });
    }

    try
    {
        var saved = await service.SaveAsync(request.Json, ct);
        return Results.Ok(saved);
    }
    catch (Exception exception)
    {
        return Results.BadRequest(new { message = exception.Message });
    }
});

app.MapGet("/api/diagnostics/summary", async (
    IHostEnvironment environment,
    BasicRuntime basicRuntime,
    BootstrapConfigurationService bootstrapConfiguration,
    IOptionsMonitor<EdgeReportingOptions> edgeOptionsMonitor,
    CollectionConfigurationSyncState collectionSyncState,
    IGatewayRepository repository,
    CancellationToken ct) =>
{
    using var currentProcess = Process.GetCurrentProcess();
    currentProcess.Refresh();

    var edgeOptions = edgeOptionsMonitor.CurrentValue;
    var bootstrapInfo = await bootstrapConfiguration.GetAsync(ct);
    var channels = await repository.GetChannelsAsync(ct);
    var devices = await repository.GetDevicesAsync(ct);
    var points = await repository.GetPointsAsync(ct);
    var pollingTasks = await repository.GetPollingTasksAsync(ct);
    var uploadChannels = await repository.GetUploadChannelsAsync(ct);
    var uploadRoutes = await repository.GetUploadRoutesAsync(ct);
    var writeCommands = await repository.GetWriteCommandsAsync(ct);
    var collectionSync = collectionSyncState.GetSnapshot();

    return Results.Ok(new
    {
        generatedAtUtc = DateTime.UtcNow,
        applicationName = environment.ApplicationName,
        environment = environment.EnvironmentName,
        contentRootPath = environment.ContentRootPath,
        machineName = Environment.MachineName,
        process = new
        {
            id = currentProcess.Id,
            name = currentProcess.ProcessName,
            workingSetBytes = currentProcess.WorkingSet64,
            privateMemoryBytes = currentProcess.PrivateMemorySize64,
            threadCount = currentProcess.Threads.Count,
            startTimeUtc = currentProcess.StartTime.ToUniversalTime()
        },
        bootstrap = new
        {
            bootstrapInfo.Exists,
            bootstrapInfo.FilePath,
            bootstrapInfo.LastWriteTimeUtc
        },
        edgeReporting = new
        {
            edgeOptions.Enabled,
            edgeOptions.RuntimeType,
            edgeOptions.RuntimeName,
            edgeOptions.InstanceId,
            edgeOptions.BaseUrl,
            hasAccessToken = !string.IsNullOrWhiteSpace(edgeOptions.AccessToken),
            edgeOptions.HeartbeatIntervalSeconds,
            edgeOptions.RetryDelaySeconds,
            metadata = edgeOptions.Metadata
        },
        collectionSync,
        basicRuntime = new
        {
            extensionCount = basicRuntime.RegisteredExtensions.Count,
            extensions = basicRuntime.RegisteredExtensions
        },
        counts = new
        {
            channelCount = channels.Count,
            enabledChannelCount = channels.Count(item => item.Enabled),
            deviceCount = devices.Count,
            enabledDeviceCount = devices.Count(item => item.Enabled),
            pointCount = points.Count,
            enabledPointCount = points.Count(item => item.Enabled),
            pollingTaskCount = pollingTasks.Count,
            enabledPollingTaskCount = pollingTasks.Count(item => item.Enabled),
            uploadChannelCount = uploadChannels.Count,
            enabledUploadChannelCount = uploadChannels.Count(item => item.Enabled),
            uploadRouteCount = uploadRoutes.Count,
            enabledUploadRouteCount = uploadRoutes.Count(item => item.Enabled),
            recentWriteCommandCount = writeCommands.Count
        }
    });
});

app.Run();

internal sealed record BootstrapConfigUpdateRequest(string Json);
