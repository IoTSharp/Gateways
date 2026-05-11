using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var edgeApiBaseUrl = builder.Configuration["EdgeApi:BaseUrl"] ?? "http://127.0.0.1:18180";

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        context.Context.Response.Headers.Pragma = "no-cache";
    }
});

app.MapGet("/api/frontend/config", () => Results.Ok(new
{
    edgeApiBaseUrl,
    builtAtUtc = DateTime.UtcNow
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    application = "IoTSharp.Edge.Admin",
    edgeApiBaseUrl,
    timestampUtc = DateTime.UtcNow
}));

app.Run();
