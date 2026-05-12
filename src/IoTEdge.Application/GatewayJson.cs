namespace IoTEdge.Application;

/// <summary>
/// 网关 JSON 辅助工具。
/// 用于统一处理配置字典、数组和常用取值转换。
/// </summary>
public static class GatewayJson
{
    /// <summary>
    /// 解析 JSON 字符串为大小写不敏感的配置字典。
    /// </summary>
    public static IReadOnlyDictionary<string, string?> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new Dictionary<string, string?>();
    }

    /// <summary>
    /// 将配置字典序列化为 JSON。
    /// </summary>
    public static string Serialize(IReadOnlyDictionary<string, string?> values)
        => JsonSerializer.Serialize(values);

    /// <summary>
    /// 从配置字典中读取 decimal 值。
    /// </summary>
    public static decimal? GetDecimal(IReadOnlyDictionary<string, string?> values, string key)
        => values.TryGetValue(key, out var value) && decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// 从配置字典中读取 int 值。
    /// </summary>
    public static int? GetInt32(IReadOnlyDictionary<string, string?> values, string key)
        => values.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// 从配置字典中读取字符串值。
    /// </summary>
    public static string? Get(IReadOnlyDictionary<string, string?> values, string key)
        => values.TryGetValue(key, out var value) ? value : null;

    /// <summary>
    /// 解析 JSON 数组为 Guid 集合。
    /// </summary>
    public static IReadOnlyCollection<Guid> ParseGuidArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<Guid>();
        }

        try
        {
            var values = JsonSerializer.Deserialize<Guid[]>(json);
            return values?.Where(value => value != Guid.Empty).Distinct().ToArray() ?? Array.Empty<Guid>();
        }
        catch (JsonException)
        {
            return Array.Empty<Guid>();
        }
    }
}
