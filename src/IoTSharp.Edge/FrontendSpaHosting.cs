using System.Diagnostics;
using System.Net.Sockets;

namespace IoTSharp.Edge;

internal sealed class FrontendDevelopmentServerHostedService : IHostedService
{
    private static readonly Uri DevServerUri = new("http://127.0.0.1:5173");

    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<FrontendDevelopmentServerHostedService> _logger;
    private Process? _process;

    public FrontendDevelopmentServerHostedService(IHostEnvironment hostEnvironment, ILogger<FrontendDevelopmentServerHostedService> logger)
    {
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_hostEnvironment.IsDevelopment())
        {
            return;
        }

        try
        {
            var clientAppPath = Path.Combine(_hostEnvironment.ContentRootPath, "ClientApp");
            if (!File.Exists(Path.Combine(clientAppPath, "package.json")))
            {
                _logger.LogInformation("ClientApp not found at {ClientAppPath}; skipping frontend dev server start.", clientAppPath);
                return;
            }

            if (await IsPortOpenAsync(DevServerUri, TimeSpan.FromMilliseconds(100), cancellationToken))
            {
                _logger.LogInformation("Frontend dev server already listening on {DevServerUri}.", DevServerUri);
                return;
            }

            await EnsureClientDependenciesAsync(clientAppPath, cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = "npm",
                Arguments = "run dev",
                WorkingDirectory = clientAppPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            _process.OutputDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    _logger.LogInformation("[client] {Line}", args.Data);
                }
            };
            _process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    _logger.LogWarning("[client] {Line}", args.Data);
                }
            };

            if (!_process.Start())
            {
                throw new InvalidOperationException("Failed to start Vue frontend dev server.");
            }

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _logger.LogInformation("Starting frontend dev server from {ClientAppPath}.", clientAppPath);
            await WaitForPortAsync(DevServerUri, TimeSpan.FromSeconds(30), cancellationToken);
            _logger.LogInformation("Frontend dev server is ready at {DevServerUri}.", DevServerUri);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Failed to start frontend dev server. Edge API will keep running.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Failed to stop frontend dev server cleanly.");
        }

        return Task.CompletedTask;
    }

    private static async Task EnsureClientDependenciesAsync(string clientAppPath, CancellationToken cancellationToken)
    {
        var nodeModulesPath = Path.Combine(clientAppPath, "node_modules");
        if (Directory.Exists(nodeModulesPath))
        {
            return;
        }

        var lockFilePath = Path.Combine(clientAppPath, "package-lock.json");
        if (File.Exists(lockFilePath))
        {
            await RunProcessAsync(clientAppPath, "npm", "ci --no-audit --no-fund", cancellationToken);
            return;
        }

        await RunProcessAsync(clientAppPath, "npm", "install --no-audit --no-fund", cancellationToken);
    }

    private static async Task RunProcessAsync(string workingDirectory, string fileName, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process '{fileName} {arguments}'.");
        }

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Process '{fileName} {arguments}' failed with exit code {process.ExitCode}.");
        }
    }

    private static async Task WaitForPortAsync(Uri uri, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            if (await IsPortOpenAsync(uri, TimeSpan.FromMilliseconds(250), cancellationToken))
            {
                return;
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for frontend dev server at {uri}.");
    }

    private static async Task<bool> IsPortOpenAsync(Uri uri, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(uri.Host, uri.Port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(timeout, cancellationToken));
            return completed == connectTask && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}

internal static class FrontendSpaProxy
{
    private static readonly Uri DevServerUri = new("http://127.0.0.1:5173");

    public static async Task<bool> TryProxyAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/health"))
        {
            return false;
        }

        var factory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(nameof(FrontendSpaProxy));
        var targetUri = new Uri(DevServerUri, context.Request.Path + context.Request.QueryString);
        using var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUri);

        foreach (var header in context.Request.Headers)
        {
            if (!ShouldCopyRequestHeader(header.Key))
            {
                continue;
            }

            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            requestMessage.Content = new StreamContent(context.Request.Body);
            foreach (var header in context.Request.Headers)
            {
                if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    requestMessage.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }
        }

        HttpResponseMessage responseMessage;
        try
        {
            responseMessage = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
        }
        catch (Exception exception) when ((exception is HttpRequestException or TaskCanceledException) && !context.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        using (responseMessage)
        {
            context.Response.StatusCode = (int)responseMessage.StatusCode;

            foreach (var header in responseMessage.Headers)
            {
                if (ShouldCopyResponseHeader(header.Key))
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }
            }

            foreach (var header in responseMessage.Content.Headers)
            {
                if (ShouldCopyResponseHeader(header.Key))
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }
            }

            context.Response.Headers.Remove("transfer-encoding");
            await responseMessage.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
            return true;
        }
    }

    private static bool ShouldCopyRequestHeader(string headerName)
        => !string.Equals(headerName, "Host", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(headerName, "Content-Length", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(headerName, "Connection", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(headerName, "Upgrade", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(headerName, "Proxy-Connection", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldCopyResponseHeader(string headerName)
        => !string.Equals(headerName, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(headerName, "Connection", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(headerName, "Keep-Alive", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(headerName, "Proxy-Connection", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(headerName, "Upgrade", StringComparison.OrdinalIgnoreCase);
}
