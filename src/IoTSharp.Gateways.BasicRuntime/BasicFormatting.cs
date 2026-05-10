namespace IoTSharp.Gateways.BasicRuntime;

internal static class BasicValueFormatter
{
    public static string ToDisplayString(ExecutionContext context, BasicValue value)
    {
        return value.Kind switch
        {
            BasicValueKind.Class or BasicValueKind.Instance => TryInvokeToString(context, value) ?? value.AsString(),
            BasicValueKind.Callable => value.Callable.DebugName,
            _ => value.AsString()
        };
    }

    private static string? TryInvokeToString(ExecutionContext context, BasicValue value)
    {
        if (value.Kind is not (BasicValueKind.Class or BasicValueKind.Instance))
        {
            return null;
        }

        if (!value.ObjectValue.TryGetMember("to_string", context, out var method) || method.Kind != BasicValueKind.Callable)
        {
            return null;
        }

        var result = method.Callable.Invoke(context, []);
        return result.Kind == BasicValueKind.Nil ? string.Empty : result.AsString();
    }
}
