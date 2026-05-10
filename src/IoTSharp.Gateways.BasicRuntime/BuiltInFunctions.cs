using System.Globalization;

namespace IoTSharp.Gateways.BasicRuntime;

internal static class BuiltInFunctions
{
    public static void Register(BasicRuntime runtime)
    {
        runtime.RegisterInternalFunction("VAL", (_, args) => BasicValue.FromNumber(ParseNumber(Arg(args, 0).AsString())));
        runtime.RegisterInternalFunction("STR", (_, args) => BasicValue.FromString(Arg(args, 0).AsString()));
        runtime.RegisterInternalFunction("LEN", (_, args) => BasicValue.FromNumber(Length(Arg(args, 0))));
        runtime.RegisterInternalFunction("MID", (_, args) => Mid(args));
        runtime.RegisterInternalFunction("LEFT", (_, args) => Left(args));
        runtime.RegisterInternalFunction("RIGHT", (_, args) => Right(args));
        runtime.RegisterInternalFunction("ASC", (_, args) => BasicValue.FromNumber(Arg(args, 0).AsString().FirstOrDefault()));
        runtime.RegisterInternalFunction("CHR", (_, args) => BasicValue.FromString(((char)(int)Arg(args, 0).AsNumber()).ToString()));
        runtime.RegisterInternalFunction("FLOOR", (_, args) => BasicValue.FromNumber(Math.Floor(Arg(args, 0).AsNumber())));
        runtime.RegisterInternalFunction("CEIL", (_, args) => BasicValue.FromNumber(Math.Ceiling(Arg(args, 0).AsNumber())));
        runtime.RegisterInternalFunction("ROUND", (_, args) => BasicValue.FromNumber(Math.Round(Arg(args, 0).AsNumber())));
        runtime.RegisterInternalFunction("ABS", (_, args) => BasicValue.FromNumber(Math.Abs(Arg(args, 0).AsNumber())));
        runtime.RegisterInternalFunction("INT", (_, args) => BasicValue.FromNumber(Math.Truncate(Arg(args, 0).AsNumber())));
        runtime.RegisterInternalFunction("FIX", (_, args) => BasicValue.FromNumber(Math.Truncate(Arg(args, 0).AsNumber())));
        runtime.RegisterInternalFunction("SQR", (_, args) => BasicValue.FromNumber(Math.Sqrt(Arg(args, 0).AsNumber())));
        runtime.RegisterInternalFunction("SQRT", (_, args) => BasicValue.FromNumber(Math.Sqrt(Arg(args, 0).AsNumber())));
        runtime.RegisterInternalFunction("SIN", (_, args) => BasicValue.FromNumber(Math.Sin(Arg(args, 0).AsNumber())));
        runtime.RegisterInternalFunction("COS", (_, args) => BasicValue.FromNumber(Math.Cos(Arg(args, 0).AsNumber())));
        runtime.RegisterInternalFunction("TAN", (_, args) => BasicValue.FromNumber(Math.Tan(Arg(args, 0).AsNumber())));
        runtime.RegisterInternalFunction("LOG", (_, args) => BasicValue.FromNumber(Math.Log(Arg(args, 0).AsNumber())));
        runtime.RegisterInternalFunction("EXP", (_, args) => BasicValue.FromNumber(Math.Exp(Arg(args, 0).AsNumber())));
        runtime.RegisterInternalFunction("MIN", (_, args) => BasicValue.FromNumber(args.Select(arg => arg.AsNumber()).DefaultIfEmpty(0).Min()));
        runtime.RegisterInternalFunction("MAX", (_, args) => BasicValue.FromNumber(args.Select(arg => arg.AsNumber()).DefaultIfEmpty(0).Max()));
        runtime.RegisterInternalFunction("RND", (_, _) => BasicValue.FromNumber(Random.Shared.NextDouble()));
        runtime.RegisterInternalFunction("LIST", (_, args) => BasicValue.FromList(new BasicList(args)));
        runtime.RegisterInternalFunction("PUSH", (_, args) => Push(args));
        runtime.RegisterInternalFunction("POP", (_, args) => Pop(args));
        runtime.RegisterInternalFunction("INSERT", (_, args) => Insert(args));
        runtime.RegisterInternalFunction("REMOVE", (_, args) => Remove(args));
        runtime.RegisterInternalFunction("COUNT", (_, args) => BasicValue.FromNumber(Length(Arg(args, 0))));
        runtime.RegisterInternalFunction("TYPE", (_, args) => BasicValue.FromString(TypeName(Arg(args, 0))));
    }

    private static BasicValue Mid(IReadOnlyList<BasicValue> args)
    {
        var text = Arg(args, 0).AsString();
        var start = ClampIndex((int)Arg(args, 1).AsNumber(), text.Length);
        var count = args.Count > 2 ? Math.Max(0, (int)args[2].AsNumber()) : text.Length - start;
        if (start >= text.Length)
        {
            return BasicValue.FromString(string.Empty);
        }

        return BasicValue.FromString(text.Substring(start, Math.Min(count, text.Length - start)));
    }

    private static BasicValue Left(IReadOnlyList<BasicValue> args)
    {
        var text = Arg(args, 0).AsString();
        var count = Math.Clamp((int)Arg(args, 1).AsNumber(), 0, text.Length);
        return BasicValue.FromString(text[..count]);
    }

    private static BasicValue Right(IReadOnlyList<BasicValue> args)
    {
        var text = Arg(args, 0).AsString();
        var count = Math.Clamp((int)Arg(args, 1).AsNumber(), 0, text.Length);
        return BasicValue.FromString(text[^count..]);
    }

    private static BasicValue Push(IReadOnlyList<BasicValue> args)
    {
        var list = ExpectList(args, 0);
        list.Items.Add(Arg(args, 1));
        return BasicValue.FromNumber(list.Items.Count);
    }

    private static BasicValue Pop(IReadOnlyList<BasicValue> args)
    {
        var list = ExpectList(args, 0);
        if (list.Items.Count == 0)
        {
            return BasicValue.Nil;
        }

        var index = list.Items.Count - 1;
        var value = list.Items[index];
        list.Items.RemoveAt(index);
        return value;
    }

    private static BasicValue Insert(IReadOnlyList<BasicValue> args)
    {
        var list = ExpectList(args, 0);
        var index = Math.Clamp((int)Arg(args, 1).AsNumber(), 0, list.Items.Count);
        list.Items.Insert(index, Arg(args, 2));
        return BasicValue.FromNumber(list.Items.Count);
    }

    private static BasicValue Remove(IReadOnlyList<BasicValue> args)
    {
        var list = ExpectList(args, 0);
        var index = (int)Arg(args, 1).AsNumber();
        if (index < 0 || index >= list.Items.Count)
        {
            return BasicValue.Nil;
        }

        var value = list.Items[index];
        list.Items.RemoveAt(index);
        return value;
    }

    private static BasicValue Arg(IReadOnlyList<BasicValue> args, int index)
        => index >= 0 && index < args.Count ? args[index] : BasicValue.Nil;

    private static BasicList ExpectList(IReadOnlyList<BasicValue> args, int index)
    {
        var value = Arg(args, index);
        if (value.Kind != BasicValueKind.List)
        {
            throw new BasicRuntimeException("LIST value expected.");
        }

        return value.List;
    }

    private static double ParseNumber(string text)
        => double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static double Length(BasicValue value)
    {
        return value.Kind switch
        {
            BasicValueKind.Nil => 0,
            BasicValueKind.Number => value.AsString().Length,
            BasicValueKind.String => value.Text.Length,
            BasicValueKind.List => value.List.Items.Count,
            BasicValueKind.Array => value.Array.Length,
            _ => 0
        };
    }

    private static int ClampIndex(int value, int length)
        => Math.Clamp(value, 0, Math.Max(0, length));

    private static string TypeName(BasicValue value)
    {
        return value.Kind switch
        {
            BasicValueKind.Nil => "NIL",
            BasicValueKind.Number => "NUMBER",
            BasicValueKind.String => "STRING",
            BasicValueKind.List => "LIST",
            BasicValueKind.Array => "ARRAY",
            _ => "UNKNOWN"
        };
    }
}
