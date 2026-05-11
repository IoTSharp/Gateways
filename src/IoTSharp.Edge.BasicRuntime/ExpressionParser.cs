using System.Globalization;

namespace IoTSharp.Edge.BasicRuntime;

internal sealed class ExpressionParser
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly ExecutionContext _context;
    private int _position;

    private ExpressionParser(IReadOnlyList<Token> tokens, ExecutionContext context)
    {
        _tokens = tokens;
        _context = context;
    }

    public static BasicValue Evaluate(IReadOnlyList<Token> tokens, ExecutionContext context)
    {
        if (tokens.Count == 0)
        {
            return BasicValue.Nil;
        }

        var parser = new ExpressionParser(tokens, context);
        var result = parser.ParseOr();
        if (!parser.IsAtEnd)
        {
            throw Error(parser.Current, $"Unexpected token '{parser.Current.Text}' in expression.");
        }

        return result;
    }

    private bool IsAtEnd => _position >= _tokens.Count;

    private Token Current => IsAtEnd ? _tokens[^1] : _tokens[_position];

    private BasicValue ParseOr()
    {
        var left = ParseAnd();
        while (MatchKeyword("OR"))
        {
            var right = ParseAnd();
            left = BasicValue.FromBoolean(left.IsTruthy() || right.IsTruthy());
        }

        return left;
    }

    private BasicValue ParseAnd()
    {
        var left = ParseNot();
        while (MatchKeyword("AND"))
        {
            var right = ParseNot();
            left = BasicValue.FromBoolean(left.IsTruthy() && right.IsTruthy());
        }

        return left;
    }

    private BasicValue ParseNot()
    {
        if (MatchKeyword("NOT"))
        {
            return BasicValue.FromBoolean(!ParseNot().IsTruthy());
        }

        return ParseComparison();
    }

    private BasicValue ParseComparison()
    {
        var left = ParseTerm();
        while (true)
        {
            if (MatchOperator("=") || MatchOperator("=="))
            {
                left = BasicValue.FromBoolean(left.Equals(ParseTerm()));
            }
            else if (MatchOperator("<>"))
            {
                left = BasicValue.FromBoolean(!left.Equals(ParseTerm()));
            }
            else if (MatchOperator("<"))
            {
                left = BasicValue.FromBoolean(Compare(left, ParseTerm()) < 0);
            }
            else if (MatchOperator(">"))
            {
                left = BasicValue.FromBoolean(Compare(left, ParseTerm()) > 0);
            }
            else if (MatchOperator("<="))
            {
                left = BasicValue.FromBoolean(Compare(left, ParseTerm()) <= 0);
            }
            else if (MatchOperator(">="))
            {
                left = BasicValue.FromBoolean(Compare(left, ParseTerm()) >= 0);
            }
            else if (MatchKeyword("IS"))
            {
                left = BasicValue.FromBoolean(IsTypeMatch(left, ParseTerm()));
            }
            else
            {
                return left;
            }
        }
    }

    private BasicValue ParseTerm()
    {
        var left = ParseFactor();
        while (true)
        {
            if (MatchOperator("+"))
            {
                var right = ParseFactor();
                left = left.Kind == BasicValueKind.String || right.Kind == BasicValueKind.String
                    ? BasicValue.FromString(left.AsString() + right.AsString())
                    : BasicValue.FromNumber(left.AsNumber() + right.AsNumber());
            }
            else if (MatchOperator("-"))
            {
                left = BasicValue.FromNumber(left.AsNumber() - ParseFactor().AsNumber());
            }
            else
            {
                return left;
            }
        }
    }

    private BasicValue ParseFactor()
    {
        var left = ParsePower();
        while (true)
        {
            if (MatchOperator("*"))
            {
                left = BasicValue.FromNumber(left.AsNumber() * ParsePower().AsNumber());
            }
            else if (MatchOperator("/"))
            {
                var right = ParsePower().AsNumber();
                if (Math.Abs(right) < double.Epsilon)
                {
                    throw Error(Current, "Division by zero.");
                }

                left = BasicValue.FromNumber(left.AsNumber() / right);
            }
            else if (MatchKeyword("MOD"))
            {
                var right = ParsePower().AsNumber();
                if (Math.Abs(right) < double.Epsilon)
                {
                    throw Error(Current, "Modulo by zero.");
                }

                left = BasicValue.FromNumber(left.AsNumber() % right);
            }
            else
            {
                return left;
            }
        }
    }

    private BasicValue ParsePower()
    {
        var left = ParseUnary();
        if (MatchOperator("^"))
        {
            left = BasicValue.FromNumber(Math.Pow(left.AsNumber(), ParsePower().AsNumber()));
        }

        return left;
    }

    private BasicValue ParseUnary()
    {
        if (MatchOperator("-"))
        {
            return BasicValue.FromNumber(-ParseUnary().AsNumber());
        }

        if (MatchOperator("+"))
        {
            return BasicValue.FromNumber(ParseUnary().AsNumber());
        }

        return ParsePostfix();
    }

    private BasicValue ParsePostfix()
    {
        var value = ParsePrimary(out var baseIdentifier, out var isIdentifierBase);
        while (!IsAtEnd)
        {
            if (Match(TokenKind.Dot, out var dot))
            {
                var member = Expect(TokenKind.Identifier, "需要成员名。", dot);
                value = GetMember(value, member);
                baseIdentifier = null;
                isIdentifierBase = false;
                continue;
            }

            if (Match(TokenKind.OpenParen, out var open))
            {
                if (isIdentifierBase && baseIdentifier is not null && string.Equals(baseIdentifier, "len", StringComparison.OrdinalIgnoreCase)
                    && _position + 1 < _tokens.Count
                    && _tokens[_position].Kind == TokenKind.Ellipsis
                    && _tokens[_position + 1].Kind == TokenKind.CloseParen)
                {
                    _position += 2;
                    value = BasicValue.FromNumber(_context.RemainingVarArgsCount);
                    baseIdentifier = null;
                    isIdentifierBase = false;
                    continue;
                }

                var arguments = ParseArgumentList(open);
                value = InvokeOrIndex(value, baseIdentifier, isIdentifierBase, arguments, open);
                baseIdentifier = null;
                isIdentifierBase = false;
                continue;
            }

            break;
        }

        return value;
    }

    private BasicValue ParsePrimary(out string? baseIdentifier, out bool isIdentifierBase)
    {
        baseIdentifier = null;
        isIdentifierBase = false;

        if (Match(TokenKind.Number, out var numberToken))
        {
            return double.TryParse(numberToken.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
                ? BasicValue.FromNumber(number)
                : throw Error(numberToken, $"Invalid number literal '{numberToken.Text}'.");
        }

        if (Match(TokenKind.String, out var stringToken))
        {
            return BasicValue.FromString(stringToken.Text);
        }

        if (Match(TokenKind.OpenParen, out var openToken))
        {
            var value = ParseOr();
            Expect(TokenKind.CloseParen, "Expected ')' after expression.", openToken);
            return value;
        }

        if (Match(TokenKind.Ellipsis, out _))
        {
            return _context.PopVarArg();
        }

        if (Match(TokenKind.Identifier, out var identifier))
        {
            if (identifier.IsKeyword("CALL"))
            {
                var target = Expect(TokenKind.Identifier, "CALL 需要函数名。", identifier);
                var arguments = Match(TokenKind.OpenParen, out var callOpen) ? ParseArgumentList(callOpen) : [];
                return InvokeByName(target.Text, arguments, target);
            }

            if (identifier.IsKeyword("NEW"))
            {
                return ParseNew(identifier);
            }

            if (identifier.IsKeyword("TRUE"))
            {
                return BasicValue.FromBoolean(true);
            }

            if (identifier.IsKeyword("FALSE"))
            {
                return BasicValue.FromBoolean(false);
            }

            if (identifier.IsKeyword("NIL") || identifier.IsKeyword("NULL"))
            {
                return BasicValue.Nil;
            }

            baseIdentifier = identifier.Text;
            isIdentifierBase = true;
            return _context.GetVariable(identifier.Text);
        }

        throw Error(Current, "需要表达式。");
    }

    private BasicValue ParseNew(Token anchor)
    {
        var open = Expect(TokenKind.OpenParen, "NEW 在类名后需要左括号“(”。", anchor);
        var arguments = ParseArgumentList(open);
        if (arguments.Count != 1)
        {
            throw Error(anchor, "NEW 需要且只需要一个类或对象值。");
        }

        var source = arguments[0];
        if (source.Kind is not (BasicValueKind.Class or BasicValueKind.Instance))
        {
            throw Error(anchor, "NEW 需要类或对象值。");
        }

        return BasicValue.FromInstance(source.ObjectValue.CreateInstance());
    }

    private BasicValue GetMember(BasicValue target, Token member)
    {
        if (target.Kind is not (BasicValueKind.Class or BasicValueKind.Instance))
        {
            throw Error(member, $"值“{target.AsString()}”没有成员。");
        }

        return target.ObjectValue.TryGetMember(member.Text, _context, out var value)
            ? value
            : BasicValue.Nil;
    }

    private BasicValue InvokeOrIndex(BasicValue target, string? baseIdentifier, bool isIdentifierBase, IReadOnlyList<BasicValue> arguments, Token anchor)
    {
        if (target.Kind == BasicValueKind.Callable)
        {
            return target.Callable.Invoke(_context, arguments);
        }

        if (target.Kind is BasicValueKind.Array or BasicValueKind.List or BasicValueKind.Dictionary or BasicValueKind.String)
        {
            return GetIndexedValue(target, arguments, anchor);
        }

        if (isIdentifierBase && baseIdentifier is not null && TryInvokeByName(baseIdentifier, arguments, anchor, out var result))
        {
            return result;
        }

        throw Error(anchor, "值既不能调用，也不能索引。");
    }

    private bool TryInvokeByName(string name, IReadOnlyList<BasicValue> arguments, Token anchor, out BasicValue result)
    {
        if (_context.Execution.Runtime.TryGetFunction(name, out var native))
        {
            result = native(_context, arguments);
            return true;
        }

        if (_context.Program.TryGetFunction(name, out var definition))
        {
            result = BasicProgramRunner.ExecuteFunction(_context, definition, arguments);
            return true;
        }

        result = BasicValue.Nil;
        return false;
    }

    private BasicValue InvokeByName(string name, IReadOnlyList<BasicValue> arguments, Token anchor)
    {
        return TryInvokeByName(name, arguments, anchor, out var result)
            ? result
            : throw Error(anchor, $"未找到函数“{name}”。");
    }

    private static BasicValue GetIndexedValue(BasicValue target, IReadOnlyList<BasicValue> arguments, Token anchor)
    {
        return target.Kind switch
        {
            BasicValueKind.Array => target.Array.Get(arguments.Select(argument => (int)argument.AsNumber()).ToArray()),
            BasicValueKind.List when arguments.Count == 1 => GetListValue(target.List, (int)arguments[0].AsNumber()),
            BasicValueKind.Dictionary when arguments.Count == 1 => target.Dictionary.Get(arguments[0].AsString()),
            BasicValueKind.String when arguments.Count == 1 => GetStringValue(target.Text, (int)arguments[0].AsNumber()),
            _ => throw Error(anchor, "值不可索引。")
        };
    }

    private static BasicValue GetListValue(BasicList list, int index)
        => index >= 0 && index < list.Items.Count ? list.Items[index] : BasicValue.Nil;

    private static BasicValue GetStringValue(string text, int index)
        => index >= 0 && index < text.Length ? BasicValue.FromString(text[index].ToString()) : BasicValue.FromString(string.Empty);

    private IReadOnlyList<BasicValue> ParseArgumentList(Token openToken)
    {
        var values = new List<BasicValue>();
        if (Match(TokenKind.CloseParen, out _))
        {
            return values;
        }

        while (true)
        {
            values.Add(ParseOr());
            if (Match(TokenKind.Comma, out _))
            {
                continue;
            }

            Expect(TokenKind.CloseParen, "Expected ')' after argument list.", openToken);
            return values;
        }
    }

    private bool MatchKeyword(string keyword)
    {
        if (!IsAtEnd && Current.IsKeyword(keyword))
        {
            _position++;
            return true;
        }

        return false;
    }

    private bool MatchOperator(string op)
    {
        if (!IsAtEnd && Current.Kind == TokenKind.Operator && string.Equals(Current.Text, op, StringComparison.Ordinal))
        {
            _position++;
            return true;
        }

        return false;
    }

    private bool Match(TokenKind kind, out Token token)
    {
        if (!IsAtEnd && Current.Kind == kind)
        {
            token = Current;
            _position++;
            return true;
        }

        token = default;
        return false;
    }

    private Token Expect(TokenKind kind, string message, Token anchor)
    {
        if (Match(kind, out var token))
        {
            return token;
        }

        throw Error(anchor, message);
    }

    private static int Compare(BasicValue left, BasicValue right)
    {
        if (left.Kind is BasicValueKind.Number || right.Kind is BasicValueKind.Number)
        {
            return left.AsNumber().CompareTo(right.AsNumber());
        }

        return string.Compare(left.AsString(), right.AsString(), StringComparison.Ordinal);
    }

    private static bool IsTypeMatch(BasicValue value, BasicValue expected)
    {
        if (expected.Kind is BasicValueKind.Class or BasicValueKind.Instance
            && value.Kind is BasicValueKind.Class or BasicValueKind.Instance)
        {
            return IsSameOrDerived(value.ObjectValue.Definition, expected.ObjectValue.Definition);
        }

        if (expected.Kind is BasicValueKind.Class or BasicValueKind.Instance)
        {
            return false;
        }

        return value.Kind == expected.Kind;
    }

    private static bool IsSameOrDerived(ClassDefinition candidate, ClassDefinition expected)
    {
        for (ClassDefinition? current = candidate; current is not null; current = current.ParentDefinition)
        {
            if (ReferenceEquals(current, expected) || string.Equals(current.Name, expected.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static BasicRuntimeException Error(Token token, string message)
        => new(message, token.Line, token.Column);
}
