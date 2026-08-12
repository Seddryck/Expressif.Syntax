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
    PositionalElementAccess,
    Variable,
    RecordAccess,
    ArrayLiteral,
    TupleLiteral,
    RecordLiteral,
    RecordField,
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
    protected ValueSyntax(SyntaxKind kind, SourceSpan span, string text, IEnumerable<SyntaxNode>? children = null)
        : base(kind, span, text, children) { }
}

public sealed class VariableSyntax : ValueSyntax
{
    internal VariableSyntax(SourceSpan span, string text) : base(SyntaxKind.Variable, span, text)
        => Name = text[1..];

    public string Name { get; }
}

public readonly record struct RecordFieldSelector(string? Name, int? Index)
{
    public bool IsNamed => Name is not null;
    public bool IsPositional => Index is not null;
}

public sealed class RecordAccessSyntax : ValueSyntax
{
    internal RecordAccessSyntax(SourceSpan span, string text, bool isOriginalInput, IEnumerable<RecordFieldSelector> fields)
        : base(SyntaxKind.RecordAccess, span, text)
    {
        IsOriginalInput = isOriginalInput;
        Fields = Array.AsReadOnly(fields.ToArray());
    }

    public bool IsOriginalInput { get; }
    public IReadOnlyList<RecordFieldSelector> Fields { get; }
}

public abstract class SequenceLiteralSyntax : ValueSyntax
{
    protected SequenceLiteralSyntax(SyntaxKind kind, SourceSpan span, string text, IEnumerable<ValueSyntax> values)
        : base(kind, span, text, values)
        => Values = Array.AsReadOnly(values.ToArray());

    public IReadOnlyList<ValueSyntax> Values { get; }
}

public sealed class ArrayLiteralSyntax : SequenceLiteralSyntax
{
    internal ArrayLiteralSyntax(SourceSpan span, string text, IEnumerable<ValueSyntax> values)
        : base(SyntaxKind.ArrayLiteral, span, text, values) { }
}

public sealed class TupleLiteralSyntax : SequenceLiteralSyntax
{
    internal TupleLiteralSyntax(SourceSpan span, string text, IEnumerable<ValueSyntax> values)
        : base(SyntaxKind.TupleLiteral, span, text, values) { }
}

public sealed class RecordLiteralSyntax : ValueSyntax
{
    internal RecordLiteralSyntax(SourceSpan span, string text, IEnumerable<RecordFieldSyntax> fields)
        : base(SyntaxKind.RecordLiteral, span, text, fields)
        => Fields = Array.AsReadOnly(fields.ToArray());

    public IReadOnlyList<RecordFieldSyntax> Fields { get; }
}

public sealed class RecordFieldSyntax : SyntaxNode
{
    internal RecordFieldSyntax(SourceSpan span, string text, string name, QuotingStyle? quotingStyle, ValueSyntax value)
        : base(SyntaxKind.RecordField, span, text, [value])
    {
        Name = name;
        QuotingStyle = quotingStyle;
        Value = value;
    }

    public string Name { get; }
    public QuotingStyle? QuotingStyle { get; }
    public ValueSyntax Value { get; }
}

public sealed class PositionalElementAccessSyntax : ValueSyntax
{
    internal PositionalElementAccessSyntax(SourceSpan span, string text)
        : base(SyntaxKind.PositionalElementAccess, span, text)
    {
        FromEnd = text[1] == '^';
        Index = int.Parse(text.AsSpan(FromEnd ? 2 : 1), System.Globalization.CultureInfo.InvariantCulture);
    }

    public int Index { get; }
    public bool FromEnd { get; }
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
