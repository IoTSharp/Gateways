namespace IoTEdge.Application;

/// <summary>
/// 值转换服务。
/// 负责按转换规则对原始数据进行缩放、偏移、类型转换、按位提取和表达式处理。
/// </summary>
public sealed class ValueTransformationService
{
    private readonly MyBasicRuntime _basicRuntime;

    public ValueTransformationService()
        : this(new MyBasicRuntime())
    {
    }

    public ValueTransformationService(MyBasicRuntime basicRuntime)
    {
        _basicRuntime = basicRuntime;
    }

    public object? Apply(object? rawValue, IReadOnlyCollection<TransformRule> rules)
    {
        object? current = rawValue;
        foreach (var rule in rules.Where(x => x.Enabled).OrderBy(x => x.SortOrder))
        {
            current = ApplyRule(current, rule);
        }

        return current;
    }

    private object? ApplyRule(object? value, TransformRule rule)
    {
        if (value is null)
        {
            return null;
        }

        var arguments = GatewayJson.Parse(rule.ArgumentsJson);
        return rule.Kind switch
        {
            TransformationKind.Scale => Scale(value, GatewayJson.GetDecimal(arguments, "factor") ?? 1m),
            TransformationKind.Offset => Offset(value, GatewayJson.GetDecimal(arguments, "offset") ?? 0m),
            TransformationKind.Cast => Cast(value, GatewayJson.Get(arguments, "type")),
            TransformationKind.BitExtract => BitExtract(value, GatewayJson.GetInt32(arguments, "index") ?? 0),
            TransformationKind.EnumMap => arguments.TryGetValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, out var mapped) ? mapped : value,
            TransformationKind.Expression => ApplyExpression(value, arguments),
            _ => value
        };
    }

    private object? ApplyExpression(object value, IReadOnlyDictionary<string, string?> arguments)
    {
        var code = GatewayJson.Get(arguments, "expression")
            ?? GatewayJson.Get(arguments, "script")
            ?? GatewayJson.Get(arguments, "code");
        if (string.IsNullOrWhiteSpace(code))
        {
            return value;
        }

        var variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["value"] = value,
            ["raw"] = value,
            ["x"] = value
        };

        foreach (var pair in arguments)
        {
            if (!IsExpressionKey(pair.Key))
            {
                variables[pair.Key] = pair.Value;
            }
        }

        var options = new BasicRuntimeOptions
        {
            MaxStatements = 10_000,
            MaxLoopIterations = 10_000
        };

        if (LooksLikeScript(code))
        {
            var result = _basicRuntime.Execute(code, variables, options);
            if (result.ReturnValue is not null)
            {
                return result.ReturnValue;
            }

            if (result.Variables.TryGetValue("result", out var transformed))
            {
                return transformed;
            }

            return result.Variables.TryGetValue("value", out var updatedValue) ? updatedValue : value;
        }

        return _basicRuntime.Evaluate(code, variables, options);
    }

    private static object? Scale(object value, decimal factor)
        => TryGetDecimal(value, out var decimalValue) ? decimalValue * factor : value;

    private static object? Offset(object value, decimal offset)
        => TryGetDecimal(value, out var decimalValue) ? decimalValue + offset : value;

    private static object? BitExtract(object value, int index)
    {
        if (!TryGetDecimal(value, out var decimalValue))
        {
            return value;
        }

        var integerValue = (long)decimalValue;
        return (integerValue & (1L << index)) != 0;
    }

    private static object? Cast(object value, string? targetType)
        => targetType?.ToLowerInvariant() switch
        {
            "string" => Convert.ToString(value, CultureInfo.InvariantCulture),
            "boolean" => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
            "byte" => Convert.ToByte(value, CultureInfo.InvariantCulture),
            "int16" => Convert.ToInt16(value, CultureInfo.InvariantCulture),
            "uint16" => Convert.ToUInt16(value, CultureInfo.InvariantCulture),
            "int32" => Convert.ToInt32(value, CultureInfo.InvariantCulture),
            "uint32" => Convert.ToUInt32(value, CultureInfo.InvariantCulture),
            "int64" => Convert.ToInt64(value, CultureInfo.InvariantCulture),
            "uint64" => Convert.ToUInt64(value, CultureInfo.InvariantCulture),
            "float" => Convert.ToSingle(value, CultureInfo.InvariantCulture),
            "double" => Convert.ToDouble(value, CultureInfo.InvariantCulture),
            _ => value
        };

    private static bool IsExpressionKey(string key)
        => string.Equals(key, "expression", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "script", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "code", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "value", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeScript(string code)
    {
        if (code.Contains('\n') || code.Contains('\r'))
        {
            return true;
        }

        var trimmed = code.TrimStart();
        var firstSpace = trimmed.IndexOfAny([' ', '\t', '(']);
        var firstWord = firstSpace >= 0 ? trimmed[..firstSpace] : trimmed;
        return firstWord.Equals("LET", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("IF", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("FOR", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("WHILE", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("DO", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("DEF", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("DIM", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("RETURN", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("PRINT", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("INPUT", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("GOTO", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("GOSUB", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("END", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetDecimal(object value, out decimal decimalValue)
    {
        switch (value)
        {
            case byte byteValue:
                decimalValue = byteValue;
                return true;
            case short shortValue:
                decimalValue = shortValue;
                return true;
            case ushort ushortValue:
                decimalValue = ushortValue;
                return true;
            case int intValue:
                decimalValue = intValue;
                return true;
            case uint uintValue:
                decimalValue = uintValue;
                return true;
            case long longValue:
                decimalValue = longValue;
                return true;
            case ulong ulongValue:
                decimalValue = ulongValue;
                return true;
            case float floatValue:
                decimalValue = (decimal)floatValue;
                return true;
            case double doubleValue:
                decimalValue = (decimal)doubleValue;
                return true;
            case decimal directValue:
                decimalValue = directValue;
                return true;
            default:
                return decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out decimalValue);
        }
    }
}
