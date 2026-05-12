using System.Globalization;

namespace IoTEdge.BasicRuntime;

internal static class BuiltInFunctions
{
    public static void Register(BasicRuntime runtime)
    {
        runtime.RegisterInternalFunction("VAL", (_, args) => BasicValue.FromNumber(ParseNumber(Arg(args, 0).AsString())));
        runtime.RegisterInternalFunction("STR", (context, args) => BasicValue.FromString(BasicValueFormatter.ToDisplayString(context, Arg(args, 0))));
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
        runtime.RegisterInternalFunction("DICT", (_, _) => BasicValue.FromDictionary(new BasicDictionary()));
        runtime.RegisterInternalFunction("PUSH", (_, args) => Push(args));
        runtime.RegisterInternalFunction("POP", (_, args) => Pop(args));
        runtime.RegisterInternalFunction("INSERT", (_, args) => Insert(args));
        runtime.RegisterInternalFunction("REMOVE", (_, args) => Remove(args));
        runtime.RegisterInternalFunction("COUNT", (_, args) => BasicValue.FromNumber(Length(Arg(args, 0))));
        runtime.RegisterInternalFunction("EXISTS", (_, args) => BasicValue.FromBoolean(Exists(args)));
        runtime.RegisterInternalFunction("CLEAR", (_, args) => Clear(args));
        runtime.RegisterInternalFunction("ITERATOR", (_, args) => BasicValue.FromIterator(CreateIterator(Arg(args, 0))));
        runtime.RegisterInternalFunction("MOVE_NEXT", (_, args) => BasicValue.FromBoolean(MoveNext(Arg(args, 0))));
        runtime.RegisterInternalFunction("GET", (_, args) => GetIteratorValue(Arg(args, 0)));
        runtime.RegisterInternalFunction("OS", (_, _) => BasicValue.FromString(GetOsName()));
        runtime.RegisterInternalFunction("SYS", (_, args) => Sys(args));
        runtime.RegisterInternalFunction("TYPE", (_, args) => BasicValue.FromString(TypeName(Arg(args, 0))));
        MqttBuiltInFunctions.Register(runtime);
        SerialBuiltInFunctions.Register(runtime);
        ModbusBuiltInFunctions.Register(runtime);
        PlcBuiltInFunctions.Register(runtime);
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
        return count == 0 ? BasicValue.FromString(string.Empty) : BasicValue.FromString(text[^count..]);
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

    private static bool Exists(IReadOnlyList<BasicValue> args)
    {
        var collection = Arg(args, 0);
        var needle = Arg(args, 1);
        return collection.Kind switch
        {
            BasicValueKind.List => collection.List.Items.Any(item => item.Equals(needle)),
            BasicValueKind.Array => collection.Array.ToObjectArray().Select(BasicValue.FromObject).Any(item => item.Equals(needle)),
            BasicValueKind.Dictionary => collection.Dictionary.Exists(needle.AsString()),
            BasicValueKind.Instance or BasicValueKind.Class => collection.ObjectValue.HasMember(needle.AsString()),
            _ => false
        };
    }

    private static BasicValue Clear(IReadOnlyList<BasicValue> args)
    {
        var value = Arg(args, 0);
        switch (value.Kind)
        {
            case BasicValueKind.List:
                value.List.Items.Clear();
                return BasicValue.Nil;
            case BasicValueKind.Dictionary:
                value.Dictionary.Clear();
                return BasicValue.Nil;
            case BasicValueKind.Iterator:
                value.Iterator.Reset();
                return BasicValue.Nil;
            default:
                return BasicValue.Nil;
        }
    }

    private static BasicIterator CreateIterator(BasicValue value)
    {
        return value.Kind switch
        {
            BasicValueKind.List => new BasicIterator(value.List.Items),
            BasicValueKind.Array => new BasicIterator(value.Array.ToObjectArray().Select(BasicValue.FromObject)),
            BasicValueKind.Dictionary => new BasicIterator(value.Dictionary.Keys.Select(key => BasicValue.FromString(key))),
            _ => new BasicIterator(Array.Empty<BasicValue>())
        };
    }

    private static bool MoveNext(BasicValue iteratorValue)
        => iteratorValue.Kind == BasicValueKind.Iterator && iteratorValue.Iterator.MoveNext();

    private static BasicValue GetIteratorValue(BasicValue iteratorValue)
        => iteratorValue.Kind == BasicValueKind.Iterator ? iteratorValue.Iterator.Current : BasicValue.Nil;

    private static BasicValue Sys(IReadOnlyList<BasicValue> args)
    {
        if (args.Count == 0)
        {
            return BasicValue.Nil;
        }

        var command = Arg(args, 0).AsString();
        if (string.IsNullOrWhiteSpace(command))
        {
            return BasicValue.Nil;
        }

        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? (Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe") : "/bin/sh",
            Arguments = OperatingSystem.IsWindows() ? "/c " + command : "-c \"" + command.Replace("\"", "\\\"") + "\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process?.WaitForExit();
        return BasicValue.FromNumber(process?.ExitCode ?? -1);
    }

    private static BasicValue Arg(IReadOnlyList<BasicValue> args, int index)
        => index >= 0 && index < args.Count ? args[index] : BasicValue.Nil;

    private static BasicList ExpectList(IReadOnlyList<BasicValue> args, int index)
    {
        var value = Arg(args, index);
        if (value.Kind != BasicValueKind.List)
        {
            throw new BasicRuntimeException("需要 LIST 值。");
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
            BasicValueKind.Dictionary => value.Dictionary.Count,
            BasicValueKind.Iterator => value.Iterator.Index + 1,
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
            BasicValueKind.Dictionary => "DICT",
            BasicValueKind.Iterator => "ITERATOR",
            BasicValueKind.Class => value.ObjectValue.DisplayName,
            BasicValueKind.Instance => value.ObjectValue.DisplayName,
            BasicValueKind.Callable => "CALLABLE",
            _ => "UNKNOWN"
        };
    }

    private static string GetOsName()
        => OperatingSystem.IsWindows() ? "WINDOWS" : OperatingSystem.IsLinux() ? "LINUX" : OperatingSystem.IsMacOS() ? "MACOS" : "UNKNOWN";
}
