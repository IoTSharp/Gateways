using IoTSharp.Edge.Domain;
using IoTSharp.Edge.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IoTSharp.Edge.Infrastructure.Tests;

public sealed class GatewayInfrastructureDriverTests
{
    [Fact]
    public void Infrastructure_registers_expanded_driver_catalog()
    {
        var services = new ServiceCollection();
        services.AddGatewayInfrastructure(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var registry = provider.GetRequiredService<IDeviceDriverRegistry>();
        var metadata = registry.GetMetadata().ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);

        Assert.Contains("opc-ua", metadata.Keys);
        Assert.Contains("opc-da", metadata.Keys);
        Assert.Contains("mt-cnc", metadata.Keys);
        Assert.Contains("fanuc-cnc", metadata.Keys);
        Assert.Equal(DriverType.OpcUa, metadata["opc-ua"].DriverType);
        Assert.Equal(DriverType.MtCnc, metadata["mt-cnc"].DriverType);
        Assert.Equal("high", metadata["opc-da"].RiskLevel);
        Assert.Equal("high", metadata["fanuc-cnc"].RiskLevel);
    }

    [Fact]
    public async Task OpcUa_driver_validates_node_id_format()
    {
        var services = new ServiceCollection();
        services.AddGatewayInfrastructure(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var driver = provider.GetRequiredService<IDeviceDriverRegistry>().GetRequiredDriver("opc-ua");
        var valid = await driver.ValidateAddressAsync(new DriverReadRequest("ns=2;s=Device.Temperature", GatewayDataType.Double), CancellationToken.None);
        var invalid = await driver.ValidateAddressAsync(new DriverReadRequest("not a node id", GatewayDataType.Double), CancellationToken.None);

        Assert.True(valid.IsValid);
        Assert.False(invalid.IsValid);
    }

    [Fact]
    public async Task MtConnect_driver_reports_read_only_writes()
    {
        var services = new ServiceCollection();
        services.AddGatewayInfrastructure(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var driver = provider.GetRequiredService<IDeviceDriverRegistry>().GetRequiredDriver("mt-cnc");
        var result = await driver.WriteAsync(
            new DriverConnectionContext("mt-cnc", new Dictionary<string, string?> { ["baseUrl"] = "http://127.0.0.1:5000" }),
            new DriverWriteRequest("avail", GatewayDataType.String, "AVAILABLE"),
            CancellationToken.None);

        Assert.Equal(QualityStatus.Bad, result.Quality);
        Assert.Contains("read-only", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
