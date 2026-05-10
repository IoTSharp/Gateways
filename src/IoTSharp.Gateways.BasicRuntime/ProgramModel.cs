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

internal sealed record FunctionDefinition(string Name, IReadOnlyList<string> Parameters, bool IsVariadic, int BodyStart, int BodyEnd);

internal sealed record MethodDefinition(
    string Name,
    IReadOnlyList<string> Parameters,
    bool IsVariadic,
    int BodyStart,
    int BodyEnd,
    Token Anchor);

internal sealed record ClassFieldDefinition(
    string Name,
    IReadOnlyList<Token> InitializerTokens,
    Token Anchor);

internal sealed class ClassDefinition
{
    public ClassDefinition(
        string name,
        string? parentName,
        int startIndex,
        int endIndex,
        IReadOnlyList<ClassFieldDefinition> fields,
        IReadOnlyDictionary<string, MethodDefinition> methods)
    {
        Name = name;
        ParentName = parentName;
        StartIndex = startIndex;
        EndIndex = endIndex;
        Fields = fields;
        Methods = methods;
    }

    public string Name { get; }

    public string? ParentName { get; }

    public ClassDefinition? ParentDefinition { get; set; }

    public int StartIndex { get; }

    public int EndIndex { get; }

    public IReadOnlyList<ClassFieldDefinition> Fields { get; }

    public IReadOnlyDictionary<string, MethodDefinition> Methods { get; }
}

internal sealed class BasicProgram
{
    private BasicProgram(
        IReadOnlyList<Statement> statements,
        IReadOnlyDictionary<string, int> labels,
        IReadOnlyDictionary<string, FunctionDefinition> functions,
        IReadOnlyDictionary<string, ClassDefinition> classes)
    {
        Statements = statements;
        Labels = labels;
        Functions = functions;
        Classes = classes;
    }

    public IReadOnlyList<Statement> Statements { get; }

    public IReadOnlyDictionary<string, int> Labels { get; }

    public IReadOnlyDictionary<string, FunctionDefinition> Functions { get; }

    public IReadOnlyDictionary<string, ClassDefinition> Classes { get; }

    public static BasicProgram Parse(string source)
    {
        var rawStatements = SplitStatements(Lexer.Tokenize(source));
        var normalized = NormalizeStatements(rawStatements);
        var classes = BuildClasses(normalized.Statements);
        var functions = BuildFunctions(normalized.Statements);
        return new BasicProgram(normalized.Statements, normalized.Labels, functions, classes);
    }

    public bool TryGetLabel(string name, out int index)
        => Labels.TryGetValue(name, out index);

    public bool TryGetFunction(string name, out FunctionDefinition definition)
        => Functions.TryGetValue(name, out definition!);

    public bool TryGetClass(string name, out ClassDefinition definition)
        => Classes.TryGetValue(name, out definition!);

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
            if (statement.StartsWithKeyword("CLASS"))
            {
                index = FindMatchingEndClass(statements, index + 1);
                continue;
            }

            if (!statement.StartsWithKeyword("DEF"))
            {
                continue;
            }

            var definition = ParseFunctionDefinition(statements, index, statements.Count);
            functions[definition.Name] = definition;
            index = definition.BodyEnd;
        }

        return functions;
    }

    private static Dictionary<string, ClassDefinition> BuildClasses(IReadOnlyList<Statement> statements)
    {
        var classes = new Dictionary<string, ClassDefinition>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < statements.Count; index++)
        {
            var statement = statements[index];
            if (!statement.StartsWithKeyword("CLASS"))
            {
                continue;
            }

            var definition = ParseClassDefinition(statements, index);
            classes[definition.Name] = definition;
            index = definition.EndIndex;
        }

        return classes;
    }

    private static FunctionDefinition ParseFunctionDefinition(IReadOnlyList<Statement> statements, int index, int endBound)
    {
        var statement = statements[index];
        var cursor = 1;
        if (cursor >= statement.Tokens.Count || statement.Tokens[cursor].Kind != TokenKind.Identifier)
        {
            throw new BasicRuntimeException("DEF requires a function name.", statement.FirstToken.Line, statement.FirstToken.Column);
        }

        var name = statement.Tokens[cursor++].Text;
        var (parameters, isVariadic, _) = ParseParameterList(statement, cursor, "DEF");

        var endIndex = FindMatchingEndDef(statements, index + 1, endBound);
        return new FunctionDefinition(name, parameters, isVariadic, index + 1, endIndex);
    }

    private static ClassDefinition ParseClassDefinition(IReadOnlyList<Statement> statements, int index)
    {
        var statement = statements[index];
        var cursor = 1;
        if (cursor >= statement.Tokens.Count || statement.Tokens[cursor].Kind != TokenKind.Identifier)
        {
            throw new BasicRuntimeException("CLASS requires a class name.", statement.FirstToken.Line, statement.FirstToken.Column);
        }

        var name = statement.Tokens[cursor++].Text;
        string? parentName = null;
        if (cursor < statement.Tokens.Count && statement.Tokens[cursor].Kind == TokenKind.OpenParen)
        {
            cursor++;
            if (cursor < statement.Tokens.Count && statement.Tokens[cursor].Kind == TokenKind.Identifier)
            {
                parentName = statement.Tokens[cursor].Text;
                cursor++;
            }

            if (cursor >= statement.Tokens.Count || statement.Tokens[cursor].Kind != TokenKind.CloseParen)
            {
                throw new BasicRuntimeException("CLASS inheritance expects ')'.", statement.Tokens[cursor - 1].Line, statement.Tokens[cursor - 1].Column);
            }
        }

        var endIndex = FindMatchingEndClass(statements, index + 1);
        var fields = new List<ClassFieldDefinition>();
        var methods = new Dictionary<string, MethodDefinition>(StringComparer.OrdinalIgnoreCase);
        var cursorIndex = index + 1;
        while (cursorIndex < endIndex)
        {
            var bodyStatement = statements[cursorIndex];
            if (bodyStatement.StartsWithKeyword("VAR"))
            {
                fields.Add(ParseClassField(bodyStatement));
                cursorIndex++;
                continue;
            }

            if (bodyStatement.StartsWithKeyword("DEF"))
            {
                var method = ParseMethodDefinition(statements, cursorIndex, endIndex);
                methods[method.Name] = method;
                cursorIndex = method.BodyEnd + 1;
                continue;
            }

            cursorIndex++;
        }

        return new ClassDefinition(name, parentName, index, endIndex, fields, methods);
    }

    private static ClassFieldDefinition ParseClassField(Statement statement)
    {
        if (statement.Tokens.Count < 2 || statement.Tokens[1].Kind != TokenKind.Identifier)
        {
            throw new BasicRuntimeException("VAR expects a field name.", statement.FirstToken.Line, statement.FirstToken.Column);
        }

        var name = statement.Tokens[1].Text;
        var equalsIndex = statement.Tokens.ToList().FindIndex(token => token.Kind == TokenKind.Operator && token.Text == "=");
        var initializer = equalsIndex >= 0
            ? statement.Tokens.Skip(equalsIndex + 1).ToArray()
            : Array.Empty<Token>();
        return new ClassFieldDefinition(name, initializer, statement.FirstToken);
    }

    private static MethodDefinition ParseMethodDefinition(IReadOnlyList<Statement> statements, int index, int endBound)
    {
        var statement = statements[index];
        var cursor = 1;
        if (cursor >= statement.Tokens.Count || statement.Tokens[cursor].Kind != TokenKind.Identifier)
        {
            throw new BasicRuntimeException("DEF requires a method name.", statement.FirstToken.Line, statement.FirstToken.Column);
        }

        var name = statement.Tokens[cursor++].Text;
        var (parameters, isVariadic, _) = ParseParameterList(statement, cursor, "Method");

        var bodyEnd = FindMatchingEndDef(statements, index + 1, endBound);
        return new MethodDefinition(name, parameters, isVariadic, index + 1, bodyEnd, statement.FirstToken);
    }

    private static (IReadOnlyList<string> Parameters, bool IsVariadic, int NextCursor) ParseParameterList(
        Statement statement,
        int cursor,
        string owner)
    {
        var parameters = new List<string>();
        var isVariadic = false;

        if (cursor >= statement.Tokens.Count || statement.Tokens[cursor].Kind != TokenKind.OpenParen)
        {
            return (parameters, false, cursor);
        }

        cursor++;
        while (cursor < statement.Tokens.Count && statement.Tokens[cursor].Kind != TokenKind.CloseParen)
        {
            var token = statement.Tokens[cursor];
            if (token.Kind == TokenKind.Ellipsis)
            {
                isVariadic = true;
                cursor++;
                if (cursor < statement.Tokens.Count && statement.Tokens[cursor].Kind != TokenKind.CloseParen)
                {
                    throw new BasicRuntimeException($"{owner} variadic marker must be the last parameter.", token.Line, token.Column);
                }

                break;
            }

            if (token.Kind != TokenKind.Identifier)
            {
                throw new BasicRuntimeException($"{owner} parameter list expects identifiers.", token.Line, token.Column);
            }

            parameters.Add(token.Text);
            cursor++;
            if (cursor < statement.Tokens.Count && statement.Tokens[cursor].Kind == TokenKind.Comma)
            {
                cursor++;
            }
        }

        if (cursor >= statement.Tokens.Count || statement.Tokens[cursor].Kind != TokenKind.CloseParen)
        {
            throw new BasicRuntimeException($"{owner} parameter list is missing ')'.", statement.FirstToken.Line, statement.FirstToken.Column);
        }

        return (parameters, isVariadic, cursor + 1);
    }

    private static int FindMatchingEndDef(IReadOnlyList<Statement> statements, int startIndex, int endBound)
    {
        var depth = 0;
        for (var index = startIndex; index < endBound; index++)
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

    private static int FindMatchingEndClass(IReadOnlyList<Statement> statements, int startIndex)
    {
        var depth = 0;
        for (var index = startIndex; index < statements.Count; index++)
        {
            var statement = statements[index];
            if (statement.StartsWithKeyword("CLASS"))
            {
                depth++;
                continue;
            }

            if (statement.StartsWithKeyword("ENDCLASS"))
            {
                if (depth == 0)
                {
                    return index;
                }

                depth--;
            }
        }

        throw new BasicRuntimeException("CLASS block is missing ENDCLASS.");
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
    private readonly IReadOnlyList<BasicValue> _varArgs;
    private int _varArgIndex;

    private ExecutionContext(
        BasicProgram program,
        BasicExecution execution,
        Dictionary<string, BasicValue> variables,
        ExecutionContext? parent,
        FunctionDefinition? function,
        BasicValue? me,
        IReadOnlyList<BasicValue>? varArgs = null)
    {
        Program = program;
        Execution = execution;
        Variables = variables;
        Parent = parent;
        Function = function;
        Me = me;
        _varArgs = varArgs ?? Array.Empty<BasicValue>();
    }

    public BasicProgram Program { get; }

    public BasicExecution Execution { get; }

    public Dictionary<string, BasicValue> Variables { get; }

    public ExecutionContext? Parent { get; }

    public FunctionDefinition? Function { get; }

    public BasicValue? Me { get; }

    public bool Stopped { get; set; }

    public bool Returned { get; set; }

    public BasicValue ReturnValue { get; set; } = BasicValue.Nil;

    public static ExecutionContext CreateRoot(BasicProgram program, BasicExecution execution, Dictionary<string, BasicValue> globals)
        => new(program, execution, globals, null, null, null);

    public ExecutionContext CreateFunctionScope(FunctionDefinition function, IReadOnlyList<BasicValue> arguments, BasicValue? me = null)
    {
        var locals = new Dictionary<string, BasicValue>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < function.Parameters.Count; index++)
        {
            var name = function.Parameters[index];
            locals[name] = index < arguments.Count ? arguments[index] : BasicValue.Nil;
        }

        var varArgs = function.IsVariadic
            ? arguments.Skip(function.Parameters.Count).ToArray()
            : Array.Empty<BasicValue>();
        return new ExecutionContext(Program, Execution, locals, this, function, me ?? Me, varArgs);
    }

    public ExecutionContext CreateLambdaScope(IReadOnlyList<string> parameters, bool isVariadic, IReadOnlyList<BasicValue> arguments, BasicValue? me = null)
    {
        var locals = new Dictionary<string, BasicValue>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < parameters.Count; index++)
        {
            var name = parameters[index];
            locals[name] = index < arguments.Count ? arguments[index] : BasicValue.Nil;
        }

        var varArgs = isVariadic
            ? arguments.Skip(parameters.Count).ToArray()
            : Array.Empty<BasicValue>();
        return new ExecutionContext(Program, Execution, locals, this, null, me ?? Me, varArgs);
    }

    public bool TryGetValue(string name, out BasicValue value)
    {
        if (Me is { } current && string.Equals(name, "me", StringComparison.OrdinalIgnoreCase))
        {
            value = current;
            return true;
        }

        if (Variables.TryGetValue(name, out value))
        {
            return true;
        }

        if (Me is { } me && (me.Kind is BasicValueKind.Class or BasicValueKind.Instance) && me.ObjectValue.TryGetMember(name, this, out value))
        {
            return true;
        }

        if (Parent is not null)
        {
            return Parent.TryGetValue(name, out value);
        }

        value = default;
        return false;
    }

    public BasicValue GetVariable(string name)
    {
        return TryGetValue(name, out var value)
            ? value
            : name.EndsWith('$')
                ? BasicValue.FromString(string.Empty)
                : BasicValue.FromNumber(0);
    }

    public void SetVariable(string name, BasicValue value)
    {
        if (Me is not null && string.Equals(name, "me", StringComparison.OrdinalIgnoreCase))
        {
            throw new BasicRuntimeException("Cannot assign to reserved word 'me'.");
        }

        if (TrySetExisting(name, value))
        {
            return;
        }

        Variables[name] = value;
    }

    public int RemainingVarArgsCount => Math.Max(0, _varArgs.Count - _varArgIndex);

    public BasicValue PopVarArg()
    {
        if (_varArgIndex >= _varArgs.Count)
        {
            return BasicValue.Nil;
        }

        return _varArgs[_varArgIndex++];
    }

    private bool TrySetExisting(string name, BasicValue value)
    {
        if (Variables.ContainsKey(name))
        {
            Variables[name] = value;
            return true;
        }

        if (Me is { } me && (me.Kind is BasicValueKind.Class or BasicValueKind.Instance) && me.ObjectValue.HasMember(name))
        {
            me.ObjectValue.SetMember(name, value);
            return true;
        }

        if (Parent is not null)
        {
            return Parent.TrySetExisting(name, value);
        }

        return false;
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
