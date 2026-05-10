using System.Collections.ObjectModel;

namespace IoTSharp.Gateways.BasicRuntime;

public delegate object? BasicNativeFunction(BasicRuntimeContext context, IReadOnlyList<object?> arguments);

public sealed class BasicRuntime
{
    private readonly Dictionary<string, InternalBasicFunction> _functions = new(StringComparer.OrdinalIgnoreCase);

    public BasicRuntime()
    {
        BuiltInFunctions.Register(this);
    }

    public void RegisterFunction(string name, BasicNativeFunction function)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(function);

        _functions[name.Trim()] = (context, arguments) =>
        {
            var publicArguments = arguments.Select(value => value.ToObject()).ToArray();
            var result = function(new BasicRuntimeContext(context), publicArguments);
            return BasicValue.FromObject(result);
        };
    }

    public BasicRuntimeResult Execute(
        string source,
        IReadOnlyDictionary<string, object?>? variables = null,
        BasicRuntimeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var program = BasicProgram.Parse(source);
        var execution = new BasicExecution(this, options ?? BasicRuntimeOptions.Default);
        var globals = new Dictionary<string, BasicValue>(StringComparer.OrdinalIgnoreCase);
        if (variables is not null)
        {
            foreach (var pair in variables)
            {
                globals[pair.Key] = BasicValue.FromObject(pair.Value);
            }
        }

        var context = ExecutionContext.CreateRoot(program, execution, globals);
        var returnValue = BasicProgramRunner.Execute(context, 0, program.Statements.Count);
        var output = execution.Output.ToArray();
        var publicVariables = globals.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToObject(),
            StringComparer.OrdinalIgnoreCase);

        return new BasicRuntimeResult(returnValue.ToObject(), new ReadOnlyDictionary<string, object?>(publicVariables), output);
    }

    public object? Evaluate(
        string expression,
        IReadOnlyDictionary<string, object?>? variables = null,
        BasicRuntimeOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        return Execute("RETURN " + expression, variables, options).ReturnValue;
    }

    internal void RegisterInternalFunction(string name, InternalBasicFunction function)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(function);

        _functions[name.Trim()] = function;
    }

    internal bool TryGetFunction(string name, out InternalBasicFunction function)
        => _functions.TryGetValue(name, out function!);
}

public sealed record BasicRuntimeResult(
    object? ReturnValue,
    IReadOnlyDictionary<string, object?> Variables,
    IReadOnlyList<string> Output);

public sealed class BasicRuntimeOptions
{
    public static BasicRuntimeOptions Default { get; } = new();

    public int MaxStatements { get; init; } = 100_000;

    public int MaxLoopIterations { get; init; } = 100_000;

    public Func<string, string?>? InputProvider { get; init; }

    public Action<string>? OutputWriter { get; init; }
}

public sealed class BasicRuntimeContext
{
    private readonly ExecutionContext _context;

    internal BasicRuntimeContext(ExecutionContext context)
    {
        _context = context;
    }

    public object? GetVariable(string name)
        => _context.GetVariable(name).ToObject();

    public void SetVariable(string name, object? value)
        => _context.SetVariable(name, BasicValue.FromObject(value));
}

public sealed class BasicRuntimeException : Exception
{
    public BasicRuntimeException(string message)
        : base(message)
    {
    }

    public BasicRuntimeException(string message, int line, int column)
        : base($"{message} (line {line}, column {column})")
    {
        Line = line;
        Column = column;
    }

    public int? Line { get; }

    public int? Column { get; }
}

internal delegate BasicValue InternalBasicFunction(ExecutionContext context, IReadOnlyList<BasicValue> arguments);

internal sealed class BasicExecution
{
    private int _statementCount;
    private int _loopCount;

    public BasicExecution(BasicRuntime runtime, BasicRuntimeOptions options)
    {
        Runtime = runtime;
        Options = options;
    }

    public BasicRuntime Runtime { get; }

    public BasicRuntimeOptions Options { get; }

    public List<string> Output { get; } = [];

    public void CountStatement(Token token)
    {
        if (++_statementCount > Options.MaxStatements)
        {
            throw new BasicRuntimeException("BASIC statement budget exceeded.", token.Line, token.Column);
        }
    }

    public void CountLoop(Token token)
    {
        if (++_loopCount > Options.MaxLoopIterations)
        {
            throw new BasicRuntimeException("BASIC loop budget exceeded.", token.Line, token.Column);
        }
    }

    public void Write(string text)
    {
        Output.Add(text);
        Options.OutputWriter?.Invoke(text);
    }
}
