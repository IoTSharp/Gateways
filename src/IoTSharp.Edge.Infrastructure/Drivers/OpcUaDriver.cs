namespace IoTSharp.Edge.Infrastructure.Drivers;

internal sealed class OpcUaDriver : DeviceDriverBase
{
    private static readonly ITelemetryContext Telemetry = DefaultTelemetry.Create(_ => { });

    public override DriverMetadata Metadata { get; } = new(
        "opc-ua",
        DriverType.OpcUa,
        "OPC UA 协议",
        "基于 OPC Foundation UA .NET Standard 的 OPC UA 节点读写驱动。",
        true,
        true,
        true,
        true,
        new[]
        {
            new ConnectionSettingDefinition("endpoint", "端点", "text", true, "OPC UA 端点 URL，例如 opc.tcp://127.0.0.1:4840。"),
            new ConnectionSettingDefinition("useSecurity", "启用安全", "boolean", false, "自动发现并优先使用安全端点。"),
            new ConnectionSettingDefinition("securityMode", "安全模式", "select", false, "请求的消息安全模式。", new[] { "Auto", "None", "Sign", "SignAndEncrypt" }),
            new ConnectionSettingDefinition("securityPolicy", "安全策略", "select", false, "请求的安全策略。", new[] { "Auto", "None", "Basic256Sha256", "Aes128_Sha256_RsaOaep", "Aes256_Sha256_RsaPss" }),
            new ConnectionSettingDefinition("username", "用户名", "text", false, "用于认证会话的用户名。"),
            new ConnectionSettingDefinition("password", "密码", "password", false, "用于认证会话的密码。"),
            new ConnectionSettingDefinition("timeout", "超时", "number", false, "操作超时时间，单位毫秒。"),
            new ConnectionSettingDefinition("sessionTimeout", "会话超时", "number", false, "会话超时时间，单位毫秒。"),
            new ConnectionSettingDefinition("autoAcceptUntrustedCertificates", "自动接受不受信任证书", "boolean", false, "自动接受不受信任的服务器证书。")
        });

    public override async Task<ConnectionTestResult> TestConnectionAsync(DriverConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            await using var session = await OpenSessionAsync(context.Settings, cancellationToken);
            return new ConnectionTestResult(true);
        }
        catch (Exception exception)
        {
            return new ConnectionTestResult(false, exception.Message);
        }
    }

    public override Task<AddressValidationResult> ValidateAddressAsync(DriverReadRequest request, CancellationToken cancellationToken)
        => Task.FromResult(NodeId.TryParse(request.Address, out _)
            ? new AddressValidationResult(true)
            : new AddressValidationResult(false, "OPC UA 地址必须是有效的 NodeId，例如 ns=2;s=设备.温度。"));

    public override async Task<DriverReadResult> ReadAsync(DriverConnectionContext context, DriverReadRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!NodeId.TryParse(request.Address, out var nodeId))
            {
                return new DriverReadResult(request.Address, null, null, DateTimeOffset.UtcNow, QualityStatus.Bad, "OPC UA 地址必须是有效的 NodeId。");
            }

            await using var session = await OpenSessionAsync(context.Settings, cancellationToken);
            var response = await session.ReadAsync(
                null,
                0,
                TimestampsToReturn.Both,
                new ReadValueIdCollection
                {
                    new()
                    {
                        NodeId = nodeId,
                        AttributeId = Attributes.Value
                    }
                },
                cancellationToken);

            var value = response.Results.Count > 0 ? response.Results[0] : new DataValue(new StatusCode(StatusCodes.BadNoData));
            if (StatusCode.IsBad(value.StatusCode))
            {
                return new DriverReadResult(
                    request.Address,
                    null,
                    null,
                    ResolveOpcUaTimestamp(value),
                    QualityStatus.Bad,
                    DescribeStatus(value.StatusCode));
            }

            var rawValue = NormalizeOpcUaValue(value.Value, request.DataType);
            return new DriverReadResult(request.Address, rawValue, rawValue, ResolveOpcUaTimestamp(value), QualityStatus.Good);
        }
        catch (Exception exception)
        {
            return FailedRead(request.Address, exception);
        }
    }

    public override async Task<IReadOnlyCollection<DriverReadResult>> ReadBatchAsync(DriverConnectionContext context, DriverBatchReadRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var nodes = new ReadValueIdCollection();
            var orderedRequests = request.Requests.ToArray();
            foreach (var item in orderedRequests)
            {
                if (!NodeId.TryParse(item.Address, out var nodeId))
                {
                    return orderedRequests
                .Select(invalidItem => new DriverReadResult(invalidItem.Address, null, null, DateTimeOffset.UtcNow, QualityStatus.Bad, "OPC UA 地址必须是有效的 NodeId。"))
                        .ToArray();
                }

                nodes.Add(new ReadValueId
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.Value
                });
            }

            await using var session = await OpenSessionAsync(context.Settings, cancellationToken);
            var response = await session.ReadAsync(null, 0, TimestampsToReturn.Both, nodes, cancellationToken);
            var results = new List<DriverReadResult>(orderedRequests.Length);
            for (var index = 0; index < orderedRequests.Length; index++)
            {
                var item = orderedRequests[index];
                var value = response.Results.Count > index ? response.Results[index] : new DataValue(new StatusCode(StatusCodes.BadNoData));
                if (StatusCode.IsBad(value.StatusCode))
                {
                    results.Add(new DriverReadResult(item.Address, null, null, ResolveOpcUaTimestamp(value), QualityStatus.Bad, DescribeStatus(value.StatusCode)));
                    continue;
                }

                var rawValue = NormalizeOpcUaValue(value.Value, item.DataType);
                results.Add(new DriverReadResult(item.Address, rawValue, rawValue, ResolveOpcUaTimestamp(value), QualityStatus.Good));
            }

            return results;
        }
        catch (Exception exception)
        {
            return request.Requests
                .Select(item => FailedRead(item.Address, exception))
                .ToArray();
        }
    }

    public override async Task<DriverWriteResult> WriteAsync(DriverConnectionContext context, DriverWriteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!NodeId.TryParse(request.Address, out var nodeId))
            {
                return new DriverWriteResult(request.Address, request.Value, DateTimeOffset.UtcNow, QualityStatus.Bad, "OPC UA 地址必须是有效的 NodeId。");
            }

            await using var session = await OpenSessionAsync(context.Settings, cancellationToken);
            var response = await session.WriteAsync(
                null,
                new WriteValueCollection
                {
                    new()
                    {
                        NodeId = nodeId,
                        AttributeId = Attributes.Value,
                        Value = new DataValue(new Variant(CoerceWriteValue(request.Value, request.DataType)))
                    }
                },
                cancellationToken);

            var status = response.Results.Count > 0 ? response.Results[0] : new StatusCode(StatusCodes.BadNoData);
            return StatusCode.IsBad(status)
                ? new DriverWriteResult(request.Address, request.Value, DateTimeOffset.UtcNow, QualityStatus.Bad, DescribeStatus(status))
                : new DriverWriteResult(request.Address, request.Value, DateTimeOffset.UtcNow, QualityStatus.Good);
        }
        catch (Exception exception)
        {
            return FailedWrite(request.Address, request.Value, exception);
        }
    }

    public override async Task<IReadOnlyCollection<DriverWriteResult>> WriteBatchAsync(DriverConnectionContext context, DriverBatchWriteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var writes = new WriteValueCollection();
            var orderedRequests = request.Requests.ToArray();
            foreach (var item in orderedRequests)
            {
                if (!NodeId.TryParse(item.Address, out var nodeId))
                {
                    return orderedRequests
                .Select(invalidItem => new DriverWriteResult(invalidItem.Address, invalidItem.Value, DateTimeOffset.UtcNow, QualityStatus.Bad, "OPC UA 地址必须是有效的 NodeId。"))
                        .ToArray();
                }

                writes.Add(new WriteValue
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(CoerceWriteValue(item.Value, item.DataType)))
                });
            }

            await using var session = await OpenSessionAsync(context.Settings, cancellationToken);
            var response = await session.WriteAsync(null, writes, cancellationToken);
            var results = new List<DriverWriteResult>(orderedRequests.Length);
            for (var index = 0; index < orderedRequests.Length; index++)
            {
                var item = orderedRequests[index];
                var status = response.Results.Count > index ? response.Results[index] : new StatusCode(StatusCodes.BadNoData);
                results.Add(StatusCode.IsBad(status)
                    ? new DriverWriteResult(item.Address, item.Value, DateTimeOffset.UtcNow, QualityStatus.Bad, DescribeStatus(status))
                    : new DriverWriteResult(item.Address, item.Value, DateTimeOffset.UtcNow, QualityStatus.Good));
            }

            return results;
        }
        catch (Exception exception)
        {
            return request.Requests
                .Select(item => FailedWrite(item.Address, item.Value, exception))
                .ToArray();
        }
    }

    private static async Task<AsyncSession> OpenSessionAsync(IReadOnlyDictionary<string, string?> settings, CancellationToken cancellationToken)
    {
        var endpointUrl = Required(settings, "endpoint");
        var timeout = Int(settings, "timeout", 3000);
        var sessionTimeout = (uint)Math.Max(Int(settings, "sessionTimeout", 60000), 1);
        var applicationName = settings.TryGetValue("applicationName", out var configuredName) && !string.IsNullOrWhiteSpace(configuredName)
            ? configuredName
            : "IoTSharp Edge";
        var applicationUri = settings.TryGetValue("applicationUri", out var configuredUri) && !string.IsNullOrWhiteSpace(configuredUri)
            ? configuredUri
            : $"urn:{Environment.MachineName}:iotsharp:edge";

        var configuration = await BuildApplicationConfigurationAsync(
            applicationName,
            applicationUri,
            timeout,
            sessionTimeout,
            Boolean(settings, "autoAcceptUntrustedCertificates", false),
            cancellationToken);
        var endpoint = await BuildConfiguredEndpointAsync(configuration, settings, endpointUrl, timeout, cancellationToken);
        var identity = BuildIdentity(settings);
        var session = await new DefaultSessionFactory(Telemetry).CreateAsync(
            configuration,
            endpoint,
            false,
            false,
            applicationName,
            sessionTimeout,
            identity,
            null,
            cancellationToken);

        return new AsyncSession(session);
    }

    private static async Task<ApplicationConfiguration> BuildApplicationConfigurationAsync(
        string applicationName,
        string applicationUri,
        int timeout,
        uint sessionTimeout,
        bool autoAcceptUntrustedCertificates,
        CancellationToken cancellationToken)
    {
        var pkiRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IoTSharp",
            "Edge",
            "OPCUA",
            "pki");

        var configuration = new ApplicationConfiguration(Telemetry)
        {
            ApplicationName = applicationName,
            ApplicationUri = applicationUri,
            ProductUri = "urn:iotsharp:edge",
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                AutoAcceptUntrustedCertificates = autoAcceptUntrustedCertificates,
                RejectSHA1SignedCertificates = false,
                MinimumCertificateKeySize = 1024,
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pkiRoot, "own"),
                    SubjectName = $"CN={applicationName}"
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StorePath = Path.Combine(pkiRoot, "trusted")
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StorePath = Path.Combine(pkiRoot, "issuer")
                },
                RejectedCertificateStore = new CertificateStoreIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pkiRoot, "rejected")
                }
            },
            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = timeout,
                MaxStringLength = 1_048_576,
                MaxByteStringLength = 1_048_576,
                MaxArrayLength = 65_535,
                MaxMessageSize = 4_194_304,
                MaxBufferSize = 65_535,
                ChannelLifetime = 300_000,
                SecurityTokenLifetime = 3_600_000
            },
            ClientConfiguration = new ClientConfiguration
            {
                DefaultSessionTimeout = (int)Math.Min(sessionTimeout, int.MaxValue),
                MinSubscriptionLifetime = 10_000
            }
        };

        await configuration.ValidateAsync(ApplicationType.Client, cancellationToken);
        await configuration.CertificateValidator.UpdateAsync(configuration, cancellationToken);
        var application = new ApplicationInstance(Telemetry)
        {
            ApplicationName = applicationName,
            ApplicationType = ApplicationType.Client,
            ApplicationConfiguration = configuration
        };
        await application.CheckApplicationInstanceCertificatesAsync(true, null, cancellationToken);
        return configuration;
    }

    private static async Task<ConfiguredEndpoint> BuildConfiguredEndpointAsync(
        ApplicationConfiguration configuration,
        IReadOnlyDictionary<string, string?> settings,
        string endpointUrl,
        int timeout,
        CancellationToken cancellationToken)
    {
        var useSecurity = Boolean(settings, "useSecurity", false);
        var securityMode = ResolveSecurityMode(settings);
        var securityPolicy = ResolveSecurityPolicy(settings);
        var endpointDescription = await ResolveEndpointDescriptionAsync(configuration, endpointUrl, useSecurity, timeout, cancellationToken);

        if (securityMode.HasValue)
        {
            endpointDescription.SecurityMode = securityMode.Value;
        }

        if (!string.IsNullOrWhiteSpace(securityPolicy))
        {
            endpointDescription.SecurityPolicyUri = securityPolicy;
        }

        return new ConfiguredEndpoint(null, endpointDescription, EndpointConfiguration.Create(configuration));
    }

    private static async Task<EndpointDescription> ResolveEndpointDescriptionAsync(
        ApplicationConfiguration configuration,
        string endpointUrl,
        bool useSecurity,
        int timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CoreClientUtils.SelectEndpointAsync(configuration, endpointUrl, useSecurity, timeout, Telemetry, cancellationToken)
                ?? new EndpointDescription(endpointUrl);
        }
        catch
        {
            return new EndpointDescription(endpointUrl)
            {
                SecurityMode = useSecurity ? MessageSecurityMode.SignAndEncrypt : MessageSecurityMode.None,
                SecurityPolicyUri = useSecurity ? SecurityPolicies.Basic256Sha256 : SecurityPolicies.None
            };
        }
    }

    private static MessageSecurityMode? ResolveSecurityMode(IReadOnlyDictionary<string, string?> settings)
    {
        if (!settings.TryGetValue("securityMode", out var value) || string.IsNullOrWhiteSpace(value) || string.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Enum.TryParse<MessageSecurityMode>(value, true, out var mode)
            ? mode
            : throw new InvalidOperationException($"不支持的 OPC UA 安全模式“{value}”。");
    }

    private static string? ResolveSecurityPolicy(IReadOnlyDictionary<string, string?> settings)
    {
        if (!settings.TryGetValue("securityPolicy", out var value) || string.IsNullOrWhiteSpace(value) || string.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith(SecurityPolicies.BaseUri, StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        return normalized.ToLowerInvariant() switch
        {
            "none" => SecurityPolicies.None,
            "basic256sha256" => SecurityPolicies.Basic256Sha256,
            "aes128_sha256_rsaoaep" => SecurityPolicies.Aes128_Sha256_RsaOaep,
            "aes256_sha256_rsapss" => SecurityPolicies.Aes256_Sha256_RsaPss,
            _ => throw new InvalidOperationException($"不支持的 OPC UA 安全策略“{value}”。")
        };
    }

    private static IUserIdentity BuildIdentity(IReadOnlyDictionary<string, string?> settings)
    {
        var username = settings.TryGetValue("username", out var userValue) ? userValue : null;
        if (string.IsNullOrWhiteSpace(username))
        {
            return new UserIdentity();
        }

        var password = settings.TryGetValue("password", out var passwordValue) ? passwordValue : string.Empty;
        return new UserIdentity(username, Encoding.UTF8.GetBytes(password ?? string.Empty));
    }

    private static object? NormalizeOpcUaValue(object? value, GatewayDataType dataType)
        => value switch
        {
            null => null,
            Variant variant => NormalizeOpcUaValue(variant.Value, dataType),
            _ => dataType switch
            {
                GatewayDataType.Boolean => Convert.ToBoolean(value),
                GatewayDataType.Byte => Convert.ToByte(value, CultureInfo.InvariantCulture),
                GatewayDataType.Int16 => Convert.ToInt16(value, CultureInfo.InvariantCulture),
                GatewayDataType.UInt16 => Convert.ToUInt16(value, CultureInfo.InvariantCulture),
                GatewayDataType.Int32 => Convert.ToInt32(value, CultureInfo.InvariantCulture),
                GatewayDataType.UInt32 => Convert.ToUInt32(value, CultureInfo.InvariantCulture),
                GatewayDataType.Int64 => Convert.ToInt64(value, CultureInfo.InvariantCulture),
                GatewayDataType.UInt64 => Convert.ToUInt64(value, CultureInfo.InvariantCulture),
                GatewayDataType.Float => Convert.ToSingle(value, CultureInfo.InvariantCulture),
                GatewayDataType.Double => Convert.ToDouble(value, CultureInfo.InvariantCulture),
                GatewayDataType.String => Convert.ToString(value) ?? string.Empty,
                _ => value
            }
        };

    private static object? CoerceWriteValue(object? value, GatewayDataType dataType)
        => value is null
            ? null
            : dataType switch
            {
                GatewayDataType.Boolean => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
                GatewayDataType.Byte => Convert.ToByte(value, CultureInfo.InvariantCulture),
                GatewayDataType.Int16 => Convert.ToInt16(value, CultureInfo.InvariantCulture),
                GatewayDataType.UInt16 => Convert.ToUInt16(value, CultureInfo.InvariantCulture),
                GatewayDataType.Int32 => Convert.ToInt32(value, CultureInfo.InvariantCulture),
                GatewayDataType.UInt32 => Convert.ToUInt32(value, CultureInfo.InvariantCulture),
                GatewayDataType.Int64 => Convert.ToInt64(value, CultureInfo.InvariantCulture),
                GatewayDataType.UInt64 => Convert.ToUInt64(value, CultureInfo.InvariantCulture),
                GatewayDataType.Float => Convert.ToSingle(value, CultureInfo.InvariantCulture),
                GatewayDataType.Double => Convert.ToDouble(value, CultureInfo.InvariantCulture),
                GatewayDataType.String => Convert.ToString(value) ?? string.Empty,
                _ => value
            };

    private static DateTimeOffset ResolveOpcUaTimestamp(DataValue value)
    {
        var timestamp = value.SourceTimestamp != DateTime.MinValue ? value.SourceTimestamp : value.ServerTimestamp;
        return timestamp == DateTime.MinValue
            ? DateTimeOffset.UtcNow
            : new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));
    }

    private static string DescribeStatus(StatusCode status)
        => $"OPC UA 状态码 0x{status.Code:X8}。";

    private sealed class AsyncSession : IAsyncDisposable
    {
        private readonly ISession _session;

        public AsyncSession(ISession session)
        {
            _session = session;
        }

        public Task<ReadResponse> ReadAsync(
            RequestHeader? requestHeader,
            double maxAge,
            TimestampsToReturn timestampsToReturn,
            ReadValueIdCollection nodesToRead,
            CancellationToken cancellationToken)
            => _session.ReadAsync(requestHeader, maxAge, timestampsToReturn, nodesToRead, cancellationToken);

        public Task<WriteResponse> WriteAsync(
            RequestHeader? requestHeader,
            WriteValueCollection nodesToWrite,
            CancellationToken cancellationToken)
            => _session.WriteAsync(requestHeader, nodesToWrite, cancellationToken);

        public ValueTask DisposeAsync()
        {
            _session.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
