namespace IoTSharp.Edge;

internal static class GatewayCollectionConfigurationValidator
{
    public static void ValidateStructuralKeys(EdgeCollectionConfigurationContract configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var taskKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var task in configuration.Tasks ?? [])
        {
            var taskKey = RequireKey(task.TaskKey, "task.taskKey 为必填项。");
            if (!taskKeys.Add(taskKey))
            {
                throw new InvalidOperationException($"采集任务键“{taskKey}”重复。");
            }

            var deviceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var device in task.Devices ?? [])
            {
                var deviceKey = RequireKey(device.DeviceKey, $"任务“{taskKey}”包含未设置 deviceKey 的设备。");
                if (!deviceKeys.Add(deviceKey))
                {
                    throw new InvalidOperationException($"任务“{taskKey}”中设备键“{deviceKey}”重复。");
                }

                var pointKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var point in device.Points ?? [])
                {
                    var pointKey = RequireKey(point.PointKey, $"任务“{taskKey}”、设备“{deviceKey}”包含未设置 pointKey 的点位。");
                    if (!pointKeys.Add(pointKey))
                    {
                        throw new InvalidOperationException($"任务“{taskKey}”、设备“{deviceKey}”中点位键“{pointKey}”重复。");
                    }
                }
            }
        }

        var uploads = configuration.Uploads is { Count: > 0 }
            ? configuration.Uploads
            : configuration.Upload is null
                ? []
                : [configuration.Upload];

        var uploadKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var upload in uploads)
        {
            var uploadKey = upload.TargetKey?.Trim();
            if (string.IsNullOrWhiteSpace(uploadKey))
            {
                continue;
            }

            if (!uploadKeys.Add(uploadKey))
            {
                throw new InvalidOperationException($"上传目标键“{uploadKey}”重复。");
            }
        }
    }

    private static string RequireKey(string? value, string message)
    {
        var key = value?.Trim();
        return string.IsNullOrWhiteSpace(key) ? throw new InvalidOperationException(message) : key;
    }
}
