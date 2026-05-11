using IoTSharp.Edge.Application;
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
        Assert.Contains("siemens-s7", metadata.Keys);
        Assert.Contains("mitsubishi", metadata.Keys);
        Assert.Contains("omron-fins", metadata.Keys);
        Assert.Contains("allen-bradley", metadata.Keys);
        Assert.Equal(DriverType.OpcUa, metadata["opc-ua"].DriverType);
        Assert.Equal(DriverType.MtCnc, metadata["mt-cnc"].DriverType);
        Assert.Equal("high", metadata["opc-da"].RiskLevel);
        Assert.Equal("high", metadata["fanuc-cnc"].RiskLevel);
    }

    [Fact]
    public void Protocol_catalog_exposes_collection_families()
    {
        var services = new ServiceCollection();
        services.AddGatewayInfrastructure(new ConfigurationBuilder().Build());
        services.AddScoped<DriverCatalogService>();
        services.AddScoped<CollectionProtocolCatalogService>();
        using var provider = services.BuildServiceProvider();

        var catalog = provider.GetRequiredService<CollectionProtocolCatalogService>();
        var protocols = catalog.GetProtocols().ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);

        Assert.Equal("Modbus", protocols["modbus"].ContractProtocol);
        Assert.Equal("PLC", protocols["modbus"].Category);
        Assert.Equal("ready", protocols["modbus"].Lifecycle);
        Assert.Equal("PLC", protocols["siemens-s7"].Category);
        Assert.Equal("CNC", protocols["mt-cnc"].Category);
        Assert.Equal("guarded", protocols["opc-da"].Lifecycle);
        Assert.Equal("guarded", protocols["fanuc-cnc"].Lifecycle);
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
        Assert.Contains("只读", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
