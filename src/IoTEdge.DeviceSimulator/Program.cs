using IoTEdge.DeviceSimulator;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ModbusSimulatorOptions>(builder.Configuration.GetSection("ModbusSimulator"));
builder.Services.AddSingleton<ModbusSimulatorState>();
builder.Services.AddSingleton<IoTServerModbusHost>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<IoTServerModbusHost>());
builder.Services.AddHostedService<ModbusValueWriter>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    application = "IoTEdge.DeviceSimulator",
    timestampUtc = DateTime.UtcNow
}));

app.MapGet("/api/values", (ModbusSimulatorState state) => Results.Ok(state.GetSnapshot()));

app.Run();
