using System.Globalization;
using System.Text;

namespace IoTEdge.BasicRuntime;

internal static class BasicFormatFunctions
{
    public static void Register(BasicRuntime runtime)
    {
        runtime.RegisterInternalFunction("FORMAT", Format);
        runtime.RegisterInternalFunction("FMT", Format);
    }

    private static BasicValue Format(ExecutionContext context, IReadOnlyList<BasicValue> args)
    {
        var template = Arg(args, 0).AsString();
        var values = args.Skip(1).ToArray();
        var text = UsesIndexedPlaceholders(template)
            ? FormatIndexed(context, template, values)
            : FormatPrintf(context, template, values);

        return BasicValue.FromString(text);
    }

    private static string FormatIndexed(ExecutionContext context, string template, IReadOnlyList<BasicValue> values)
    {
        var builder = new StringBuilder(template.Length + values.Count * 8);
        for (var index = 0; index < template.Length; index++)
        {
            var current = template[index];
            if (current == '{')
            {
                if (index + 1 < template.Length && template[index + 1] == '{')
                {
                    builder.Append('{');
                    index++;
                    continue;
                }

                var close = template.IndexOf('}', index + 1);
                if (close > index && int.TryParse(template.AsSpan(index + 1, close - index - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var argIndex))
                {
                    builder.Append(argIndex >= 0 && argIndex < values.Count
                        ? BasicValueFormatter.ToDisplayString(context, values[argIndex])
                        : string.Empty);
                    index = close;
                    continue;
                }
            }

            if (current == '}' && index + 1 < template.Length && template[index + 1] == '}')
            {
                builder.Append('}');
                index++;
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static string FormatPrintf(ExecutionContext context, string template, IReadOnlyList<BasicValue> values)
    {
        var builder = new StringBuilder(template.Length + values.Count * 8);
        var valueIndex = 0;
        for (var index = 0; index < template.Length; index++)
        {
            var current = template[index];
            if (current != '%' || index + 1 >= template.Length)
            {
                builder.Append(current);
                continue;
            }

            var specStart = index;
            index++;
            if (template[index] == '%')
            {
                builder.Append('%');
                continue;
            }

            while (index < template.Length && IsPrintfModifier(template[index]))
            {
                index++;
            }

            if (index >= template.Length)
            {
                builder.Append(template, specStart, template.Length - specStart);
                break;
            }

            var specifier = template[index];
            var value = valueIndex < values.Count ? values[valueIndex++] : BasicValue.Nil;
            switch (specifier)
            {
                case 'd':
                case 'i':
                case 'u':
                    AppendWithOptionalDotNetFormat(builder, template, specStart, index, Math.Truncate(value.AsNumber()));
                    break;
                case 'f':
                case 'F':
                case 'g':
                case 'G':
                    AppendWithOptionalDotNetFormat(builder, template, specStart, index, value.AsNumber());
                    break;
                case 's':
                    builder.Append(BasicValueFormatter.ToDisplayString(context, value));
                    break;
                default:
                    builder.Append('%').Append(specifier);
                    break;
            }
        }

        return builder.ToString();
    }

    private static void AppendWithOptionalDotNetFormat(StringBuilder builder, string template, int specStart, int specEnd, double value)
    {
        var specifier = template[specEnd];
        var precision = TryGetPrecision(template.AsSpan(specStart, specEnd - specStart + 1));
        var format = precision is >= 0
            ? specifier switch
            {
                'd' or 'i' or 'u' => "F0",
                'f' or 'F' => "F" + precision.Value.ToString(CultureInfo.InvariantCulture),
                'g' or 'G' => "G" + precision.Value.ToString(CultureInfo.InvariantCulture),
                _ => null
            }
            : specifier switch
            {
                'd' or 'i' or 'u' => "F0",
                'f' or 'F' => "F",
                'g' or 'G' => "G",
                _ => null
            };

        builder.Append(value.ToString(format, CultureInfo.InvariantCulture));
    }

    private static bool UsesIndexedPlaceholders(string template)
    {
        for (var index = 0; index < template.Length - 2; index++)
        {
            if (template[index] != '{' || !char.IsDigit(template[index + 1]))
            {
                continue;
            }

            var cursor = index + 2;
            while (cursor < template.Length && char.IsDigit(template[cursor]))
            {
                cursor++;
            }

            if (cursor < template.Length && template[cursor] == '}')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPrintfModifier(char value)
        => value is '-' or '+' or ' ' or '#' or '0' or '.' || char.IsDigit(value);

    private static int? TryGetPrecision(ReadOnlySpan<char> spec)
    {
        var dot = spec.IndexOf('.');
        if (dot < 0 || dot + 1 >= spec.Length)
        {
            return null;
        }

        var end = dot + 1;
        while (end < spec.Length && char.IsDigit(spec[end]))
        {
            end++;
        }

        return end > dot + 1 && int.TryParse(spec[(dot + 1)..end], NumberStyles.None, CultureInfo.InvariantCulture, out var precision)
            ? precision
            : null;
    }

    private static BasicValue Arg(IReadOnlyList<BasicValue> args, int index)
        => index >= 0 && index < args.Count ? args[index] : BasicValue.Nil;
}
