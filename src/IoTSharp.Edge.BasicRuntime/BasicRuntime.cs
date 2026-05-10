using System.Collections.ObjectModel;
using System.Text;

namespace IoTSharp.Edge.BasicRuntime;

public delegate object? BasicNativeFunction(BasicRuntimeContext context, IReadOnlyList<object?> arguments);

public sealed class BasicRuntime : IDisposable
{
    private readonly Dictionary<string, InternalBasicFunction> _functions = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public BasicRuntime(
        IBasicSerialPortFactory? serialPortFactory = null,
        IBasicModbusClientFactory? modbusClientFactory = null,
        IBasicPlcClientFactory? plcClientFactory = null)
    {
        SerialPortFactory = serialPortFactory ?? new SystemBasicSerialPortFactory();
        ModbusClientFactory = modbusClientFactory ?? new SystemBasicModbusClientFactory();
        PlcClientFactory = plcClientFactory ?? new SystemBasicPlcClientFactory();
        BuiltInFunctions.Register(this);
    }

    internal MqttRuntimeState MqttState { get; } = new();

    internal SerialRuntimeState SerialState { get; } = new();

    internal ModbusRuntimeState ModbusState { get; } = new();

    internal PlcRuntimeState SiemensState { get; } = new();

    internal PlcRuntimeState MitsubishiState { get; } = new();

    internal PlcRuntimeState OmronFinsState { get; } = new();

    internal PlcRuntimeState AllenBradleyState { get; } = new();

    public IBasicSerialPortFactory SerialPortFactory { get; }

    public IBasicModbusClientFactory ModbusClientFactory { get; }

    public IBasicPlcClientFactory PlcClientFactory { get; }

    public void RegisterFunction(string name, BasicNativeFunction function)
    {
        ThrowIfDisposed();
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
        ThrowIfDisposed();
        return ExecuteCore(source, null, variables, options);
    }

    public BasicRuntimeResult ExecuteFile(
        string path,
        IReadOnlyDictionary<string, object?>? variables = null,
        BasicRuntimeOptions? options = null)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var source = File.ReadAllText(fullPath);
        return ExecuteCore(source, fullPath, variables, options);
    }

    private BasicRuntimeResult ExecuteCore(
        string source,
        string? sourcePath,
        IReadOnlyDictionary<string, object?>? variables,
        BasicRuntimeOptions? options)
    {
        ArgumentNullException.ThrowIfNull(source);

        var effectiveOptions = options ?? BasicRuntimeOptions.Default;
        var expandedSource = ExpandImports(source, sourcePath, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var program = BasicProgram.Parse(expandedSource);
        var execution = new BasicExecution(this, effectiveOptions);
        var globals = new Dictionary<string, BasicValue>(StringComparer.OrdinalIgnoreCase);
        if (variables is not null)
        {
            foreach (var pair in variables)
            {
                globals[pair.Key] = BasicValue.FromObject(pair.Value);
            }
        }

        var context = ExecutionContext.CreateRoot(program, execution, globals);
        BasicProgramRunner.InitializeClasses(context);
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
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        return Execute("RETURN " + expression, variables, options).ReturnValue;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        MqttState.Dispose();
        SerialState.Dispose();
        ModbusState.Dispose();
        SiemensState.Dispose();
        MitsubishiState.Dispose();
        OmronFinsState.Dispose();
        AllenBradleyState.Dispose();
    }

    internal void RegisterInternalFunction(string name, InternalBasicFunction function)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(function);

        _functions[name.Trim()] = function;
    }

    internal bool TryGetFunction(string name, out InternalBasicFunction function)
        => _functions.TryGetValue(name, out function!);

    private static string ExpandImports(string source, string? sourcePath, HashSet<string> importedPaths)
    {
        var normalizedSource = source.Replace("\r\n", "\n").Replace('\r', '\n');
        var builder = new StringBuilder();
        var baseDirectory = string.IsNullOrWhiteSpace(sourcePath)
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? Directory.GetCurrentDirectory();

        using var reader = new StringReader(normalizedSource);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (TryParseImport(line, out var importPath))
            {
                var resolvedPath = ResolveImportPath(baseDirectory, importPath);
                if (resolvedPath is null)
                {
                    throw new BasicRuntimeException($"Unable to resolve import '{importPath}'.");
                }

                if (!importedPaths.Add(resolvedPath))
                {
                    continue;
                }

                if (!File.Exists(resolvedPath))
                {
                    throw new BasicRuntimeException($"Imported file '{resolvedPath}' does not exist.");
                }

                var importedSource = File.ReadAllText(resolvedPath);
                builder.AppendLine($"' import {Path.GetFileName(resolvedPath)}");
                builder.AppendLine(ExpandImports(importedSource, resolvedPath, importedPaths));
                continue;
            }

            builder.AppendLine(line);
        }

        return builder.ToString();
    }

    private static bool TryParseImport(string line, out string path)
    {
        var trimmed = line.Trim();
        const string keyword = "import";
        if (!trimmed.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
        {
            path = string.Empty;
            return false;
        }

        var remainder = trimmed[keyword.Length..].TrimStart();
        if (remainder.Length < 2)
        {
            path = string.Empty;
            return false;
        }

        if (remainder[0] == '"' && remainder[^1] == '"')
        {
            path = remainder[1..^1];
            return true;
        }

        path = string.Empty;
        return false;
    }

    private static string? ResolveImportPath(string baseDirectory, string importPath)
    {
        if (Path.IsPathRooted(importPath))
        {
            return Path.GetFullPath(importPath);
        }

        var combined = Path.Combine(baseDirectory, importPath);
        return Path.GetFullPath(combined);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BasicRuntime));
        }
    }
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
