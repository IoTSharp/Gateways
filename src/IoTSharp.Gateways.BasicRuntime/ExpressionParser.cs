using System.Globalization;

namespace IoTSharp.Gateways.BasicRuntime;

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
                left = BasicValue.FromBoolean(left.Kind == ParseTerm().Kind);
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

        return ParsePrimary();
    }

    private BasicValue ParsePrimary()
    {
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

        if (Match(TokenKind.Identifier, out var identifier))
        {
            if (string.Equals(identifier.Text, "CALL", StringComparison.OrdinalIgnoreCase))
            {
                var target = Expect(TokenKind.Identifier, "CALL expects a function name.", identifier);
                return Invoke(target.Text, Match(TokenKind.OpenParen, out _) ? ParseArgumentList() : []);
            }

            if (string.Equals(identifier.Text, "TRUE", StringComparison.OrdinalIgnoreCase))
            {
                return BasicValue.FromBoolean(true);
            }

            if (string.Equals(identifier.Text, "FALSE", StringComparison.OrdinalIgnoreCase))
            {
                return BasicValue.FromBoolean(false);
            }

            if (string.Equals(identifier.Text, "NIL", StringComparison.OrdinalIgnoreCase)
                || string.Equals(identifier.Text, "NULL", StringComparison.OrdinalIgnoreCase))
            {
                return BasicValue.Nil;
            }

            if (Match(TokenKind.OpenParen, out _))
            {
                var arguments = ParseArgumentList();
                if (_context.Execution.Runtime.TryGetFunction(identifier.Text, out _)
                    || _context.Program.TryGetFunction(identifier.Text, out _))
                {
                    return Invoke(identifier.Text, arguments);
                }

                return GetIndexedValue(identifier, arguments);
            }

            return _context.GetVariable(identifier.Text);
        }

        throw Error(Current, "Expression expected.");
    }

    private BasicValue GetIndexedValue(Token identifier, IReadOnlyList<BasicValue> arguments)
    {
        var value = _context.GetVariable(identifier.Text);
        var indexes = arguments.Select(argument => (int)argument.AsNumber()).ToArray();
        if (indexes.Length == 0)
        {
            return value;
        }

        return value.Kind switch
        {
            BasicValueKind.Array => value.Array.Get(indexes),
            BasicValueKind.List when indexes.Length == 1 => indexes[0] >= 0 && indexes[0] < value.List.Items.Count
                ? value.List.Items[indexes[0]]
                : throw Error(identifier, "LIST index is out of bounds."),
            BasicValueKind.String when indexes.Length == 1 => indexes[0] >= 0 && indexes[0] < value.Text.Length
                ? BasicValue.FromString(value.Text[indexes[0]].ToString())
                : BasicValue.FromString(string.Empty),
            _ => throw Error(identifier, $"'{identifier.Text}' is not indexable.")
        };
    }

    private BasicValue Invoke(string name, IReadOnlyList<BasicValue> arguments)
    {
        if (_context.Execution.Runtime.TryGetFunction(name, out var native))
        {
            return native(_context, arguments);
        }

        if (_context.Program.TryGetFunction(name, out var definition))
        {
            return BasicProgramRunner.ExecuteFunction(_context, definition, arguments);
        }

        throw Error(Current, $"Function '{name}' was not found.");
    }

    private IReadOnlyList<BasicValue> ParseArgumentList()
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

            Expect(TokenKind.CloseParen, "Expected ')' after argument list.", Current);
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

    private static BasicRuntimeException Error(Token token, string message)
        => new(message, token.Line, token.Column);
}
