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
    TupleProjection,
    Variable,
    RecordAccess,
    ArrayLiteral,
    TupleLiteral,
    RecordLiteral,
    RecordField,
    RecordSpread,
    IncomingValue,
    ParameterizedExpression,
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

public abstract class RootExpressionSyntax : ExpressionSyntax
{
    protected RootExpressionSyntax(SyntaxKind kind, SourceSpan span, string text, IEnumerable<SyntaxNode> children)
        : base(kind, span, text, children) { }
}

public sealed class OpenExpressionSyntax : RootExpressionSyntax
{
    internal OpenExpressionSyntax(SourceSpan span, string text, ExpressionSyntax? source, IEnumerable<ExpressionSyntax> pipeline)
        : base(SyntaxKind.OpenExpression, span, text,
            (source is null ? [] : new[] { source }).Concat<SyntaxNode>(pipeline))
    {
        Source = source;
        Pipeline = Array.AsReadOnly(pipeline.ToArray());
    }

    public ExpressionSyntax? Source { get; }
    public IReadOnlyList<ExpressionSyntax> Pipeline { get; }
}

public sealed class ClosedExpressionSyntax : RootExpressionSyntax
{
    internal ClosedExpressionSyntax(SourceSpan span, string text, ValueSyntax value, IEnumerable<ExpressionSyntax> pipeline)
        : base(SyntaxKind.ClosedExpression, span, text, new SyntaxNode[] { value }.Concat(pipeline))
    {
        Value = value;
        Pipeline = Array.AsReadOnly(pipeline.ToArray());
    }

    public ValueSyntax Value { get; }
    public IReadOnlyList<ExpressionSyntax> Pipeline { get; }
}

public abstract class ExpressionSyntax : SyntaxNode
{
    protected ExpressionSyntax(SyntaxKind kind, SourceSpan span, string text, IEnumerable<SyntaxNode>? children)
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

public sealed class ParameterizedExpressionSyntax : ExpressionSyntax
{
    internal ParameterizedExpressionSyntax(SourceSpan span, string text, ExpressionSyntax source, OpenExpressionSyntax expression)
        : base(SyntaxKind.ParameterizedExpression, span, text, [source, expression])
    {
        Source = source;
        Expression = expression;
    }

    public ExpressionSyntax Source { get; }
    public OpenExpressionSyntax Expression { get; }
}

public abstract class ArgumentSyntax : SyntaxNode
{
    protected ArgumentSyntax(SyntaxKind kind, SourceSpan span, string text, IEnumerable<SyntaxNode> children)
        : base(kind, span, text, children) { }
}

public sealed class PositionalArgumentSyntax : ArgumentSyntax
{
    internal PositionalArgumentSyntax(SourceSpan span, string text, ExpressionSyntax value)
        : base(SyntaxKind.PositionalArgument, span, text, [value]) => Value = value;

    public ExpressionSyntax Value { get; }
}

public abstract class ValueSyntax : ExpressionSyntax
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

public sealed class IncomingValueSyntax : ValueSyntax
{
    internal IncomingValueSyntax(SourceSpan span, string text) : base(SyntaxKind.IncomingValue, span, text) { }
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
    internal RecordLiteralSyntax(SourceSpan span, string text, IEnumerable<RecordEntrySyntax> entries)
        : base(SyntaxKind.RecordLiteral, span, text, entries)
    {
        Entries = Array.AsReadOnly(entries.ToArray());
        Fields = Array.AsReadOnly(Entries.OfType<RecordFieldSyntax>().ToArray());
    }

    public IReadOnlyList<RecordEntrySyntax> Entries { get; }
    public IReadOnlyList<RecordFieldSyntax> Fields { get; }
}

public abstract class RecordEntrySyntax : SyntaxNode
{
    protected RecordEntrySyntax(SyntaxKind kind, SourceSpan span, string text, IEnumerable<SyntaxNode>? children = null)
        : base(kind, span, text, children) { }
}

public sealed class RecordFieldSyntax : RecordEntrySyntax
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

public sealed class RecordSpreadSyntax : RecordEntrySyntax
{
    internal RecordSpreadSyntax(SourceSpan span, string text) : base(SyntaxKind.RecordSpread, span, text) { }
}

public enum TupleProjectionDirection { FromStart, FromEnd }

public sealed class TupleProjectionSyntax : ExpressionSyntax
{
    internal TupleProjectionSyntax(SourceSpan span, string text, TupleProjectionDirection direction, int index)
        : base(SyntaxKind.TupleProjection, span, text, null)
    {
        Direction = direction;
        Index = index;
    }

    public int Index { get; }
    public TupleProjectionDirection Direction { get; }
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
