using System.Collections.ObjectModel;

namespace Expressif.Syntax;

public enum SyntaxKind
{
    OpenExpression,
    ClosedExpression,
    FunctionCall,
    PositionalArgument,
    NumericLiteral,
    BooleanLiteral,
    QuotedLiteral,
    DateLiteral,
    DateTimeLiteral,
    TimeLiteral,
}

public readonly record struct SourceSpan(int Start, int Length)
{
    public int End => Start + Length;
}

public abstract class SyntaxNode
{
    private readonly ReadOnlyCollection<SyntaxNode> children;

    protected SyntaxNode(SyntaxKind kind, SourceSpan span, string text, IEnumerable<SyntaxNode>? children = null)
    {
        Kind = kind;
        Span = span;
        Text = text;
        this.children = Array.AsReadOnly((children ?? []).ToArray());
    }

    public SyntaxKind Kind { get; }
    public SourceSpan Span { get; }
    public string Text { get; }
    public IReadOnlyList<SyntaxNode> Children => children;
}

public abstract class RootExpressionSyntax : SyntaxNode
{
    protected RootExpressionSyntax(SyntaxKind kind, SourceSpan span, string text, IEnumerable<SyntaxNode> children)
        : base(kind, span, text, children) { }
}

public sealed class OpenExpressionSyntax : RootExpressionSyntax
{
    internal OpenExpressionSyntax(SourceSpan span, string text, IEnumerable<FunctionCallSyntax> pipeline)
        : base(SyntaxKind.OpenExpression, span, text, pipeline)
        => Pipeline = Array.AsReadOnly(pipeline.ToArray());

    public IReadOnlyList<FunctionCallSyntax> Pipeline { get; }
}

public sealed class ClosedExpressionSyntax : RootExpressionSyntax
{
    internal ClosedExpressionSyntax(SourceSpan span, string text, ValueSyntax value, IEnumerable<FunctionCallSyntax> pipeline)
        : base(SyntaxKind.ClosedExpression, span, text, new SyntaxNode[] { value }.Concat(pipeline))
    {
        Value = value;
        Pipeline = Array.AsReadOnly(pipeline.ToArray());
    }

    public ValueSyntax Value { get; }
    public IReadOnlyList<FunctionCallSyntax> Pipeline { get; }
}

public abstract class ExpressionSyntax : SyntaxNode
{
    protected ExpressionSyntax(SyntaxKind kind, SourceSpan span, string text, IEnumerable<SyntaxNode> children)
        : base(kind, span, text, children) { }
}

public sealed class FunctionCallSyntax : ExpressionSyntax
{
    internal FunctionCallSyntax(SourceSpan span, string text, string name, bool hasParentheses, IEnumerable<PositionalArgumentSyntax> arguments)
        : base(SyntaxKind.FunctionCall, span, text, arguments)
    {
        Name = name;
        HasParentheses = hasParentheses;
        Arguments = Array.AsReadOnly(arguments.ToArray());
    }

    public string Name { get; }
    public bool HasParentheses { get; }
    public IReadOnlyList<PositionalArgumentSyntax> Arguments { get; }
}

public abstract class ArgumentSyntax : SyntaxNode
{
    protected ArgumentSyntax(SyntaxKind kind, SourceSpan span, string text, IEnumerable<SyntaxNode> children)
        : base(kind, span, text, children) { }
}

public sealed class PositionalArgumentSyntax : ArgumentSyntax
{
    internal PositionalArgumentSyntax(SourceSpan span, string text, ValueSyntax value)
        : base(SyntaxKind.PositionalArgument, span, text, [value]) => Value = value;

    public ValueSyntax Value { get; }
}

public abstract class ValueSyntax : SyntaxNode
{
    protected ValueSyntax(SyntaxKind kind, SourceSpan span, string text)
        : base(kind, span, text) { }
}

public sealed class NumericLiteralSyntax : ValueSyntax
{
    internal NumericLiteralSyntax(SourceSpan span, string text) : base(SyntaxKind.NumericLiteral, span, text) { }
}

public sealed class BooleanLiteralSyntax : ValueSyntax
{
    internal BooleanLiteralSyntax(SourceSpan span, string text)
        : base(SyntaxKind.BooleanLiteral, span, text) => Value = text == "#true";

    public bool Value { get; }
}

public enum QuotingStyle { DoubleQuote, Backtick }

public sealed class QuotedLiteralSyntax : ValueSyntax
{
    internal QuotedLiteralSyntax(SourceSpan span, string text, QuotingStyle quotingStyle)
        : base(SyntaxKind.QuotedLiteral, span, text) => QuotingStyle = quotingStyle;

    public QuotingStyle QuotingStyle { get; }
}

public abstract class TemporalLiteralSyntax : ValueSyntax
{
    protected TemporalLiteralSyntax(SyntaxKind kind, SourceSpan span, string text) : base(kind, span, text) { }
}

public sealed class DateLiteralSyntax : TemporalLiteralSyntax
{
    internal DateLiteralSyntax(SourceSpan span, string text) : base(SyntaxKind.DateLiteral, span, text) { }
}

public sealed class DateTimeLiteralSyntax : TemporalLiteralSyntax
{
    internal DateTimeLiteralSyntax(SourceSpan span, string text) : base(SyntaxKind.DateTimeLiteral, span, text) { }
}

public sealed class TimeLiteralSyntax : TemporalLiteralSyntax
{
    internal TimeLiteralSyntax(SourceSpan span, string text) : base(SyntaxKind.TimeLiteral, span, text) { }
}
