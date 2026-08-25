using System.Collections.ObjectModel;
using System.Globalization;

namespace Expressif.Syntax;

public enum SyntaxKind
{
    OpenExpression,
    ClosedExpression,
    FunctionCall,
    PositionalArgument,
    NamedArgument,
    ArgumentName,
    NumericLiteral,
    BooleanLiteral,
    NullLiteral,
    QuotedLiteral,
    DateLiteral,
    DateTimeLiteral,
    TimeLiteral,
    TupleProjection,
    Variable,
    RecordAccess,
    ArrayLiteral,
    ArrayElement,
    TupleLiteral,
    RecordLiteral,
    RecordField,
    RecordFieldName,
    RecordSpread,
    IncomingValue,
    ParameterizedExpression,
    ParenthesizedExpression,
    IntervalLiteral,
    MapShorthand,
    UnaryExpression,
    UnaryOperator,
    BinaryExpression,
    BinaryOperator,
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
    internal FunctionCallSyntax(SourceSpan span, string text, string name, bool hasParentheses, IEnumerable<ArgumentSyntax> arguments)
        : base(SyntaxKind.FunctionCall, span, text, arguments)
    {
        Name = name;
        HasParentheses = hasParentheses;
        Arguments = Array.AsReadOnly(arguments.ToArray());
    }

    public string Name { get; }
    public bool HasParentheses { get; }
    public IReadOnlyList<ArgumentSyntax> Arguments { get; }
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

public sealed class ParenthesizedExpressionSyntax : ExpressionSyntax
{
    internal ParenthesizedExpressionSyntax(SourceSpan span, string text, RootExpressionSyntax expression)
        : base(SyntaxKind.ParenthesizedExpression, span, text, [expression])
        => Expression = expression;

    public RootExpressionSyntax Expression { get; }
}

public sealed class MapShorthandSyntax : ExpressionSyntax
{
    internal MapShorthandSyntax(SourceSpan span, string text, OpenExpressionSyntax expression)
        : base(SyntaxKind.MapShorthand, span, text, [expression])
        => Expression = expression;

    public OpenExpressionSyntax Expression { get; }
}

public sealed class UnaryOperatorSyntax : SyntaxNode
{
    internal UnaryOperatorSyntax(SourceSpan span, string text)
        : base(SyntaxKind.UnaryOperator, span, text) { }
}

public sealed class UnaryExpressionSyntax : ExpressionSyntax
{
    internal UnaryExpressionSyntax(
        SourceSpan span,
        string text,
        UnaryOperatorSyntax @operator,
        ExpressionSyntax operand)
        : base(SyntaxKind.UnaryExpression, span, text, [@operator, operand])
    {
        Operator = @operator;
        Operand = operand;
    }

    public UnaryOperatorSyntax Operator { get; }
    public ExpressionSyntax Operand { get; }
}

public sealed class BinaryOperatorSyntax : SyntaxNode
{
    internal BinaryOperatorSyntax(SourceSpan span, string text)
        : base(SyntaxKind.BinaryOperator, span, text) { }
}

public sealed class BinaryExpressionSyntax : ExpressionSyntax
{
    internal BinaryExpressionSyntax(
        SourceSpan span,
        string text,
        ExpressionSyntax left,
        BinaryOperatorSyntax @operator,
        ExpressionSyntax right)
        : base(SyntaxKind.BinaryExpression, span, text, [left, @operator, right])
    {
        Left = left;
        Operator = @operator;
        Right = right;
    }

    public ExpressionSyntax Left { get; }
    public BinaryOperatorSyntax Operator { get; }
    public ExpressionSyntax Right { get; }
}

public abstract class ArgumentSyntax : SyntaxNode
{
    protected ArgumentSyntax(SyntaxKind kind, SourceSpan span, string text, IEnumerable<SyntaxNode> children)
        : base(kind, span, text, children) { }

    public abstract ExpressionSyntax Value { get; }
}

public sealed class PositionalArgumentSyntax : ArgumentSyntax
{
    internal PositionalArgumentSyntax(SourceSpan span, string text, ExpressionSyntax value)
        : base(SyntaxKind.PositionalArgument, span, text, [value]) => Value = value;

    public override ExpressionSyntax Value { get; }
}

public sealed class NamedArgumentSyntax : ArgumentSyntax
{
    internal NamedArgumentSyntax(SourceSpan span, string text, ArgumentNameSyntax name, ExpressionSyntax value)
        : base(SyntaxKind.NamedArgument, span, text, [name, value])
    {
        Name = name;
        Value = value;
    }

    public ArgumentNameSyntax Name { get; }
    public override ExpressionSyntax Value { get; }
}

public sealed class ArgumentNameSyntax : SyntaxNode
{
    internal ArgumentNameSyntax(
        SourceSpan span,
        string text,
        string value,
        bool isPrivate,
        QuotingStyle? quotingStyle)
        : base(SyntaxKind.ArgumentName, span, text)
    {
        Value = value;
        IsPrivate = isPrivate;
        QuotingStyle = quotingStyle;
    }

    public string Value { get; }
    public bool IsPrivate { get; }
    public QuotingStyle? QuotingStyle { get; }
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

public sealed class ArrayLiteralSyntax : ValueSyntax
{
    internal ArrayLiteralSyntax(SourceSpan span, string text, IEnumerable<ArrayElementSyntax> elements)
        : this(span, text, elements.ToArray()) { }

    private ArrayLiteralSyntax(SourceSpan span, string text, ArrayElementSyntax[] elements)
        : base(SyntaxKind.ArrayLiteral, span, text, elements)
    {
        Elements = Array.AsReadOnly(elements);
        Values = Array.AsReadOnly(elements.Select(element => element.Expression).ToArray());
    }

    public IReadOnlyList<ArrayElementSyntax> Elements { get; }
    public IReadOnlyList<ExpressionSyntax?> Values { get; }
}

public sealed class ArrayElementSyntax : SyntaxNode
{
    internal ArrayElementSyntax(SourceSpan span, string text, ExpressionSyntax? expression, bool isSpread)
        : base(SyntaxKind.ArrayElement, span, text, expression is null ? [] : [expression])
    {
        Expression = expression;
        IsSpread = isSpread;
    }

    public ExpressionSyntax? Expression { get; }
    public bool IsSpread { get; }
    public bool IsImplicitSpread => IsSpread && Expression is null;
}

public sealed class TupleLiteralSyntax : SequenceLiteralSyntax
{
    internal TupleLiteralSyntax(SourceSpan span, string text, IEnumerable<ValueSyntax> values)
        : base(SyntaxKind.TupleLiteral, span, text, values) { }
}

public sealed class RecordLiteralSyntax : ValueSyntax
{
    internal RecordLiteralSyntax(SourceSpan span, string text, IEnumerable<RecordEntrySyntax> entries)
        : this(span, text, entries.ToArray()) { }

    private RecordLiteralSyntax(SourceSpan span, string text, RecordEntrySyntax[] entries)
        : base(SyntaxKind.RecordLiteral, span, text, entries)
    {
        Entries = Array.AsReadOnly(entries);
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
    internal RecordFieldSyntax(SourceSpan span, string text, RecordFieldNameSyntax name, ExpressionSyntax? value, bool isSpread = false)
        : base(SyntaxKind.RecordField, span, text, value is null ? [name] : [name, value])
    {
        Name = name;
        Value = value;
        IsSpread = isSpread;
    }

    public RecordFieldNameSyntax Name { get; }
    public ExpressionSyntax? Value { get; }
    public bool IsSpread { get; }
    public bool IsImplicitSpread => IsSpread && Value is null;
}

public sealed class RecordFieldNameSyntax : SyntaxNode
{
    internal RecordFieldNameSyntax(
        SourceSpan span,
        string text,
        string value,
        bool isPrivate,
        QuotingStyle? quotingStyle)
        : base(SyntaxKind.RecordFieldName, span, text)
    {
        Value = value;
        IsPrivate = isPrivate;
        QuotingStyle = quotingStyle;
    }

    public string Value { get; }
    public bool IsPrivate { get; }
    public QuotingStyle? QuotingStyle { get; }
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
    internal NumericLiteralSyntax(SourceSpan span, string text)
        : base(SyntaxKind.NumericLiteral, span, text)
        => Value = decimal.Parse(
            text,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture);

    public decimal Value { get; }
}

public sealed class BooleanLiteralSyntax : ValueSyntax
{
    internal BooleanLiteralSyntax(SourceSpan span, string text)
        : base(SyntaxKind.BooleanLiteral, span, text) => Value = text == "#true";

    public bool Value { get; }
}

public sealed class NullLiteralSyntax : ValueSyntax
{
    internal NullLiteralSyntax(SourceSpan span, string text)
        : base(SyntaxKind.NullLiteral, span, text) { }

    public object? Value => null;
}

public enum QuotingStyle { DoubleQuote, Backtick }

public sealed class QuotedLiteralSyntax : ValueSyntax
{
    internal QuotedLiteralSyntax(SourceSpan span, string text, QuotingStyle quotingStyle)
        : base(SyntaxKind.QuotedLiteral, span, text)
    {
        QuotingStyle = quotingStyle;
        var content = text[1..^1];
        Value = quotingStyle is QuotingStyle.DoubleQuote
            ? content.Replace("\\\"", "\"").Replace("\\\\", "\\")
            : content;
    }

    public QuotingStyle QuotingStyle { get; }
    public string Value { get; }
}

public abstract class TemporalLiteralSyntax : ValueSyntax
{
    protected TemporalLiteralSyntax(SyntaxKind kind, SourceSpan span, string text) : base(kind, span, text) { }
}

public sealed class DateLiteralSyntax : TemporalLiteralSyntax
{
    internal DateLiteralSyntax(SourceSpan span, string text) : base(SyntaxKind.DateLiteral, span, text)
        => Value = DateOnly.ParseExact(text[2..^1], "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public DateOnly Value { get; }
}

public sealed class DateTimeLiteralSyntax : TemporalLiteralSyntax
{
    internal DateTimeLiteralSyntax(SourceSpan span, string text) : base(SyntaxKind.DateTimeLiteral, span, text)
        => Value = DateTime.ParseExact(
            text[2..^1],
            ["yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm:ss"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None);

    public DateTime Value { get; }
}

public sealed class TimeLiteralSyntax : TemporalLiteralSyntax
{
    internal TimeLiteralSyntax(SourceSpan span, string text) : base(SyntaxKind.TimeLiteral, span, text)
        => Value = TimeOnly.ParseExact(text[2..^1], "HH:mm:ss", CultureInfo.InvariantCulture);

    public TimeOnly Value { get; }
}

public enum IntervalBoundKind { Finite, NegativeInfinity, PositiveInfinity }

public readonly record struct IntervalBound(IntervalBoundKind Kind, ValueSyntax? Value)
{
    public bool IsInfinite => Kind is not IntervalBoundKind.Finite;
}

public sealed class IntervalLiteralSyntax : ValueSyntax
{
    internal IntervalLiteralSyntax(
        SourceSpan span,
        string text,
        IntervalBound lowerBound,
        IntervalBound upperBound,
        bool isLowerInclusive,
        bool isUpperInclusive)
        : base(SyntaxKind.IntervalLiteral, span, text,
            new[] { lowerBound.Value, upperBound.Value }.OfType<ValueSyntax>())
    {
        LowerBound = lowerBound;
        UpperBound = upperBound;
        IsLowerInclusive = isLowerInclusive;
        IsUpperInclusive = isUpperInclusive;
    }

    public IntervalBound LowerBound { get; }
    public IntervalBound UpperBound { get; }
    public bool IsLowerInclusive { get; }
    public bool IsUpperInclusive { get; }
}
