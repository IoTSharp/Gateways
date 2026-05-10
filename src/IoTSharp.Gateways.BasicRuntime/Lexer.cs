namespace IoTSharp.Gateways.BasicRuntime;

internal enum TokenKind
{
    EndOfFile,
    NewLine,
    Colon,
    Comma,
    Semicolon,
    Dot,
    Ellipsis,
    OpenParen,
    CloseParen,
    Operator,
    Number,
    String,
    Identifier
}

internal readonly record struct Token(TokenKind Kind, string Text, int Line, int Column)
{
    public bool IsKeyword(string keyword)
        => Kind == TokenKind.Identifier && string.Equals(Text, keyword, StringComparison.OrdinalIgnoreCase);
}

internal static class Lexer
{
    public static List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        var line = 1;
        var column = 1;
        var atStatementStart = true;
        var position = 0;

        while (position < source.Length)
        {
            var ch = source[position];

            if (ch == '\r')
            {
                position++;
                continue;
            }

            if (ch == '\n')
            {
                tokens.Add(new Token(TokenKind.NewLine, "\n", line, column));
                line++;
                column = 1;
                position++;
                atStatementStart = true;
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                position++;
                column++;
                continue;
            }

            if (ch == '\'')
            {
                SkipComment(source, ref position, ref column);
                continue;
            }

            if (ch == ':' )
            {
                tokens.Add(new Token(TokenKind.Colon, ":", line, column));
                position++;
                column++;
                atStatementStart = true;
                continue;
            }

            if (ch == ',')
            {
                tokens.Add(new Token(TokenKind.Comma, ",", line, column));
                position++;
                column++;
                atStatementStart = false;
                continue;
            }

            if (ch == ';')
            {
                tokens.Add(new Token(TokenKind.Semicolon, ";", line, column));
                position++;
                column++;
                atStatementStart = false;
                continue;
            }

            if (ch == '(')
            {
                tokens.Add(new Token(TokenKind.OpenParen, "(", line, column));
                position++;
                column++;
                atStatementStart = false;
                continue;
            }

            if (ch == ')')
            {
                tokens.Add(new Token(TokenKind.CloseParen, ")", line, column));
                position++;
                column++;
                atStatementStart = false;
                continue;
            }

            if (ch == '"')
            {
                tokens.Add(ReadString(source, ref position, ref column, line));
                atStatementStart = false;
                continue;
            }

            if (IsNumberStart(ch, Peek(source, position + 1)))
            {
                tokens.Add(ReadNumber(source, ref position, ref column, line));
                atStatementStart = false;
                continue;
            }

            if (IsIdentifierStart(ch))
            {
                var token = ReadIdentifier(source, ref position, ref column, line);
                if (atStatementStart && string.Equals(token.Text, "REM", StringComparison.OrdinalIgnoreCase))
                {
                    SkipComment(source, ref position, ref column);
                    atStatementStart = true;
                    continue;
                }

                tokens.Add(token);
                atStatementStart = false;
                continue;
            }

            tokens.Add(ReadOperator(source, ref position, ref column, line));
            atStatementStart = false;
        }

        tokens.Add(new Token(TokenKind.EndOfFile, string.Empty, line, column));
        return tokens;
    }

    private static void SkipComment(string source, ref int position, ref int column)
    {
        while (position < source.Length && source[position] != '\n')
        {
            position++;
            column++;
        }
    }

    private static Token ReadString(string source, ref int position, ref int column, int line)
    {
        var startColumn = column;
        position++;
        column++;
        var builder = new System.Text.StringBuilder();
        while (position < source.Length)
        {
            var ch = source[position];
            if (ch == '"')
            {
                if (Peek(source, position + 1) == '"')
                {
                    builder.Append('"');
                    position += 2;
                    column += 2;
                    continue;
                }

                position++;
                column++;
                return new Token(TokenKind.String, builder.ToString(), line, startColumn);
            }

            if (ch == '\\' && Peek(source, position + 1) is '"' or '\\' or 'n' or 'r' or 't')
            {
                var next = Peek(source, position + 1);
                builder.Append(next switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => next
                });
                position += 2;
                column += 2;
                continue;
            }

            builder.Append(ch);
            position++;
            column++;
        }

        throw new BasicRuntimeException("Unterminated string literal.", line, startColumn);
    }

    private static Token ReadNumber(string source, ref int position, ref int column, int line)
    {
        var start = position;
        var startColumn = column;
        var hasDot = false;
        while (position < source.Length)
        {
            var ch = source[position];
            if (char.IsDigit(ch))
            {
                position++;
                column++;
                continue;
            }

            if (ch == '.' && !hasDot)
            {
                hasDot = true;
                position++;
                column++;
                continue;
            }

            if ((ch == 'e' || ch == 'E') && position + 1 < source.Length)
            {
                position++;
                column++;
                if (source[position] is '+' or '-')
                {
                    position++;
                    column++;
                }

                while (position < source.Length && char.IsDigit(source[position]))
                {
                    position++;
                    column++;
                }

                break;
            }

            break;
        }

        return new Token(TokenKind.Number, source[start..position], line, startColumn);
    }

    private static Token ReadIdentifier(string source, ref int position, ref int column, int line)
    {
        var start = position;
        var startColumn = column;
        position++;
        column++;
        while (position < source.Length)
        {
            var ch = source[position];
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '$')
            {
                position++;
                column++;
                continue;
            }

            break;
        }

        return new Token(TokenKind.Identifier, source[start..position], line, startColumn);
    }

    private static Token ReadOperator(string source, ref int position, ref int column, int line)
    {
        var startColumn = column;
        var ch = source[position];
        string text;
        if (ch == '.' && position + 2 < source.Length && source[position + 1] == '.' && source[position + 2] == '.')
        {
            position += 3;
            column += 3;
            return new Token(TokenKind.Ellipsis, "...", line, startColumn);
        }

        if (position + 1 < source.Length)
        {
            var next = source[position + 1];
            text = (ch, next) switch
            {
                ('<', '=') => "<=",
                ('>', '=') => ">=",
                ('<', '>') => "<>",
                ('=', '=') => "==",
                _ => ch.ToString()
            };
            if (text.Length == 2)
            {
                position += 2;
                column += 2;
                return new Token(TokenKind.Operator, text, line, startColumn);
            }
        }

        position++;
        column++;
        return ch switch
        {
            '.' => new Token(TokenKind.Dot, ".", line, startColumn),
            '+' or '-' or '*' or '/' or '^' or '=' or '<' or '>' => new Token(TokenKind.Operator, ch.ToString(), line, startColumn),
            _ => throw new BasicRuntimeException($"Unexpected character '{ch}'.", line, startColumn)
        };
    }

    private static bool IsNumberStart(char current, char next)
        => char.IsDigit(current) || (current == '.' && char.IsDigit(next));

    private static bool IsIdentifierStart(char ch)
        => char.IsLetter(ch) || ch == '_';

    private static char Peek(string source, int index)
        => index >= 0 && index < source.Length ? source[index] : '\0';
}
