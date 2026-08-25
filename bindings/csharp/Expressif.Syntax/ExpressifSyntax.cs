using TreeSitter;
using TsNode = TreeSitter.Node;

namespace Expressif.Syntax;

public static class ExpressifSyntax
{
    internal static IReadOnlySet<string> SupportedValueNodeTypes { get; } = new HashSet<string>
    {
        "array_literal", "boolean_literal", "incoming_value", "null_literal", "numeric_literal",
        "interval_literal", "quoted_literal", "record_access", "record_literal", "temporal_literal", "tuple_literal", "variable",
    };

    public static RootExpressionSyntax Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var language = new Language(GetGrammarLibraryName(), "tree_sitter_expressif");
        using var parser = new Parser(language);
        using var tree = parser.Parse(source) ?? throw new ExpressifSyntaxException("Tree-sitter did not produce a syntax tree.", []);
        var root = tree.RootNode;

        if (root.HasError)
        {
            var errors = CollectErrors(root).ToArray();
            if (errors.Length == 0)
                errors = [new SyntaxError(root.Type, Span(root), root.Text, false)];
            throw new ExpressifSyntaxException("The source contains syntax errors.", errors);
        }

        var expression = SingleNamedChild(root, "source_file");
        if (expression.Type == "root_expression")
            expression = SingleNamedChild(expression, "root_expression");

        return expression.Type switch
        {
            "open_expression" => BindOpen(expression),
            "closed_expression" => BindClosed(expression),
            _ => throw Unknown(expression),
        };
    }

    private static OpenExpressionSyntax BindOpen(TsNode node)
    {
        var expressions = node.NamedChildren.Select(BindExpression).ToArray();
        var source = expressions.FirstOrDefault() is TupleProjectionSyntax
            ? expressions[0]
            : null;
        var pipeline = expressions.Skip(source is null ? 0 : 1).ToArray();
        return new(Span(node), node.Text, source, pipeline);
    }

    private static ClosedExpressionSyntax BindClosed(TsNode node)
    {
        var children = node.NamedChildren.ToArray();
        if (children.Length == 0)
            throw Unknown(node);

        var value = BindValue(children[0]);
        var pipeline = children.Skip(1).Select(BindExpression).ToArray();
        return new(Span(node), node.Text, value, pipeline);
    }

    private static FunctionCallSyntax BindFunctionCall(TsNode node)
    {
        if (node.Type != "function_call")
            throw Unknown(node);

        var name = node.GetChildForField("name") ?? node.NamedChildren.FirstOrDefault(n => n.Type == "function_name")
            ?? throw Unknown(node);
        var argumentList = node.NamedChildren.FirstOrDefault(n => n.Type == "argument_list");
        var arguments = argumentList is null
            ? []
            : argumentList.NamedChildren.Select(BindArgument).ToArray();
        var suffix = node.Text.AsSpan(name.Text.Length).TrimStart();
        return new(Span(node), node.Text, name.Text, suffix.StartsWith("("), arguments);
    }

    private static ArgumentSyntax BindArgument(TsNode node) => node.Type switch
    {
        "positional_argument" => BindPositionalArgument(node),
        "named_argument" => BindNamedArgument(node),
        _ => throw Unknown(node),
    };

    private static PositionalArgumentSyntax BindPositionalArgument(TsNode node)
    {
        var valueNode = SingleNamedChild(node, "positional_argument");
        var value = BindExpression(valueNode);
        return new(Span(node), node.Text, value);
    }

    private static NamedArgumentSyntax BindNamedArgument(TsNode node)
    {
        var nameContainer = node.GetChildForField("name") ?? throw Unknown(node);
        var visibility = SingleNamedChild(nameContainer, nameContainer.Type);
        var name = visibility.Type == "public_argument_name"
            ? SingleNamedChild(visibility, visibility.Type)
            : visibility;
        var value = node.GetChildForField("value") ?? throw Unknown(node);
        QuotingStyle? quotingStyle = name.Type switch
        {
            "double_quoted_literal" => QuotingStyle.DoubleQuote,
            "backtick_quoted_literal" => QuotingStyle.Backtick,
            "unquoted_argument_name" or "private_argument_name" => null,
            _ => throw Unknown(name),
        };
        var nameValue = quotingStyle is null ? name.Text : name.Text[1..^1];
        var nameSyntax = new ArgumentNameSyntax(
            Span(visibility), visibility.Text, nameValue,
            visibility.Type == "private_argument_name", quotingStyle);
        return new(Span(node), node.Text, nameSyntax, BindExpression(value));
    }

    private static ParameterizedExpressionSyntax BindParameterizedExpression(TsNode node)
    {
        var source = node.GetChildForField("source") ?? throw Unknown(node);
        var expression = node.GetChildForField("expression") ?? throw Unknown(node);
        return new(Span(node), node.Text, BindExpression(source), BindOpen(expression));
    }

    private static ParenthesizedExpressionSyntax BindParenthesizedExpression(TsNode node)
    {
        var expression = node.GetChildForField("expression") ?? throw Unknown(node);
        return new(Span(node), node.Text, BindRootExpression(expression));
    }

    private static MapShorthandSyntax BindMapShorthand(TsNode node)
    {
        var expression = node.GetChildForField("expression") ?? throw Unknown(node);
        return new(Span(node), node.Text, BindOpen(expression));
    }

    private static UnaryExpressionSyntax BindUnaryExpression(TsNode node)
    {
        var @operator = node.GetChildForField("operator") ?? throw Unknown(node);
        var operand = node.GetChildForField("operand") ?? throw Unknown(node);
        return new(
            Span(node),
            node.Text,
            new UnaryOperatorSyntax(Span(@operator), @operator.Text),
            BindExpression(operand));
    }

    private static BinaryExpressionSyntax BindBinaryExpression(TsNode node)
    {
        var left = node.GetChildForField("left") ?? throw Unknown(node);
        var @operator = node.GetChildForField("operator") ?? throw Unknown(node);
        var right = node.GetChildForField("right") ?? throw Unknown(node);
        return new(
            Span(node),
            node.Text,
            BindExpression(left),
            new BinaryOperatorSyntax(Span(@operator), @operator.Text),
            BindExpression(right));
    }

    private static RootExpressionSyntax BindRootExpression(TsNode node)
    {
        if (node.Type == "root_expression")
            node = SingleNamedChild(node, "root_expression");

        return node.Type switch
        {
            "open_expression" => BindOpen(node),
            "closed_expression" => BindClosed(node),
            _ => throw Unknown(node),
        };
    }

    private static ExpressionSyntax BindExpression(TsNode node) => node.Type switch
    {
        "closed_expression" => BindClosed(node),
        "binary_expression" => BindBinaryExpression(node),
        "function_call" => BindFunctionCall(node),
        "map_shorthand" => BindMapShorthand(node),
        "open_expression" => BindOpen(node),
        "parameterized_expression" => BindParameterizedExpression(node),
        "parenthesized_expression" => BindParenthesizedExpression(node),
        "tuple_projection" => BindTupleProjection(node),
        "unary_expression" => BindUnaryExpression(node),
        _ => BindValue(node),
    };

    private static TupleProjectionSyntax BindTupleProjection(TsNode node)
    {
        var direction = node.GetChildForField("direction") ?? throw Unknown(node);
        var index = node.GetChildForField("index") ?? throw Unknown(node);
        var parsedDirection = direction.Type switch
        {
            "from_start" => TupleProjectionDirection.FromStart,
            "from_end" => TupleProjectionDirection.FromEnd,
            _ => throw Unknown(direction),
        };
        return new(
            Span(node),
            node.Text,
            parsedDirection,
            BindSemanticValue(index, () => int.Parse(index.Text, System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static ValueSyntax BindValue(TsNode node) => BindSemanticValue(node, () => node.Type switch
        {
            "variable" => new VariableSyntax(Span(node), node.Text),
            "incoming_value" => new IncomingValueSyntax(Span(node), node.Text),
            "record_access" => BindRecordAccess(node),
            "numeric_literal" => new NumericLiteralSyntax(Span(node), node.Text),
            "boolean_literal" => new BooleanLiteralSyntax(Span(node), node.Text),
            "null_literal" => new NullLiteralSyntax(Span(node), node.Text),
            "double_quoted_literal" => new QuotedLiteralSyntax(Span(node), node.Text, QuotingStyle.DoubleQuote),
            "backtick_quoted_literal" => new QuotedLiteralSyntax(Span(node), node.Text, QuotingStyle.Backtick),
            "date_literal" => new DateLiteralSyntax(Span(node), node.Text),
            "date_time_literal" => new DateTimeLiteralSyntax(Span(node), node.Text),
            "time_literal" => new TimeLiteralSyntax(Span(node), node.Text),
            "array_literal" => new ArrayLiteralSyntax(Span(node), node.Text, node.NamedChildren.Select(BindArrayElement).ToArray()),
            "tuple_literal" => new TupleLiteralSyntax(Span(node), node.Text, node.NamedChildren.Select(BindValue).ToArray()),
            "record_literal" => new RecordLiteralSyntax(Span(node), node.Text, node.NamedChildren.Select(BindRecordEntry).ToArray()),
            "interval_literal" => BindInterval(node),
            "value" or "quoted_literal" or "temporal_literal" => BindValue(SingleNamedChild(node, node.Type)),
            _ => throw Unknown(node),
        });

    private static ArrayElementSyntax BindArrayElement(TsNode node)
    {
        if (node.Type != "array_element")
            throw Unknown(node);

        var expression = node.GetChildForField("expression");
        return new(Span(node), node.Text, expression is null ? null : BindExpression(expression), node.GetChildForField("spread") is not null);
    }

    private static T BindSemanticValue<T>(TsNode node, Func<T> bind)
    {
        try
        {
            return bind();
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            throw new ExpressifSyntaxException(
                "The source contains a value that cannot be represented.",
                [new SyntaxError(node.Type, Span(node), node.Text, false)],
                exception);
        }
    }

    private static IntervalLiteralSyntax BindInterval(TsNode node)
    {
        var shorthand = node.NamedChildren.FirstOrDefault(child => child.Type == "interval_shorthand");
        if (shorthand is not null)
        {
            var zeroIndex = node.Text.IndexOf('0');
            var zero = zeroIndex < 0
                ? null
                : new NumericLiteralSyntax(new SourceSpan(node.StartIndex + zeroIndex, 1), "0");
            return shorthand.Text switch
            {
                "(0+)" => new(Span(node), node.Text,
                    new(IntervalBoundKind.Finite, zero), new(IntervalBoundKind.PositiveInfinity, null), true, true),
                "(+)" => new(Span(node), node.Text,
                    new(IntervalBoundKind.Finite, new NumericLiteralSyntax(new SourceSpan(node.StartIndex + 2, 0), "0")),
                    new(IntervalBoundKind.PositiveInfinity, null), false, true),
                "(0-)" => new(Span(node), node.Text,
                    new(IntervalBoundKind.NegativeInfinity, null), new(IntervalBoundKind.Finite, zero), true, true),
                "(-)" => new(Span(node), node.Text,
                    new(IntervalBoundKind.NegativeInfinity, null),
                    new(IntervalBoundKind.Finite, new NumericLiteralSyntax(new SourceSpan(node.StartIndex + 2, 0), "0")), true, false),
                "(positive)" => BindZeroInterval(node, true, true),
                "(negative)" => BindZeroInterval(node, false, true),
                "(absolutely-positive)" => BindZeroInterval(node, true, false),
                "(absolutely-negative)" => BindZeroInterval(node, false, false),
                _ when shorthand.Text[1] is '>' or '<' => BindComparisonInterval(node, shorthand.Text),
                _ => throw Unknown(shorthand),
            };
        }

        var lower = node.GetChildForField("lower_bound") ?? throw Unknown(node);
        var upper = node.GetChildForField("upper_bound") ?? throw Unknown(node);
        var lowerDelimiter = node.GetChildForField("lower_delimiter") ?? throw Unknown(node);
        var upperDelimiter = node.GetChildForField("upper_delimiter") ?? throw Unknown(node);
        return new(Span(node), node.Text, BindIntervalBound(lower), BindIntervalBound(upper),
            lowerDelimiter.Text == "[",
            upperDelimiter.Text == "]");
    }

    private static IntervalLiteralSyntax BindZeroInterval(TsNode node, bool positive, bool inclusive)
    {
        var zero = new NumericLiteralSyntax(new SourceSpan(node.StartIndex + 2, 0), "0");
        return positive
            ? new(Span(node), node.Text,
                new(IntervalBoundKind.Finite, zero), new(IntervalBoundKind.PositiveInfinity, null), inclusive, true)
            : new(Span(node), node.Text,
                new(IntervalBoundKind.NegativeInfinity, null), new(IntervalBoundKind.Finite, zero), true, inclusive);
    }

    private static IntervalLiteralSyntax BindComparisonInterval(TsNode node, string shorthand)
    {
        var operatorLength = shorthand[2] == '=' ? 2 : 1;
        var valueOffset = 1 + operatorLength;
        var valueText = shorthand[valueOffset..^1];
        var valueStart = node.Text.IndexOf(valueText, StringComparison.Ordinal);
        var value = new NumericLiteralSyntax(new SourceSpan(node.StartIndex + valueStart, valueText.Length), valueText);
        var inclusive = operatorLength == 2;
        return shorthand[1] == '>'
            ? new(Span(node), node.Text,
                new(IntervalBoundKind.Finite, value), new(IntervalBoundKind.PositiveInfinity, null), inclusive, true)
            : new(Span(node), node.Text,
                new(IntervalBoundKind.NegativeInfinity, null), new(IntervalBoundKind.Finite, value), true, inclusive);
    }

    private static IntervalBound BindIntervalBound(TsNode node)
    {
        var bound = SingleNamedChild(node, "interval_bound");
        return bound.Type == "infinite_bound"
            ? new(bound.Text == "+INF" ? IntervalBoundKind.PositiveInfinity : IntervalBoundKind.NegativeInfinity, null)
            : new(IntervalBoundKind.Finite, BindValue(bound));
    }

    private static RecordEntrySyntax BindRecordEntry(TsNode node) => node.Type switch
    {
        "record_field" => BindRecordField(node),
        "record_spread" => new RecordSpreadSyntax(Span(node), node.Text),
        _ => throw Unknown(node),
    };

    private static RecordAccessSyntax BindRecordAccess(TsNode node)
    {
        var fields = node.NamedChildren
            .Where(child => child.Type != "original_input")
            .Select(selector => SingleNamedChild(selector, selector.Type))
            .Select(field => field.Type switch
            {
                "named_record_field" => new RecordFieldSelector(field.Text.TrimStart('.'), null),
                "positional_record_field" => new RecordFieldSelector(null,
                    BindSemanticValue(field, () => int.Parse(
                        field.Text.AsSpan(field.Text[0] == '.' ? 1 : 0),
                        System.Globalization.CultureInfo.InvariantCulture))),
                _ => throw Unknown(field),
            });
        return new(Span(node), node.Text, node.NamedChildren.Any(child => child.Type == "original_input"), fields);
    }

    private static RecordFieldSyntax BindRecordField(TsNode node)
    {
        var nameContainer = node.GetChildForField("name") ?? throw Unknown(node);
        var visibility = SingleNamedChild(nameContainer, nameContainer.Type);
        var name = visibility.Type == "public_record_field_name"
            ? SingleNamedChild(visibility, visibility.Type)
            : visibility;
        var value = node.GetChildForField("value");
        QuotingStyle? quotingStyle = name.Type switch
        {
            "double_quoted_literal" => QuotingStyle.DoubleQuote,
            "backtick_quoted_literal" => QuotingStyle.Backtick,
            "unquoted_record_field_name" or "private_record_field_name" => null,
            _ => throw Unknown(name),
        };
        var nameText = quotingStyle is null ? name.Text : name.Text[1..^1];
        var nameSyntax = new RecordFieldNameSyntax(
            Span(visibility), visibility.Text, nameText,
            visibility.Type == "private_record_field_name", quotingStyle);
        return new(Span(node), node.Text, nameSyntax, value is null ? null : BindExpression(value), node.GetChildForField("spread") is not null);
    }

    private static TsNode SingleNamedChild(TsNode node, string container)
    {
        var children = node.NamedChildren.ToArray();
        return children.Length == 1
            ? children[0]
            : throw new ExpressifBindingException($"Expected one named child below '{container}', but found {children.Length}.");
    }

    private static IEnumerable<SyntaxError> CollectErrors(TsNode node)
    {
        if (node.IsError || node.IsMissing)
            yield return new SyntaxError(node.Type, Span(node), node.Text, node.IsMissing);

        foreach (var child in node.NamedChildren)
        foreach (var error in CollectErrors(child))
            yield return error;
    }

    private static SourceSpan Span(TsNode node) => new(node.StartIndex, node.EndIndex - node.StartIndex);
    private static ExpressifBindingException Unknown(TsNode node) =>
        new($"Cannot bind named Tree-sitter node type '{node.Type}' at {node.StartIndex}.");

    private static string GetGrammarLibraryName() => OperatingSystem.IsWindows()
        ? "tree-sitter-expressif.dll"
        : OperatingSystem.IsMacOS() ? "libtree-sitter-expressif.dylib" : "libtree-sitter-expressif.so";
}

public sealed record SyntaxError(string NodeType, SourceSpan Span, string Text, bool IsMissing);

public sealed class ExpressifSyntaxException : Exception
{
    internal ExpressifSyntaxException(
        string message,
        IReadOnlyList<SyntaxError> errors,
        Exception? innerException = null)
        : base(message, innerException) => Errors = errors;

    public IReadOnlyList<SyntaxError> Errors { get; }
}

public sealed class ExpressifBindingException : Exception
{
    internal ExpressifBindingException(string message) : base(message) { }
}
