using System.Text.Json;
using IoTSharp.Edge;
using IoTSharp.Edge.Application;
using IoTSharp.Edge.Domain;

namespace IoTSharp.Edge.Infrastructure.Tests;

public sealed class GatewayCollectionConfigurationMapperTests
{
    [Fact]
    public void Map_rejects_duplicate_task_keys()
    {
        var options = new EdgeReportingOptions
        {
            BaseUrl = "http://127.0.0.1:5000",
            AccessToken = "edge-token"
        };
        var configuration = CreateConfiguration(
            CreateTask("modbus-task", "device-01", "point-01"),
            CreateTask("modbus-task", "device-02", "point-02"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GatewayCollectionConfigurationMapper.Map(configuration, options));

        Assert.Contains("采集任务键", exception.Message);
    }

    [Fact]
    public void Map_rejects_duplicate_device_keys_within_task()
    {
        var options = new EdgeReportingOptions
        {
            BaseUrl = "http://127.0.0.1:5000",
            AccessToken = "edge-token"
        };
        var task = CreateTask("modbus-task", "device-01", "point-01");
        task = task with
        {
            Devices =
            [
                task.Devices[0],
                task.Devices[0] with { Points = [CreatePoint("point-02")] }
            ]
        };

        var configuration = CreateConfiguration(task);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GatewayCollectionConfigurationMapper.Map(configuration, options));

        Assert.Contains("设备键", exception.Message);
    }

    [Fact]
    public void Map_rejects_duplicate_point_keys_within_device()
    {
        var options = new EdgeReportingOptions
        {
            BaseUrl = "http://127.0.0.1:5000",
            AccessToken = "edge-token"
        };
        var task = CreateTask("modbus-task", "device-01", "point-01");
        task = task with
        {
            Devices =
            [
                task.Devices[0] with
                {
                    Points =
                    [
                        CreatePoint("point-01"),
                        CreatePoint("point-01")
                    ]
                }
            ]
        };

        var configuration = CreateConfiguration(task);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GatewayCollectionConfigurationMapper.Map(configuration, options));

        Assert.Contains("点位键", exception.Message);
    }

    [Fact]
    public void Map_expands_multiple_upload_targets_into_channels_and_routes()
    {
        var configuration = CreateConfiguration(
            CreateTask("modbus-task", "device-01", "point-01"));
        configuration = configuration with
        {
            Uploads =
            [
                CreateUpload(
                    "sonnetdb-main",
                    "SonnetDB 主目标",
                    "SonnetDb",
                    "http://sonnetdb:5080",
                    new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["database"] = "metrics",
                        ["measurement"] = "edge_modbus",
                        ["field"] = "value"
                    }),
                CreateUpload(
                    "iotsharp-main",
                    "IoTSharp 主目标",
                    "IoTSharp",
                    "http://iotsharp:5000",
                    new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["token"] = "iotsharp-dev-token"
                    })
            ]
        };

        var snapshot = GatewayCollectionConfigurationMapper.Map(configuration, new EdgeReportingOptions());

        Assert.Equal(2, snapshot.UploadChannels.Count);
        Assert.Equal(2, snapshot.UploadRoutes.Count);
        Assert.Contains(snapshot.UploadChannels, channel => channel.Protocol == UploadProtocol.SonnetDb);
        Assert.Contains(snapshot.UploadChannels, channel => channel.Protocol == UploadProtocol.IoTSharp);
    }

    [Fact]
    public void Map_preserves_legacy_upload_configuration()
    {
        var configuration = CreateConfiguration(CreateTask("modbus-task", "device-01", "point-01")) with
        {
            Upload = CreateUpload(
                "sonnetdb-main",
                "SonnetDB 主目标",
                "SonnetDb",
                "http://sonnetdb:5080",
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["database"] = "metrics",
                    ["measurement"] = "edge_modbus",
                    ["field"] = "value"
                })
        };

        var snapshot = GatewayCollectionConfigurationMapper.Map(configuration, new EdgeReportingOptions());

        Assert.Single(snapshot.UploadChannels);
        Assert.Single(snapshot.UploadRoutes);
        Assert.Equal(UploadProtocol.SonnetDb, snapshot.UploadChannels.Single().Protocol);
    }

    private static EdgeCollectionConfigurationContract CreateConfiguration(params CollectionTaskContract[] tasks)
    {
        return new EdgeCollectionConfigurationContract
        {
            EdgeNodeId = Guid.NewGuid(),
            Tasks = tasks
        };
    }

    private static CollectionTaskContract CreateTask(string taskKey, string deviceKey, string pointKey)
    {
        return new CollectionTaskContract
        {
            TaskKey = taskKey,
            Protocol = GatewayCollectionProtocolType.Modbus,
            Connection = new CollectionConnectionContract
            {
                ConnectionName = taskKey,
                Protocol = GatewayCollectionProtocolType.Modbus,
                Transport = "tcp",
                Host = "127.0.0.1",
                Port = 1502,
                TimeoutMs = 3000
            },
            Devices =
            [
                new CollectionDeviceContract
                {
                    DeviceKey = deviceKey,
                    DeviceName = deviceKey,
                    Points = [CreatePoint(pointKey)]
                }
            ]
        };
    }

    private static CollectionPointContract CreatePoint(string pointKey)
    {
        return new CollectionPointContract
        {
            PointKey = pointKey,
            PointName = pointKey,
            SourceType = "HoldingRegister",
            Address = "40001",
            RawValueType = "Float",
            Length = 2,
            Mapping = new PlatformMappingContract
            {
                TargetType = GatewayCollectionTargetType.Telemetry,
                TargetName = pointKey,
                ValueType = GatewayCollectionValueType.Double
            }
        };
    }

    private static CollectionUploadContract CreateUpload(
        string targetKey,
        string displayName,
        string protocol,
        string endpoint,
        IReadOnlyDictionary<string, string?> settings)
    {
        return new CollectionUploadContract
        {
            TargetKey = targetKey,
            DisplayName = displayName,
            Protocol = protocol,
            Endpoint = endpoint,
            Settings = JsonSerializer.SerializeToElement(settings, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Enabled = true,
            BatchSize = 1,
            BufferingEnabled = false
        };
    }
}
