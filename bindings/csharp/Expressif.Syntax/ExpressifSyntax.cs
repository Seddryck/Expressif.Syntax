using TreeSitter;
using TsNode = TreeSitter.Node;

namespace Expressif.Syntax;

public static class ExpressifSyntax
{
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
        var calls = node.NamedChildren.Select(BindFunctionCall).ToArray();
        return new(Span(node), node.Text, calls);
    }

    private static ClosedExpressionSyntax BindClosed(TsNode node)
    {
        var children = node.NamedChildren.ToArray();
        if (children.Length == 0)
            throw Unknown(node);

        var value = BindValue(children[0]);
        var calls = children.Skip(1).Select(BindFunctionCall).ToArray();
        return new(Span(node), node.Text, value, calls);
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

    private static PositionalArgumentSyntax BindArgument(TsNode node)
    {
        if (node.Type != "positional_argument")
            throw Unknown(node);

        var valueNode = SingleNamedChild(node, "positional_argument");
        return new(Span(node), node.Text, BindValue(valueNode));
    }

    private static ValueSyntax BindValue(TsNode node) => node.Type switch
    {
        "positional_element_access" => new PositionalElementAccessSyntax(Span(node), node.Text),
        "numeric_literal" => new NumericLiteralSyntax(Span(node), node.Text),
        "boolean_literal" => new BooleanLiteralSyntax(Span(node), node.Text),
        "double_quoted_literal" => new QuotedLiteralSyntax(Span(node), node.Text, QuotingStyle.DoubleQuote),
        "backtick_quoted_literal" => new QuotedLiteralSyntax(Span(node), node.Text, QuotingStyle.Backtick),
        "date_literal" => new DateLiteralSyntax(Span(node), node.Text),
        "date_time_literal" => new DateTimeLiteralSyntax(Span(node), node.Text),
        "time_literal" => new TimeLiteralSyntax(Span(node), node.Text),
        "value" or "quoted_literal" or "temporal_literal" => BindValue(SingleNamedChild(node, node.Type)),
        _ => throw Unknown(node),
    };

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
    internal ExpressifSyntaxException(string message, IReadOnlyList<SyntaxError> errors) : base(message) => Errors = errors;
    public IReadOnlyList<SyntaxError> Errors { get; }
}

public sealed class ExpressifBindingException : Exception
{
    internal ExpressifBindingException(string message) : base(message) { }
}
