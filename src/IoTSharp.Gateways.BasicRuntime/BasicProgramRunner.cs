namespace IoTSharp.Gateways.BasicRuntime;

internal static class BasicProgramRunner
{
    public static BasicValue Execute(ExecutionContext context, int startIndex, int endIndex)
    {
        var pc = startIndex;
        while (pc < endIndex && !context.Stopped && !context.Returned)
        {
            var statement = context.Program.Statements[pc];
            context.Execution.CountStatement(statement.FirstToken);
            pc = ExecuteStatement(context, statement, pc, endIndex);
        }

        return context.Returned ? context.ReturnValue : BasicValue.Nil;
    }

    public static BasicValue ExecuteFunction(ExecutionContext caller, FunctionDefinition definition, IReadOnlyList<BasicValue> arguments)
    {
        var context = caller.CreateFunctionScope(definition, arguments);
        var result = Execute(context, definition.BodyStart, definition.BodyEnd);
        if (context.Stopped)
        {
            caller.Stopped = true;
        }

        return result;
    }

    private static int ExecuteStatement(ExecutionContext context, Statement statement, int currentIndex, int endIndex)
    {
        if (statement.Tokens.Count == 0)
        {
            return currentIndex + 1;
        }

        var first = statement.Tokens[0];
        if (first.Kind == TokenKind.Identifier)
        {
            if (first.IsKeyword("REM"))
            {
                return currentIndex + 1;
            }

            if (first.IsKeyword("LET"))
            {
                ExecuteAssignment(context, statement.Tokens.Skip(1).ToArray());
                return currentIndex + 1;
            }

            if (first.IsKeyword("DIM"))
            {
                ExecuteDim(context, statement);
                return currentIndex + 1;
            }

            if (first.IsKeyword("PRINT"))
            {
                ExecutePrint(context, statement);
                return currentIndex + 1;
            }

            if (first.IsKeyword("INPUT"))
            {
                ExecuteInput(context, statement);
                return currentIndex + 1;
            }

            if (first.IsKeyword("IF"))
            {
                return ExecuteIf(context, statement, currentIndex, endIndex);
            }

            if (first.IsKeyword("ELSEIF") || first.IsKeyword("ELSE"))
            {
                return FindMatchingEndIf(context.Program.Statements, currentIndex + 1, endIndex) + 1;
            }

            if (first.IsKeyword("ENDIF"))
            {
                return currentIndex + 1;
            }

            if (first.IsKeyword("FOR"))
            {
                return ExecuteFor(context, statement, currentIndex, endIndex);
            }

            if (first.IsKeyword("NEXT"))
            {
                return ExecuteNext(context, statement, currentIndex);
            }

            if (first.IsKeyword("WHILE"))
            {
                return ExecuteWhile(context, statement, currentIndex, endIndex);
            }

            if (first.IsKeyword("WEND"))
            {
                return ExecuteWend(context, statement, currentIndex);
            }

            if (first.IsKeyword("DO"))
            {
                return ExecuteDo(context, statement, currentIndex, endIndex);
            }

            if (first.IsKeyword("UNTIL"))
            {
                return ExecuteUntil(context, statement, currentIndex);
            }

            if (first.IsKeyword("EXIT"))
            {
                return ExecuteExit(context, statement);
            }

            if (first.IsKeyword("GOTO"))
            {
                return ResolveLabelTarget(context, statement, 1);
            }

            if (first.IsKeyword("GOSUB"))
            {
                context.PushGosubReturn(currentIndex + 1);
                return ResolveLabelTarget(context, statement, 1);
            }

            if (first.IsKeyword("RETURN"))
            {
                return ExecuteReturn(context, statement, currentIndex);
            }

            if (first.IsKeyword("DEF"))
            {
                return context.Program.TryGetFunction(ReadFunctionName(statement), out var definition)
                    ? definition.BodyEnd + 1
                    : FindMatchingEndDef(context.Program.Statements, currentIndex + 1, endIndex) + 1;
            }

            if (first.IsKeyword("ENDDEF"))
            {
                context.Returned = true;
                context.ReturnValue = BasicValue.Nil;
                return currentIndex + 1;
            }

            if (first.IsKeyword("CALL"))
            {
                ExpressionParser.Evaluate(statement.Tokens, context);
                return currentIndex + 1;
            }

            if (first.IsKeyword("END"))
            {
                context.Stopped = true;
                return currentIndex + 1;
            }
        }

        if (FindAssignmentOperator(statement.Tokens) >= 0)
        {
            ExecuteAssignment(context, statement.Tokens);
        }
        else
        {
            ExpressionParser.Evaluate(statement.Tokens, context);
        }

        return currentIndex + 1;
    }

    private static int ExecuteIf(ExecutionContext context, Statement statement, int currentIndex, int endIndex)
    {
        var thenIndex = FindTopLevelKeyword(statement.Tokens, "THEN", 1);
        if (thenIndex < 0)
        {
            throw Error(statement, "IF requires THEN.");
        }

        var condition = ExpressionParser.Evaluate(Slice(statement.Tokens, 1, thenIndex), context).IsTruthy();
        var inlineTokens = Slice(statement.Tokens, thenIndex + 1, statement.Tokens.Count);
        if (inlineTokens.Count > 0)
        {
            var elseIndex = FindTopLevelKeyword(inlineTokens, "ELSE", 0);
            var selected = condition
                ? Slice(inlineTokens, 0, elseIndex >= 0 ? elseIndex : inlineTokens.Count)
                : elseIndex >= 0
                    ? Slice(inlineTokens, elseIndex + 1, inlineTokens.Count)
                    : [];

            return selected.Count == 0
                ? currentIndex + 1
                : ExecuteInline(context, selected, currentIndex, endIndex);
        }

        return condition
            ? currentIndex + 1
            : ResolveFalseIfBranch(context, currentIndex + 1, endIndex);
    }

    private static int ResolveFalseIfBranch(ExecutionContext context, int startIndex, int endIndex)
    {
        var index = startIndex;
        while (index < endIndex)
        {
            var branch = FindNextIfBranch(context.Program.Statements, index, endIndex);
            if (branch < 0)
            {
                throw new BasicRuntimeException("IF block is missing ENDIF.");
            }

            var statement = context.Program.Statements[branch];
            if (statement.StartsWithKeyword("ENDIF"))
            {
                return branch + 1;
            }

            if (statement.StartsWithKeyword("ELSE"))
            {
                return branch + 1;
            }

            if (!statement.StartsWithKeyword("ELSEIF"))
            {
                return branch + 1;
            }

            var thenIndex = FindTopLevelKeyword(statement.Tokens, "THEN", 1);
            if (thenIndex < 0)
            {
                throw Error(statement, "ELSEIF requires THEN.");
            }

            var condition = ExpressionParser.Evaluate(Slice(statement.Tokens, 1, thenIndex), context).IsTruthy();
            var inlineTokens = Slice(statement.Tokens, thenIndex + 1, statement.Tokens.Count);
            if (condition)
            {
                if (inlineTokens.Count == 0)
                {
                    return branch + 1;
                }

                var next = ExecuteInline(context, inlineTokens, branch, endIndex);
                return context.Stopped || context.Returned || next != branch + 1
                    ? next
                    : FindMatchingEndIf(context.Program.Statements, branch + 1, endIndex) + 1;
            }

            index = branch + 1;
        }

        return endIndex;
    }

    private static int ExecuteFor(ExecutionContext context, Statement statement, int currentIndex, int endIndex)
    {
        if (statement.Tokens.Count < 6 || statement.Tokens[1].Kind != TokenKind.Identifier)
        {
            throw Error(statement, "FOR expects 'FOR variable = start TO end'.");
        }

        var equalsIndex = FindTopLevelOperator(statement.Tokens, "=", 2);
        var toIndex = FindTopLevelKeyword(statement.Tokens, "TO", equalsIndex + 1);
        if (equalsIndex < 0 || toIndex < 0)
        {
            throw Error(statement, "FOR expects '=' and TO.");
        }

        var stepIndex = FindTopLevelKeyword(statement.Tokens, "STEP", toIndex + 1);
        var variableName = statement.Tokens[1].Text;
        var start = ExpressionParser.Evaluate(Slice(statement.Tokens, equalsIndex + 1, toIndex), context);
        var end = ExpressionParser.Evaluate(Slice(statement.Tokens, toIndex + 1, stepIndex >= 0 ? stepIndex : statement.Tokens.Count), context);
        var step = stepIndex >= 0
            ? ExpressionParser.Evaluate(Slice(statement.Tokens, stepIndex + 1, statement.Tokens.Count), context)
            : BasicValue.FromNumber(1);
        var nextIndex = FindMatchingPair(context.Program.Statements, currentIndex + 1, endIndex, "FOR", "NEXT");
        if (Math.Abs(step.AsNumber()) < double.Epsilon)
        {
            throw Error(statement, "FOR STEP cannot be zero.");
        }

        context.SetVariable(variableName, start);
        if (!ShouldContinueFor(start.AsNumber(), end.AsNumber(), step.AsNumber()))
        {
            return nextIndex + 1;
        }

        context.PushLoop(new LoopFrame("FOR", currentIndex, nextIndex, variableName, end, step));
        return currentIndex + 1;
    }

    private static int ExecuteNext(ExecutionContext context, Statement statement, int currentIndex)
    {
        var frame = context.PeekLoop();
        if (frame is null || !string.Equals(frame.Kind, "FOR", StringComparison.OrdinalIgnoreCase) || frame.VariableName is null)
        {
            throw Error(statement, "NEXT without FOR.");
        }

        var step = frame.StepValue ?? BasicValue.FromNumber(1);
        var end = frame.BoundValue ?? BasicValue.FromNumber(0);
        var current = context.GetVariable(frame.VariableName);
        var next = BasicValue.FromNumber(current.AsNumber() + step.AsNumber());
        context.SetVariable(frame.VariableName, next);

        if (ShouldContinueFor(next.AsNumber(), end.AsNumber(), step.AsNumber()))
        {
            context.Execution.CountLoop(statement.FirstToken);
            return frame.StatementIndex + 1;
        }

        context.PopLoop();
        return currentIndex + 1;
    }

    private static int ExecuteWhile(ExecutionContext context, Statement statement, int currentIndex, int endIndex)
    {
        var wendIndex = FindMatchingPair(context.Program.Statements, currentIndex + 1, endIndex, "WHILE", "WEND");
        var condition = ExpressionParser.Evaluate(Slice(statement.Tokens, 1, statement.Tokens.Count), context).IsTruthy();
        var existing = context.PeekLoop();

        if (!condition)
        {
            if (existing is not null && existing.StatementIndex == currentIndex && existing.Kind == "WHILE")
            {
                context.PopLoop();
            }

            return wendIndex + 1;
        }

        if (existing is null || existing.StatementIndex != currentIndex || existing.Kind != "WHILE")
        {
            context.PushLoop(new LoopFrame("WHILE", currentIndex, wendIndex));
        }

        return currentIndex + 1;
    }

    private static int ExecuteWend(ExecutionContext context, Statement statement, int currentIndex)
    {
        var frame = context.PeekLoop();
        if (frame is null || frame.Kind != "WHILE")
        {
            throw Error(statement, "WEND without WHILE.");
        }

        context.Execution.CountLoop(statement.FirstToken);
        return frame.StatementIndex;
    }

    private static int ExecuteDo(ExecutionContext context, Statement statement, int currentIndex, int endIndex)
    {
        var untilIndex = FindMatchingPair(context.Program.Statements, currentIndex + 1, endIndex, "DO", "UNTIL");
        var existing = context.PeekLoop();
        if (existing is null || existing.StatementIndex != currentIndex || existing.Kind != "DO")
        {
            context.PushLoop(new LoopFrame("DO", currentIndex, untilIndex));
        }

        return currentIndex + 1;
    }

    private static int ExecuteUntil(ExecutionContext context, Statement statement, int currentIndex)
    {
        var frame = context.PeekLoop();
        if (frame is null || frame.Kind != "DO")
        {
            throw Error(statement, "UNTIL without DO.");
        }

        var condition = ExpressionParser.Evaluate(Slice(statement.Tokens, 1, statement.Tokens.Count), context).IsTruthy();
        if (condition)
        {
            context.PopLoop();
            return currentIndex + 1;
        }

        context.Execution.CountLoop(statement.FirstToken);
        return frame.StatementIndex + 1;
    }

    private static int ExecuteExit(ExecutionContext context, Statement statement)
    {
        var frame = context.PeekLoop();
        if (frame is null)
        {
            throw Error(statement, "EXIT without an active loop.");
        }

        context.PopLoop();
        return frame.TargetIndex + 1;
    }

    private static int ExecuteReturn(ExecutionContext context, Statement statement, int currentIndex)
    {
        var value = statement.Tokens.Count > 1
            ? ExpressionParser.Evaluate(Slice(statement.Tokens, 1, statement.Tokens.Count), context)
            : BasicValue.Nil;

        if (context.Function is not null || statement.Tokens.Count > 1)
        {
            context.Returned = true;
            context.ReturnValue = value;
            return currentIndex + 1;
        }

        if (context.TryPopGosubReturn(out var target))
        {
            return target;
        }

        context.Returned = true;
        context.ReturnValue = value;
        return currentIndex + 1;
    }

    private static void ExecuteDim(ExecutionContext context, Statement statement)
    {
        if (statement.Tokens.Count < 4 || statement.Tokens[1].Kind != TokenKind.Identifier || statement.Tokens[2].Kind != TokenKind.OpenParen)
        {
            throw Error(statement, "DIM expects 'DIM name(size)'.");
        }

        var closeIndex = FindMatchingCloseParen(statement.Tokens, 2);
        var dimensions = SplitTopLevel(Slice(statement.Tokens, 3, closeIndex), TokenKind.Comma)
            .Select(tokens => Math.Max(0, (int)ExpressionParser.Evaluate(tokens, context).AsNumber()))
            .ToArray();
        context.SetVariable(statement.Tokens[1].Text, BasicValue.FromArray(new BasicArray(dimensions)));
    }

    private static void ExecuteAssignment(ExecutionContext context, IReadOnlyList<Token> tokens)
    {
        var equalsIndex = FindAssignmentOperator(tokens);
        if (equalsIndex < 0)
        {
            throw new BasicRuntimeException("Assignment expects '='.", tokens[0].Line, tokens[0].Column);
        }

        var left = Slice(tokens, 0, equalsIndex);
        var right = Slice(tokens, equalsIndex + 1, tokens.Count);
        var value = ExpressionParser.Evaluate(right, context);
        SetTarget(context, left, value);
    }

    private static void SetTarget(ExecutionContext context, IReadOnlyList<Token> left, BasicValue value)
    {
        if (left.Count == 1 && left[0].Kind == TokenKind.Identifier)
        {
            context.SetVariable(left[0].Text, value);
            return;
        }

        if (left.Count >= 4 && left[0].Kind == TokenKind.Identifier && left[1].Kind == TokenKind.OpenParen && left[^1].Kind == TokenKind.CloseParen)
        {
            var name = left[0].Text;
            var indexes = SplitTopLevel(Slice(left, 2, left.Count - 1), TokenKind.Comma)
                .Select(tokens => (int)ExpressionParser.Evaluate(tokens, context).AsNumber())
                .ToArray();
            var target = context.GetVariable(name);

            if (target.Kind == BasicValueKind.Array)
            {
                target.Array.Set(indexes, value);
                return;
            }

            if (target.Kind == BasicValueKind.List && indexes.Length == 1)
            {
                var index = indexes[0];
                if (index < 0)
                {
                    throw new BasicRuntimeException("LIST index is out of bounds.", left[0].Line, left[0].Column);
                }

                while (target.List.Items.Count <= index)
                {
                    target.List.Items.Add(BasicValue.Nil);
                }

                target.List.Items[index] = value;
                return;
            }
        }

        throw new BasicRuntimeException("Invalid assignment target.", left[0].Line, left[0].Column);
    }

    private static void ExecutePrint(ExecutionContext context, Statement statement)
    {
        var tokens = Slice(statement.Tokens, 1, statement.Tokens.Count);
        if (tokens.Count == 0)
        {
            context.Execution.Write(string.Empty);
            return;
        }

        var builder = new System.Text.StringBuilder();
        var parts = SplitPrintParts(tokens);
        foreach (var part in parts)
        {
            if (part.Tokens.Count > 0)
            {
                builder.Append(ExpressionParser.Evaluate(part.Tokens, context).AsString());
            }

            if (part.Separator == TokenKind.Comma)
            {
                builder.Append(' ');
            }
        }

        context.Execution.Write(builder.ToString());
    }

    private static void ExecuteInput(ExecutionContext context, Statement statement)
    {
        var tokens = Slice(statement.Tokens, 1, statement.Tokens.Count);
        if (tokens.Count == 0)
        {
            throw Error(statement, "INPUT expects a variable.");
        }

        string prompt;
        IReadOnlyList<Token> target;
        var comma = FindTopLevelToken(tokens, TokenKind.Comma, 0);
        if (comma >= 0)
        {
            prompt = ExpressionParser.Evaluate(Slice(tokens, 0, comma), context).AsString();
            target = Slice(tokens, comma + 1, tokens.Count);
        }
        else
        {
            prompt = string.Empty;
            target = tokens;
        }

        if (target.Count != 1 || target[0].Kind != TokenKind.Identifier)
        {
            throw Error(statement, "INPUT expects a target variable.");
        }

        var input = context.Execution.Options.InputProvider?.Invoke(prompt) ?? string.Empty;
        context.SetVariable(target[0].Text, BasicValue.FromString(input));
    }

    private static int ExecuteInline(ExecutionContext context, IReadOnlyList<Token> tokens, int currentIndex, int endIndex)
    {
        var inline = new Statement(tokens, StatementTerminator.EndOfFile);
        return ExecuteStatement(context, inline, currentIndex, endIndex);
    }

    private static int ResolveLabelTarget(ExecutionContext context, Statement statement, int startToken)
    {
        if (statement.Tokens.Count <= startToken)
        {
            throw Error(statement, "Label expected.");
        }

        string label;
        if (statement.Tokens.Count == startToken + 1
            && statement.Tokens[startToken].Kind is TokenKind.Identifier or TokenKind.Number or TokenKind.String)
        {
            label = statement.Tokens[startToken].Text;
        }
        else
        {
            label = ExpressionParser.Evaluate(Slice(statement.Tokens, startToken, statement.Tokens.Count), context).AsString();
        }

        return context.Program.TryGetLabel(label, out var target)
            ? target
            : throw new BasicRuntimeException($"Label '{label}' was not found.", statement.FirstToken.Line, statement.FirstToken.Column);
    }

    private static string ReadFunctionName(Statement statement)
    {
        if (statement.Tokens.Count > 1 && statement.Tokens[1].Kind == TokenKind.Identifier)
        {
            return statement.Tokens[1].Text;
        }

        throw Error(statement, "DEF requires a function name.");
    }

    private static bool ShouldContinueFor(double current, double end, double step)
        => step > 0 ? current <= end : current >= end;

    private static int FindAssignmentOperator(IReadOnlyList<Token> tokens)
        => FindTopLevelOperator(tokens, "=", 0);

    private static int FindTopLevelOperator(IReadOnlyList<Token> tokens, string op, int start)
    {
        var depth = 0;
        for (var index = start; index < tokens.Count; index++)
        {
            var token = tokens[index];
            depth += token.Kind switch
            {
                TokenKind.OpenParen => 1,
                TokenKind.CloseParen => -1,
                _ => 0
            };

            if (depth == 0 && token.Kind == TokenKind.Operator && token.Text == op)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindTopLevelKeyword(IReadOnlyList<Token> tokens, string keyword, int start)
    {
        var depth = 0;
        for (var index = start; index < tokens.Count; index++)
        {
            var token = tokens[index];
            depth += token.Kind switch
            {
                TokenKind.OpenParen => 1,
                TokenKind.CloseParen => -1,
                _ => 0
            };

            if (depth == 0 && token.IsKeyword(keyword))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindTopLevelToken(IReadOnlyList<Token> tokens, TokenKind kind, int start)
    {
        var depth = 0;
        for (var index = start; index < tokens.Count; index++)
        {
            var token = tokens[index];
            depth += token.Kind switch
            {
                TokenKind.OpenParen => 1,
                TokenKind.CloseParen => -1,
                _ => 0
            };

            if (depth == 0 && token.Kind == kind)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindMatchingCloseParen(IReadOnlyList<Token> tokens, int openIndex)
    {
        var depth = 0;
        for (var index = openIndex; index < tokens.Count; index++)
        {
            if (tokens[index].Kind == TokenKind.OpenParen)
            {
                depth++;
            }
            else if (tokens[index].Kind == TokenKind.CloseParen)
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        throw new BasicRuntimeException("Missing ')'.", tokens[openIndex].Line, tokens[openIndex].Column);
    }

    private static int FindMatchingPair(IReadOnlyList<Statement> statements, int startIndex, int endIndex, string beginKeyword, string endKeyword)
    {
        var depth = 0;
        for (var index = startIndex; index < endIndex; index++)
        {
            if (statements[index].StartsWithKeyword(beginKeyword))
            {
                depth++;
                continue;
            }

            if (statements[index].StartsWithKeyword(endKeyword))
            {
                if (depth == 0)
                {
                    return index;
                }

                depth--;
            }
        }

        throw new BasicRuntimeException($"{beginKeyword} block is missing {endKeyword}.");
    }

    private static int FindNextIfBranch(IReadOnlyList<Statement> statements, int startIndex, int endIndex)
    {
        var depth = 0;
        for (var index = startIndex; index < endIndex; index++)
        {
            if (statements[index].StartsWithKeyword("IF"))
            {
                depth++;
                continue;
            }

            if (statements[index].StartsWithKeyword("ENDIF"))
            {
                if (depth == 0)
                {
                    return index;
                }

                depth--;
                continue;
            }

            if (depth == 0 && (statements[index].StartsWithKeyword("ELSEIF") || statements[index].StartsWithKeyword("ELSE")))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindMatchingEndIf(IReadOnlyList<Statement> statements, int startIndex, int endIndex)
    {
        var branch = FindNextIfBranch(statements, startIndex, endIndex);
        while (branch >= 0 && !statements[branch].StartsWithKeyword("ENDIF"))
        {
            branch = FindNextIfBranch(statements, branch + 1, endIndex);
        }

        return branch >= 0 ? branch : throw new BasicRuntimeException("IF block is missing ENDIF.");
    }

    private static int FindMatchingEndDef(IReadOnlyList<Statement> statements, int startIndex, int endIndex)
    {
        var depth = 0;
        for (var index = startIndex; index < endIndex; index++)
        {
            if (statements[index].StartsWithKeyword("DEF"))
            {
                depth++;
                continue;
            }

            if (statements[index].StartsWithKeyword("ENDDEF"))
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

    private static IReadOnlyList<Token> Slice(IReadOnlyList<Token> tokens, int start, int end)
        => tokens.Skip(start).Take(Math.Max(0, end - start)).ToArray();

    private static IReadOnlyList<IReadOnlyList<Token>> SplitTopLevel(IReadOnlyList<Token> tokens, TokenKind separator)
    {
        var result = new List<IReadOnlyList<Token>>();
        var depth = 0;
        var start = 0;
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            depth += token.Kind switch
            {
                TokenKind.OpenParen => 1,
                TokenKind.CloseParen => -1,
                _ => 0
            };

            if (depth == 0 && token.Kind == separator)
            {
                result.Add(Slice(tokens, start, index));
                start = index + 1;
            }
        }

        result.Add(Slice(tokens, start, tokens.Count));
        return result;
    }

    private static IReadOnlyList<PrintPart> SplitPrintParts(IReadOnlyList<Token> tokens)
    {
        var result = new List<PrintPart>();
        var depth = 0;
        var start = 0;
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            depth += token.Kind switch
            {
                TokenKind.OpenParen => 1,
                TokenKind.CloseParen => -1,
                _ => 0
            };

            if (depth == 0 && token.Kind is TokenKind.Comma or TokenKind.Semicolon)
            {
                result.Add(new PrintPart(Slice(tokens, start, index), token.Kind));
                start = index + 1;
            }
        }

        result.Add(new PrintPart(Slice(tokens, start, tokens.Count), null));
        return result;
    }

    private static BasicRuntimeException Error(Statement statement, string message)
        => new(message, statement.FirstToken.Line, statement.FirstToken.Column);

    private sealed record PrintPart(IReadOnlyList<Token> Tokens, TokenKind? Separator);
}
