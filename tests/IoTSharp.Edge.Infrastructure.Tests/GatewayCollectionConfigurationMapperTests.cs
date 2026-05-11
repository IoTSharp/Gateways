using IoTSharp.Edge;
using IoTSharp.Edge.Application;

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
}
