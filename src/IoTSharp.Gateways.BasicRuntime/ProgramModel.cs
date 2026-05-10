using System.Globalization;

namespace IoTSharp.Gateways.BasicRuntime;

internal enum StatementTerminator
{
    NewLine,
    Colon,
    EndOfFile
}

internal sealed record Statement(IReadOnlyList<Token> Tokens, StatementTerminator Terminator)
{
    public Token FirstToken => Tokens[0];

    public bool StartsWithKeyword(string keyword)
        => Tokens.Count > 0 && Tokens[0].IsKeyword(keyword);
}

internal sealed record FunctionDefinition(string Name, IReadOnlyList<string> Parameters, int BodyStart, int BodyEnd);

internal sealed class BasicProgram
{
    private BasicProgram(
        IReadOnlyList<Statement> statements,
        IReadOnlyDictionary<string, int> labels,
        IReadOnlyDictionary<string, FunctionDefinition> functions)
    {
        Statements = statements;
        Labels = labels;
        Functions = functions;
    }

    public IReadOnlyList<Statement> Statements { get; }

    public IReadOnlyDictionary<string, int> Labels { get; }

    public IReadOnlyDictionary<string, FunctionDefinition> Functions { get; }

    public static BasicProgram Parse(string source)
    {
        var rawStatements = SplitStatements(Lexer.Tokenize(source));
        var normalized = NormalizeStatements(rawStatements);
        var functions = BuildFunctions(normalized.Statements);
        return new BasicProgram(normalized.Statements, normalized.Labels, functions);
    }

    public bool TryGetLabel(string name, out int index)
        => Labels.TryGetValue(name, out index);

    public bool TryGetFunction(string name, out FunctionDefinition definition)
        => Functions.TryGetValue(name, out definition!);

    private static List<Statement> SplitStatements(IReadOnlyList<Token> tokens)
    {
        var statements = new List<Statement>();
        var buffer = new List<Token>();
        var terminator = StatementTerminator.EndOfFile;

        foreach (var token in tokens)
        {
            if (token.Kind is TokenKind.NewLine or TokenKind.Colon or TokenKind.EndOfFile)
            {
                if (buffer.Count > 0)
                {
                    statements.Add(new Statement(buffer.ToArray(), token.Kind switch
                    {
                        TokenKind.Colon => StatementTerminator.Colon,
                        TokenKind.NewLine => StatementTerminator.NewLine,
                        _ => StatementTerminator.EndOfFile
                    }));
                    buffer.Clear();
                }

                terminator = token.Kind switch
                {
                    TokenKind.Colon => StatementTerminator.Colon,
                    TokenKind.NewLine => StatementTerminator.NewLine,
                    _ => StatementTerminator.EndOfFile
                };

                if (token.Kind == TokenKind.EndOfFile)
                {
                    break;
                }

                continue;
            }

            buffer.Add(token);
        }

        if (buffer.Count > 0)
        {
            statements.Add(new Statement(buffer.ToArray(), terminator));
        }

        return statements;
    }

    private static NormalizedProgram NormalizeStatements(IReadOnlyList<Statement> rawStatements)
    {
        var statements = new List<Statement>();
        var labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var statement in rawStatements)
        {
            if (statement.Tokens.Count == 0)
            {
                continue;
            }

            if (IsLabelOnly(statement))
            {
                labels[statement.Tokens[0].Text] = statements.Count;
                continue;
            }

            if (TryStripNumericLabel(statement, out var stripped))
            {
                labels[statement.Tokens[0].Text] = statements.Count;
                if (stripped.Tokens.Count > 0)
                {
                    statements.Add(stripped);
                }

                continue;
            }

            statements.Add(statement);
        }

        return new NormalizedProgram(statements, labels);
    }

    private static Dictionary<string, FunctionDefinition> BuildFunctions(IReadOnlyList<Statement> statements)
    {
        var functions = new Dictionary<string, FunctionDefinition>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < statements.Count; index++)
        {
            var statement = statements[index];
            if (!statement.StartsWithKeyword("DEF"))
            {
                continue;
            }

            var definition = ParseFunctionDefinition(statements, index);
            functions[definition.Name] = definition;
        }

        return functions;
    }

    private static FunctionDefinition ParseFunctionDefinition(IReadOnlyList<Statement> statements, int index)
    {
        var statement = statements[index];
        var cursor = 1;
        if (cursor >= statement.Tokens.Count || statement.Tokens[cursor].Kind != TokenKind.Identifier)
        {
            throw new BasicRuntimeException("DEF requires a function name.", statement.FirstToken.Line, statement.FirstToken.Column);
        }

        var name = statement.Tokens[cursor++].Text;
        var parameters = new List<string>();

        if (cursor < statement.Tokens.Count && statement.Tokens[cursor].Kind == TokenKind.OpenParen)
        {
            cursor++;
            while (cursor < statement.Tokens.Count && statement.Tokens[cursor].Kind != TokenKind.CloseParen)
            {
                if (statement.Tokens[cursor].Kind != TokenKind.Identifier)
                {
                    throw new BasicRuntimeException("DEF parameter list expects identifiers.", statement.Tokens[cursor].Line, statement.Tokens[cursor].Column);
                }

                parameters.Add(statement.Tokens[cursor].Text);
                cursor++;
                if (cursor < statement.Tokens.Count && statement.Tokens[cursor].Kind == TokenKind.Comma)
                {
                    cursor++;
                }
            }
        }

        var endIndex = FindMatchingEndDef(statements, index + 1);
        return new FunctionDefinition(name, parameters, index + 1, endIndex);
    }

    private static int FindMatchingEndDef(IReadOnlyList<Statement> statements, int startIndex)
    {
        var depth = 0;
        for (var index = startIndex; index < statements.Count; index++)
        {
            var statement = statements[index];
            if (statement.StartsWithKeyword("DEF"))
            {
                depth++;
                continue;
            }

            if (statement.StartsWithKeyword("ENDDEF"))
            {
                if (depth == 0)
                {
                    return index;
                }

                depth--;
            }
        }

        throw new BasicRuntimeException("DEF block is missing ENDDEF.");
    }

    private static bool IsLabelOnly(Statement statement)
        => statement.Terminator == StatementTerminator.Colon
            && statement.Tokens.Count == 1
            && statement.Tokens[0].Kind == TokenKind.Identifier;

    private static bool TryStripNumericLabel(Statement statement, out Statement stripped)
    {
        if (statement.Tokens[0].Kind == TokenKind.Number
            && double.TryParse(statement.Tokens[0].Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            && Math.Abs(value % 1) < 0.000000001d)
        {
            stripped = new Statement(statement.Tokens.Skip(1).ToArray(), statement.Terminator);
            return true;
        }

        stripped = statement;
        return false;
    }

    private sealed record NormalizedProgram(IReadOnlyList<Statement> Statements, IReadOnlyDictionary<string, int> Labels);
}

internal sealed class ExecutionContext
{
    private readonly Stack<LoopFrame> _loops = new();
    private readonly Stack<int> _gosubReturns = new();

    private ExecutionContext(BasicProgram program, BasicExecution execution, Dictionary<string, BasicValue> variables, ExecutionContext? parent, FunctionDefinition? function)
    {
        Program = program;
        Execution = execution;
        Variables = variables;
        Parent = parent;
        Function = function;
    }

    public BasicProgram Program { get; }

    public BasicExecution Execution { get; }

    public Dictionary<string, BasicValue> Variables { get; }

    public ExecutionContext? Parent { get; }

    public FunctionDefinition? Function { get; }

    public bool Stopped { get; set; }

    public bool Returned { get; set; }

    public BasicValue ReturnValue { get; set; } = BasicValue.Nil;

    public static ExecutionContext CreateRoot(BasicProgram program, BasicExecution execution, Dictionary<string, BasicValue> globals)
        => new(program, execution, globals, null, null);

    public ExecutionContext CreateFunctionScope(FunctionDefinition function, IReadOnlyList<BasicValue> arguments)
    {
        var locals = new Dictionary<string, BasicValue>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < function.Parameters.Count; index++)
        {
            var name = function.Parameters[index];
            locals[name] = index < arguments.Count ? arguments[index] : BasicValue.Nil;
        }

        return new ExecutionContext(Program, Execution, locals, this, function);
    }

    public BasicValue GetVariable(string name)
    {
        if (Variables.TryGetValue(name, out var value))
        {
            return value;
        }

        if (Parent is not null)
        {
            return Parent.GetVariable(name);
        }

        return name.EndsWith('$') ? BasicValue.FromString(string.Empty) : BasicValue.FromNumber(0);
    }

    public void SetVariable(string name, BasicValue value)
    {
        Variables[name] = value;
    }

    public void PushGosubReturn(int index)
        => _gosubReturns.Push(index);

    public bool TryPopGosubReturn(out int index)
    {
        if (_gosubReturns.Count > 0)
        {
            index = _gosubReturns.Pop();
            return true;
        }

        index = default;
        return false;
    }

    public LoopFrame? PeekLoop()
        => _loops.Count > 0 ? _loops.Peek() : null;

    public void PushLoop(LoopFrame frame)
        => _loops.Push(frame);

    public LoopFrame PopLoop()
        => _loops.Pop();

    public void ClearLoopToDepth(int depth)
    {
        while (_loops.Count > depth)
        {
            _loops.Pop();
        }
    }

    public int LoopDepth => _loops.Count;

    public bool HasMatchingLoop(string kind)
        => _loops.Any(frame => string.Equals(frame.Kind, kind, StringComparison.OrdinalIgnoreCase));
}

internal sealed record LoopFrame(
    string Kind,
    int StatementIndex,
    int TargetIndex,
    string? VariableName = null,
    BasicValue? BoundValue = null,
    BasicValue? StepValue = null);
