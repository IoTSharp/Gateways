namespace IoTEdge.BasicRuntime;

internal interface IBasicCallable
{
    string DebugName { get; }
    BasicValue Invoke(ExecutionContext context, IReadOnlyList<BasicValue> arguments);
}

internal sealed class BasicDictionary
{
    private readonly Dictionary<string, BasicValue> _entries = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _entries.Count;

    public BasicValue Get(string key)
        => _entries.TryGetValue(key, out var value) ? value : BasicValue.Nil;

    public void Set(string key, BasicValue value)
        => _entries[key] = value;

    public bool Exists(string key)
        => _entries.ContainsKey(key);

    public bool Remove(string key)
        => _entries.Remove(key);

    public void Clear()
        => _entries.Clear();

    public IReadOnlyCollection<string> Keys
        => _entries.Keys.ToArray();

    public BasicDictionary CloneDeep()
    {
        var clone = new BasicDictionary();
        foreach (var pair in _entries)
        {
            clone._entries[pair.Key] = pair.Value.CloneDeep();
        }

        return clone;
    }
}

internal sealed class BasicIterator
{
    private readonly IReadOnlyList<BasicValue> _items;
    private int _index = -1;

    public BasicIterator(IEnumerable<BasicValue> items)
    {
        _items = items.ToArray();
    }

    public int Index => _index;

    public bool IsExhausted => _index >= _items.Count - 1;

    public BasicValue Current
        => _index >= 0 && _index < _items.Count ? _items[_index] : BasicValue.Nil;

    public bool MoveNext()
    {
        if (_index + 1 >= _items.Count)
        {
            _index = _items.Count;
            return false;
        }

        _index++;
        return true;
    }

    public void Reset()
        => _index = -1;

    public BasicIterator CloneDeep()
    {
        var clone = new BasicIterator(_items.Select(item => item.CloneDeep()));
        clone._index = _index;
        return clone;
    }
}

internal sealed class BasicObjectValue
{
    private readonly Dictionary<string, BasicValue> _fields = new(StringComparer.OrdinalIgnoreCase);

    public BasicObjectValue(ClassDefinition definition, bool isPrototype)
    {
        Definition = definition;
        IsPrototype = isPrototype;
    }

    public ClassDefinition Definition { get; }

    public bool IsPrototype { get; }

    public string DisplayName => Definition.Name;

    public IReadOnlyDictionary<string, BasicValue> Fields => _fields;

    public bool HasField(string name)
        => _fields.ContainsKey(name);

    public bool TryGetMember(string name, ExecutionContext context, out BasicValue value)
    {
        if (_fields.TryGetValue(name, out value))
        {
            return true;
        }

        var method = FindMethod(name);
        if (method is not null)
        {
            value = BasicValue.FromCallable(new BasicBoundMethodValue(this, method));
            return true;
        }

        value = BasicValue.Nil;
        return false;
    }

    public void SetMember(string name, BasicValue value)
        => _fields[name] = value;

    public bool HasMember(string name)
        => _fields.ContainsKey(name) || FindMethod(name) is not null;

    public BasicObjectValue CloneDeepPrototype()
    {
        var clone = new BasicObjectValue(Definition, true);
        foreach (var pair in _fields)
        {
            clone._fields[pair.Key] = pair.Value.CloneDeep();
        }

        return clone;
    }

    public BasicObjectValue CloneDeepInstance()
    {
        var clone = new BasicObjectValue(Definition, false);
        foreach (var pair in _fields)
        {
            clone._fields[pair.Key] = pair.Value.CloneDeep();
        }

        return clone;
    }

    public BasicObjectValue CreateInstance()
        => CloneDeepInstance();

    public void InitializeFields(ExecutionContext context)
    {
        if (Definition.ParentName is not null && context.TryGetValue(Definition.ParentName, out var parentValue)
            && parentValue.Kind is BasicValueKind.Class or BasicValueKind.Instance)
        {
            Definition.ParentDefinition = parentValue.ObjectValue.Definition;
            foreach (var pair in parentValue.ObjectValue._fields)
            {
                _fields[pair.Key] = pair.Value.CloneDeep();
            }
        }

        foreach (var field in Definition.Fields)
        {
            var value = field.InitializerTokens.Count == 0
                ? BasicValue.Nil
                : ExpressionParser.Evaluate(field.InitializerTokens, context);
            _fields[field.Name] = value;
        }
    }

    private MethodDefinition? FindMethod(string name)
    {
        if (Definition.Methods.TryGetValue(name, out var method))
        {
            return method;
        }

        if (Definition.ParentDefinition is not null)
        {
            return FindMethodInDefinition(Definition.ParentDefinition, name);
        }

        return null;
    }

    private static MethodDefinition? FindMethodInDefinition(ClassDefinition definition, string name)
    {
        if (definition.Methods.TryGetValue(name, out var method))
        {
            return method;
        }

        return definition.ParentDefinition is not null
            ? FindMethodInDefinition(definition.ParentDefinition, name)
            : null;
    }
}

internal sealed class BasicBoundMethodValue : IBasicCallable
{
    public BasicBoundMethodValue(BasicObjectValue target, MethodDefinition method)
    {
        Target = target;
        Method = method;
    }

    public BasicObjectValue Target { get; }

    public MethodDefinition Method { get; }

    public string DebugName => $"{Target.DisplayName}.{Method.Name}";

    public BasicValue Invoke(ExecutionContext context, IReadOnlyList<BasicValue> arguments)
        => BasicProgramRunner.ExecuteMethod(context, Target, Method, arguments);

    public override string ToString()
        => DebugName;
}

internal sealed class BasicLambdaValue : IBasicCallable
{
    public BasicLambdaValue(IReadOnlyList<string> parameters, bool isVariadic, int bodyStart, int bodyEnd, ExecutionContext closure, string debugName = "lambda")
    {
        Parameters = parameters;
        IsVariadic = isVariadic;
        BodyStart = bodyStart;
        BodyEnd = bodyEnd;
        Closure = closure;
        DebugName = debugName;
    }

    public IReadOnlyList<string> Parameters { get; }

    public bool IsVariadic { get; }

    public int BodyStart { get; }

    public int BodyEnd { get; }

    public ExecutionContext Closure { get; }

    public string DebugName { get; }

    public BasicValue Invoke(ExecutionContext context, IReadOnlyList<BasicValue> arguments)
        => BasicProgramRunner.ExecuteLambda(context, this, arguments);

    public override string ToString()
        => DebugName;
}
