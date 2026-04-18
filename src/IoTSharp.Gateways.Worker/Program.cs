using System.Text;
using System.Text.Json.Serialization;
using IoTSharp.Gateways.Application;
using IoTSharp.Gateways.Infrastructure;
using IoTSharp.Gateways.Infrastructure.Persistence;
using IoTSharp.Gateways.Worker;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<Microsoft.Extensions.Hosting.HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});
builder.Services.Configure<EdgeReportingOptions>(builder.Configuration.GetSection("EdgeReporting"));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<ValueTransformationService>();
builder.Services.AddScoped<GatewayConfigurationService>();
builder.Services.AddScoped<DriverCatalogService>();
builder.Services.AddScoped<GatewayRuntimeService>();
builder.Services.AddGatewayInfrastructure(builder.Configuration);
builder.Services.AddHostedService<GatewayPollingWorker>();
builder.Services.AddHostedService<EdgeRuntimeReportingWorker>();

var host = builder.Build();
using (var scope = host.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IGatewaySchemaInitializer>();
    await initializer.InitializeAsync(CancellationToken.None);
}

await host.RunAsync();
