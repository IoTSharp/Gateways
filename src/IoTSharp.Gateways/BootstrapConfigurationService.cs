using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace IoTSharp.Gateways;

public sealed class BootstrapConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly IHostEnvironment _hostEnvironment;
    private readonly IConfigurationRoot? _configurationRoot;

    public BootstrapConfigurationService(IHostEnvironment hostEnvironment, IConfiguration configuration)
    {
        _hostEnvironment = hostEnvironment;
        _configurationRoot = configuration as IConfigurationRoot;
    }

    public string FilePath => Path.Combine(_hostEnvironment.ContentRootPath, "bootstrap.json");

    public async Task<BootstrapConfigurationDocument> GetAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(FilePath))
        {
            return new BootstrapConfigurationDocument(
                false,
                FilePath,
                null,
                CreateTemplate());
        }

        var json = await File.ReadAllTextAsync(FilePath, cancellationToken);
        return new BootstrapConfigurationDocument(
            true,
            FilePath,
            File.GetLastWriteTimeUtc(FilePath),
            Normalize(json));
    }

    public async Task<BootstrapConfigurationDocument> SaveAsync(string json, CancellationToken cancellationToken)
    {
        var normalized = Normalize(json);
        Directory.CreateDirectory(_hostEnvironment.ContentRootPath);

        var tempPath = FilePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, normalized, new UTF8Encoding(false), cancellationToken);
        File.Move(tempPath, FilePath, overwrite: true);
        _configurationRoot?.Reload();

        return new BootstrapConfigurationDocument(
            true,
            FilePath,
            File.GetLastWriteTimeUtc(FilePath),
            normalized);
    }

    public string CreateTemplate()
        => JsonSerializer.Serialize(
            new
            {
                EdgeReporting = new
                {
                    Enabled = true,
                    RuntimeType = "gateway",
                    RuntimeName = "Gateway Runtime",
                    InstanceId = "",
                    BaseUrl = "http://127.0.0.1:27915/",
                    AccessToken = "",
                    HeartbeatIntervalSeconds = 30,
                    RetryDelaySeconds = 5,
                    Metadata = new Dictionary<string, string>()
                }
            },
            JsonOptions);

    private static string Normalize(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Bootstrap config must be a JSON object.");
        }

        return JsonSerializer.Serialize(document.RootElement, JsonOptions);
    }
}

public sealed record BootstrapConfigurationDocument(
    bool Exists,
    string FilePath,
    DateTime? LastWriteTimeUtc,
    string Json);
