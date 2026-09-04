namespace Expressif.Syntax.Tests;

using System.Text.Json;

public class SyntaxBindingTests
{
    [Test]
    public void ParseDocumentPreservesCommentsAndCompleteSource()
    {
        const string source = "// leading\nlower(/* inner */ \"TEXT\") | trim // trailing\n/* block\n   comment */";

        var document = ExpressifSyntax.ParseDocument(source);

        Assert.Multiple(() =>
        {
            Assert.That(document.Kind, Is.EqualTo(SyntaxKind.SourceFile));
            Assert.That(document.Text, Is.EqualTo(source));
            Assert.That(document.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
            Assert.That(document.Expression, Is.TypeOf<OpenExpressionSyntax>());
            Assert.That(document.Comments, Has.Count.EqualTo(4));
            Assert.That(document.Comments.Select(comment => comment.Kind), Is.EqualTo(new[]
            {
                SyntaxKind.LineComment,
                SyntaxKind.BlockComment,
                SyntaxKind.LineComment,
                SyntaxKind.BlockComment,
            }));
            Assert.That(document.Comments.Select(comment => comment.Text), Is.EqualTo(new[]
            {
                "// leading",
                "/* inner */",
                "// trailing",
                "/* block\n   comment */",
            }));
            Assert.That(document.Children, Is.EqualTo(new SyntaxNode[]
            {
                document.Comments[0],
                document.Expression,
                document.Comments[1],
                document.Comments[2],
                document.Comments[3],
            }));
            Assert.That(document.Comments.Select(comment => comment.Children), Is.All.Empty);
        });
    }

    [Test]
    public void ParseRemainsCompatibleWhenCommentsArePresent()
    {
        const string source = "// before\nlower(/* before argument */ \"TEXT\") | /* between calls */ trim// after";

        var document = ExpressifSyntax.ParseDocument(source);
        var root = (OpenExpressionSyntax)document.Expression;

        Assert.Multiple(() =>
        {
            Assert.That(root.Pipeline, Has.Count.EqualTo(2));
            Assert.That(root.Pipeline.Select(expression => ((FunctionCallSyntax)expression).Name),
                Is.EqualTo(new[] { "lower", "trim" }));
            Assert.That(document.Comments, Has.Count.EqualTo(4));
        });
    }

    [Test]
    public void CommentMarkersInsideQuotedLiteralsRemainLiteralContent()
    {
        var document = ExpressifSyntax.ParseDocument("pair(\"// text\", `/* text */`)");

        Assert.That(document.Comments, Is.Empty);
    }

    [Test]
    public void UnterminatedBlockCommentIsRejected()
    {
        var exception = Assert.Throws<ExpressifSyntaxException>(() =>
            ExpressifSyntax.ParseDocument("lower /* never closed"));

        Assert.That(exception!.Errors, Is.Not.Empty);
    }

    [TestCase("lower", typeof(OpenExpressionSyntax))]
    [TestCase("lower() | trim", typeof(OpenExpressionSyntax))]
    [TestCase("true", typeof(OpenExpressionSyntax))]
    [TestCase("10", typeof(ClosedExpressionSyntax))]
    [TestCase("10 | add(5)", typeof(ClosedExpressionSyntax))]
    [TestCase("#true", typeof(ClosedExpressionSyntax))]
    [TestCase("\"foo\"", typeof(ClosedExpressionSyntax))]
    [TestCase("|> add(1)", typeof(OpenExpressionSyntax))]
    public void ParsePreservesRootExpressionKind(string source, Type expected)
        => Assert.That(ExpressifSyntax.Parse(source), Is.TypeOf(expected));

    [Test]
    public void MapShorthandPreservesAuthoredSyntaxAndNestedOpenExpression()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("|> add(1)");
        var shorthand = (MapShorthandSyntax)root.Pipeline.Single();
        var call = (FunctionCallSyntax)shorthand.Expression.Pipeline.Single();

        Assert.Multiple(() =>
        {
            Assert.That(root.Kind, Is.EqualTo(SyntaxKind.OpenExpression));
            Assert.That(root.Text, Is.EqualTo("|> add(1)"));
            Assert.That(root.Span, Is.EqualTo(new SourceSpan(0, 9)));
            Assert.That(root.Children, Is.EqualTo(new SyntaxNode[] { shorthand }));
            Assert.That(shorthand.Kind, Is.EqualTo(SyntaxKind.MapShorthand));
            Assert.That(shorthand.Text, Is.EqualTo("|> add(1)"));
            Assert.That(shorthand.Span, Is.EqualTo(new SourceSpan(0, 9)));
            Assert.That(shorthand.Children, Is.EqualTo(new SyntaxNode[] { shorthand.Expression }));
            Assert.That(shorthand.Expression.Text, Is.EqualTo("add(1)"));
            Assert.That(shorthand.Expression.Span, Is.EqualTo(new SourceSpan(3, 6)));
            Assert.That(call.Name, Is.EqualTo("add"));
            Assert.That(call.Arguments.Single().Value.Text, Is.EqualTo("1"));
        });
    }

    [Test]
    public void MapShorthandAcceptsRecordAccessAndPreservesOuterPipelineBoundary()
    {
        const string source = ".orders | filter(active) |> .amount | sum";
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        var shorthand = (MapShorthandSyntax)root.Pipeline[1];
        var access = (RecordAccessSyntax)shorthand.Expression.Pipeline.Single();

        Assert.Multiple(() =>
        {
            Assert.That(root.Text, Is.EqualTo(source));
            Assert.That(root.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
            Assert.That(root.Pipeline, Has.Count.EqualTo(3));
            Assert.That(shorthand.Kind, Is.EqualTo(SyntaxKind.MapShorthand));
            Assert.That(shorthand.Text, Is.EqualTo("|> .amount"));
            Assert.That(shorthand.Span, Is.EqualTo(new SourceSpan(25, 10)));
            Assert.That(shorthand.Children, Is.EqualTo(new SyntaxNode[] { shorthand.Expression }));
            Assert.That(shorthand.Expression.Text, Is.EqualTo(".amount"));
            Assert.That(shorthand.Expression.Span, Is.EqualTo(new SourceSpan(28, 7)));
            Assert.That(shorthand.Expression.Children, Is.EqualTo(new SyntaxNode[] { access }));
            Assert.That(access.Kind, Is.EqualTo(SyntaxKind.RecordAccess));
            Assert.That(access.Text, Is.EqualTo(".amount"));
            Assert.That(access.Fields.Single().Name, Is.EqualTo("amount"));
            Assert.That(root.Pipeline[2], Is.TypeOf<FunctionCallSyntax>()
                .With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("sum"));
        });
    }

    [Test]
    public void MapShorthandAcceptsRecordAccessInANestedClosedExpression()
    {
        const string source = "record(total:=.orders | filter(active) |> .amount | sum)";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var record = (FunctionCallSyntax)root.Pipeline.Single();
        var value = (ClosedExpressionSyntax)((NamedArgumentSyntax)record.Arguments.Single()).Value;
        var shorthand = (MapShorthandSyntax)value.Pipeline[1];

        Assert.Multiple(() =>
        {
            Assert.That(value.Pipeline, Has.Count.EqualTo(3));
            Assert.That(shorthand.Text, Is.EqualTo("|> .amount"));
            Assert.That(shorthand.Expression.Pipeline.Single(), Is.TypeOf<RecordAccessSyntax>()
                .With.Property(nameof(RecordAccessSyntax.Text)).EqualTo(".amount"));
            Assert.That(value.Pipeline[2], Is.TypeOf<FunctionCallSyntax>()
                .With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("sum"));
        });
    }

    [Test]
    public void MapShorthandComposesInAClosedExpressionPipeline()
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse("{1,2,3} |> add(1) | sum");
        var shorthand = (MapShorthandSyntax)root.Pipeline[0];

        Assert.Multiple(() =>
        {
            Assert.That(root.Pipeline, Has.Count.EqualTo(2));
            Assert.That(shorthand.Text, Is.EqualTo("|> add(1)"));
            Assert.That(shorthand.Expression.Pipeline.Single(), Is.TypeOf<FunctionCallSyntax>());
            Assert.That(root.Pipeline[1], Is.TypeOf<FunctionCallSyntax>()
                .With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("sum"));
        });
    }

    [Test]
    public void MapShorthandComposesAfterAnOpenExpression()
    {
        const string source = "filter(even) |> add(1)";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var filter = (FunctionCallSyntax)root.Pipeline[0];
        var shorthand = (MapShorthandSyntax)root.Pipeline[1];
        var add = (FunctionCallSyntax)shorthand.Expression.Pipeline.Single();

        Assert.Multiple(() =>
        {
            Assert.That(root.Text, Is.EqualTo(source));
            Assert.That(root.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
            Assert.That(root.Children, Is.EqualTo(new SyntaxNode[] { filter, shorthand }));
            Assert.That(filter.Name, Is.EqualTo("filter"));
            Assert.That(filter.Arguments.Single().Value.Text, Is.EqualTo("even"));
            Assert.That(shorthand.Kind, Is.EqualTo(SyntaxKind.MapShorthand));
            Assert.That(shorthand.Text, Is.EqualTo("|> add(1)"));
            Assert.That(shorthand.Span, Is.EqualTo(new SourceSpan(13, 9)));
            Assert.That(shorthand.Children, Is.EqualTo(new SyntaxNode[] { shorthand.Expression }));
            Assert.That(shorthand.Expression.Text, Is.EqualTo("add(1)"));
            Assert.That(add.Name, Is.EqualTo("add"));
            Assert.That(add.Arguments.Single().Value.Text, Is.EqualTo("1"));
        });
    }

    [Test]
    public void PipelinesAndFunctionCallDetailsPreserveSourceOrder()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("LOWER | text-to-lower() | unknown(5, 10)");

        Assert.Multiple(() =>
        {
            var calls = root.Pipeline.Cast<FunctionCallSyntax>().ToArray();
            Assert.That(calls.Select(x => x.Name), Is.EqualTo(new[] { "LOWER", "text-to-lower", "unknown" }));
            Assert.That(calls.Select(x => x.HasParentheses), Is.EqualTo(new[] { false, true, true }));
            Assert.That(calls[2].Arguments.Select(x => x.Value.Text), Is.EqualTo(new[] { "5", "10" }));
        });
    }

    private static IEnumerable<TestCaseData> NumericLiteralCases()
    {
        yield return new TestCaseData("0", 0m);
        yield return new TestCaseData("-0", 0m);
        yield return new TestCaseData("42", 42m);
        yield return new TestCaseData("-5", -5m);
        yield return new TestCaseData("3.14", 3.14m);
        yield return new TestCaseData("-3.14", -3.14m);
        yield return new TestCaseData("1.0", 1.0m);
        yield return new TestCaseData("1.500", 1.500m);
        yield return new TestCaseData("0.00100", 0.00100m);
        yield return new TestCaseData("79228162514264337593543950335", decimal.MaxValue);
    }

    [TestCaseSource(nameof(NumericLiteralCases))]
    public void NumericLiteralsExposeDecimalValue(string source, decimal expected)
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        var literal = (NumericLiteralSyntax)root.Value;
        Assert.Multiple(() =>
        {
            Assert.That(literal.Text, Is.EqualTo(source));
            Assert.That(literal.Value, Is.EqualTo(expected));
        });
    }

    [TestCase("#true", true)]
    [TestCase("#false", false)]
    public void BooleanLiteralsExposeTypedValue(string source, bool expected)
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        Assert.That(((BooleanLiteralSyntax)root.Value).Value, Is.EqualTo(expected));
    }

    [Test]
    public void NullLiteralExposesNullSemanticValueAndPreservesText()
    {
        var literal = (NullLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("#null")).Value;

        Assert.Multiple(() =>
        {
            Assert.That(literal.Kind, Is.EqualTo(SyntaxKind.NullLiteral));
            Assert.That(literal.Value, Is.Null);
            Assert.That(literal.Text, Is.EqualTo("#null"));
        });
    }

    [Test]
    public void NullLiteralComposesAsAnArgumentAndCollectionValue()
    {
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse("coalesce(#null)")).Pipeline.Single();
        var array = (ArrayLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("{#null, #true}")).Value;

        Assert.Multiple(() =>
        {
            Assert.That(call.Arguments.Single().Value, Is.TypeOf<NullLiteralSyntax>());
            Assert.That(array.Values[0], Is.TypeOf<NullLiteralSyntax>());
        });
    }

    [TestCase("true")]
    [TestCase("false")]
    public void BareBooleanWordsRemainFunctionCalls(string source)
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        Assert.That(((FunctionCallSyntax)root.Pipeline.Single()).Name, Is.EqualTo(source));
    }

    [TestCase("\"foo\"", QuotingStyle.DoubleQuote, "foo")]
    [TestCase("\"\"", QuotingStyle.DoubleQuote, "")]
    [TestCase("\"Alice said \\\"hello\\\".\"", QuotingStyle.DoubleQuote, "Alice said \"hello\".")]
    [TestCase("\"C:\\\\Temp\"", QuotingStyle.DoubleQuote, "C:\\Temp")]
    [TestCase("`foo`", QuotingStyle.Backtick, "foo")]
    [TestCase("` foo bar `", QuotingStyle.Backtick, " foo bar ")]
    public void QuotedLiteralsExposeDecodedValue(string source, QuotingStyle style, string expected)
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        var literal = (QuotedLiteralSyntax)root.Value;
        Assert.Multiple(() =>
        {
            Assert.That(literal.Text, Is.EqualTo(source));
            Assert.That(literal.QuotingStyle, Is.EqualTo(style));
            Assert.That(literal.Value, Is.EqualTo(expected));
        });
    }

    [TestCase("79228162514264337593543950336")]
    [TestCase("-79228162514264337593543950336")]
    public void NumericLiteralsOutsideDecimalRangeExposeSyntaxErrors(string source)
        => AssertSemanticValueError(source, "numeric_literal", source);

    [TestCase("#\"2025-12-17\"")]
    public void DateLiteralsExposeTypedValue(string source)
    {
        var literal = (DateLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;
        Assert.Multiple(() =>
        {
            Assert.That(literal.Text, Is.EqualTo(source));
            Assert.That(literal.Value, Is.EqualTo(new DateOnly(2025, 12, 17)));
        });
    }

    [TestCase("#\"2025-12-17T14:30:00\"")]
    [TestCase("#\"2025-12-17 14:30:00\"")]
    public void DateTimeLiteralsExposeTypedValue(string source)
    {
        var literal = (DateTimeLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;
        Assert.Multiple(() =>
        {
            Assert.That(literal.Text, Is.EqualTo(source));
            Assert.That(literal.Value, Is.EqualTo(new DateTime(2025, 12, 17, 14, 30, 0)));
            Assert.That(literal.Value.Kind, Is.EqualTo(DateTimeKind.Unspecified));
        });
    }

    [TestCase("#\"04:00:00\"")]
    public void TimeLiteralsExposeTypedValue(string source)
    {
        var literal = (TimeLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;
        Assert.Multiple(() =>
        {
            Assert.That(literal.Text, Is.EqualTo(source));
            Assert.That(literal.Value, Is.EqualTo(new TimeOnly(4, 0, 0)));
        });
    }

    [TestCase("#\"2025-02-29\"", "date_literal")]
    [TestCase("#\"2025-13-17\"", "date_literal")]
    [TestCase("#\"2025-12-17T25:30:00\"", "date_time_literal")]
    [TestCase("#\"24:00:00\"", "time_literal")]
    public void InvalidTemporalValuesExposeSyntaxErrors(string source, string nodeType)
        => AssertSemanticValueError(source, nodeType, source);

    [Test]
    public void TupleProjectionIndexesOutsideIntegerRangeExposeSyntaxErrors()
        => AssertSemanticValueError("$2147483648", "tuple_index", "2147483648");

    [Test]
    public void PositionalRecordSelectorsOutsideIntegerRangeExposeSyntaxErrors()
        => AssertSemanticValueError("^.2147483648", "positional_record_field", "2147483648");

    private static void AssertSemanticValueError(string source, string nodeType, string errorText)
    {
        var exception = Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse(source));
        Assert.Multiple(() =>
        {
            Assert.That(exception!.InnerException, Is.TypeOf<OverflowException>().Or.TypeOf<FormatException>());
            Assert.That(exception.Errors, Has.Count.EqualTo(1));
            Assert.That(exception.Errors[0].NodeType, Is.EqualTo(nodeType));
            Assert.That(exception.Errors[0].Text, Is.EqualTo(errorText));
            Assert.That(exception.Errors[0].IsMissing, Is.False);
        });
    }

    [TestCase("#\"2025-12-17\"", typeof(DateLiteralSyntax))]
    [TestCase("#\"2025-12-17T14:30:00\"", typeof(DateTimeLiteralSyntax))]
    [TestCase("#\"2025-12-17 14:30:00\"", typeof(DateTimeLiteralSyntax))]
    [TestCase("#\"14:30:00\"", typeof(TimeLiteralSyntax))]
    [TestCase("\"2025-12-17\"", typeof(QuotedLiteralSyntax))]
    public void TemporalFormsRemainDistinct(string source, Type expected)
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        Assert.That(root.Value, Is.TypeOf(expected).With.Property(nameof(SyntaxNode.Text)).EqualTo(source));
    }

    [TestCase("$0", 0, false)]
    [TestCase("$1", 1, false)]
    [TestCase("$^0", 0, true)]
    [TestCase("$^1", 1, true)]
    public void TupleProjectionExposesDirectionAndIndex(string source, int index, bool fromEnd)
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var projection = (TupleProjectionSyntax)root.Source!;

        Assert.Multiple(() =>
        {
            Assert.That(projection.Index, Is.EqualTo(index));
            Assert.That(projection.Direction, Is.EqualTo(fromEnd
                ? TupleProjectionDirection.FromEnd
                : TupleProjectionDirection.FromStart));
            Assert.That(projection.Text, Is.EqualTo(source));
        });
    }

    [Test]
    public void TupleProjectionCanBeAnArgumentAndPipelineSource()
    {
        var argumentRoot = (OpenExpressionSyntax)ExpressifSyntax.Parse("select($1)");
        var pipelineRoot = (OpenExpressionSyntax)ExpressifSyntax.Parse("$^0 | upper");

        Assert.Multiple(() =>
        {
            Assert.That(((FunctionCallSyntax)argumentRoot.Pipeline.Single()).Arguments.Single().Value,
                Is.TypeOf<TupleProjectionSyntax>());
            Assert.That(pipelineRoot.Source, Is.TypeOf<TupleProjectionSyntax>());
            Assert.That(pipelineRoot.Pipeline.Single(),
                Is.TypeOf<FunctionCallSyntax>().With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("upper"));
        });
    }

    [Test]
    public void ArrayAcceptsInputExpressionsAsElements()
    {
        const string source = "{ @foo | text-to-func(\"bar\") }";
        var array = (ArrayLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;
        var element = (ClosedExpressionSyntax)array.Values.Single()!;

        Assert.Multiple(() =>
        {
            Assert.That(array.Text, Is.EqualTo(source));
            Assert.That(array.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
            Assert.That(array.Children, Is.EqualTo(array.Elements));
            Assert.That(array.Elements.Single().Expression, Is.SameAs(element));
            Assert.That(array.Elements.Single().IsSpread, Is.False);
            Assert.That(element.Text, Is.EqualTo("@foo | text-to-func(\"bar\")"));
            Assert.That(element.Value, Is.TypeOf<VariableSyntax>()
                .With.Property(nameof(SyntaxNode.Text)).EqualTo("@foo"));
            Assert.That(element.Pipeline.Single(), Is.TypeOf<FunctionCallSyntax>());
        });
    }

    [Test]
    public void ArraySpreadElementsPreserveTheirMarkerAndExpression()
    {
        const string source = "{1, ...{2,3}, 4}";
        var array = (ArrayLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;
        var spread = array.Elements[1];

        Assert.Multiple(() =>
        {
            Assert.That(array.Elements.Select(element => element.IsSpread),
                Is.EqualTo(new[] { false, true, false }));
            Assert.That(array.Values, Is.EqualTo(array.Elements.Select(element => element.Expression)));
            Assert.That(array.Children, Is.EqualTo(array.Elements));
            Assert.That(array.Elements.Select(element => element.Kind), Is.All.EqualTo(SyntaxKind.ArrayElement));
            Assert.That(spread.Expression, Is.TypeOf<ArrayLiteralSyntax>());
            Assert.That(spread.Text, Is.EqualTo("...{2,3}"));
            Assert.That(spread.Span, Is.EqualTo(new SourceSpan(4, 8)));
            Assert.That(spread.Children, Is.EqualTo(new SyntaxNode[] { spread.Expression! }));
        });
    }

    [Test]
    public void BareArraySpreadUsesAnImplicitCurrentObject()
    {
        var array = (ArrayLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("{1, ..., 3}")).Value;
        var spread = array.Elements[1];

        Assert.Multiple(() =>
        {
            Assert.That(spread.IsSpread, Is.True);
            Assert.That(spread.IsImplicitSpread, Is.True);
            Assert.That(spread.Expression, Is.Null);
            Assert.That(spread.Text, Is.EqualTo("..."));
            Assert.That(spread.Span, Is.EqualTo(new SourceSpan(4, 3)));
            Assert.That(spread.Children, Is.Empty);
            Assert.That(array.Values[1], Is.Null);
        });
    }

    [Test]
    public void ExplicitArraySpreadsDistinguishCurrentObjectAndVariable()
    {
        var array = (ArrayLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("{...@_, ...@args}")).Value;

        Assert.Multiple(() =>
        {
            Assert.That(array.Elements.Select(element => element.IsSpread), Is.All.True);
            Assert.That(array.Elements.Select(element => element.IsImplicitSpread), Is.All.False);
            Assert.That(array.Elements[0].Expression, Is.TypeOf<IncomingValueSyntax>());
            Assert.That(array.Elements[1].Expression, Is.TypeOf<VariableSyntax>());
            Assert.That(array.Elements.Select(element => element.Text),
                Is.EqualTo(new[] { "...@_", "...@args" }));
        });
    }

    private static PositionalElementSyntax ParsePositionalSpread(string source)
    {
        var value = ((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;
        return value switch
        {
            ArrayLiteralSyntax array => array.Elements[1],
            TupleLiteralSyntax tuple => tuple.Elements[1],
            _ => throw new AssertionException($"Unexpected value syntax type {value.GetType().Name}."),
        };
    }

    [TestCase("{1, ..., 2}", null, true)]
    [TestCase("T(1, ..., 2)", null, true)]
    [TestCase("{1, ...@values, 2}", typeof(VariableSyntax), false)]
    [TestCase("T(1, ...@values, 2)", typeof(VariableSyntax), false)]
    [TestCase("{1, ...{3,4}, 2}", typeof(ArrayLiteralSyntax), false)]
    [TestCase("T(1, ...{3,4}, 2)", typeof(ArrayLiteralSyntax), false)]
    [TestCase("{1, ...T(3,4), 2}", typeof(TupleLiteralSyntax), false)]
    [TestCase("T(1, ...T(3,4), 2)", typeof(TupleLiteralSyntax), false)]
    [TestCase("{1, ...(@values |> append-space), 2}", typeof(ParenthesizedExpressionSyntax), false)]
    [TestCase("T(1, ...(@values |> append-space), 2)", typeof(ParenthesizedExpressionSyntax), false)]
    [TestCase("{1, ...(append-space), 2}", typeof(ParenthesizedExpressionSyntax), false)]
    [TestCase("T(1, ...(append-space), 2)", typeof(ParenthesizedExpressionSyntax), false)]
    [TestCase("{1, ...@values |> append-space, 2}", typeof(ClosedExpressionSyntax), false)]
    [TestCase("T(1, ...@values |> append-space, 2)", typeof(ClosedExpressionSyntax), false)]
    public void ArraysAndTuplesShareSpreadOperandSyntax(string source, Type? expressionType, bool isImplicit)
    {
        var spread = ParsePositionalSpread(source);

        Assert.Multiple(() =>
        {
            Assert.That(spread.IsSpread, Is.True);
            Assert.That(spread.IsImplicitSpread, Is.EqualTo(isImplicit));
            Assert.That(spread.Expression, expressionType is null ? Is.Null : Is.TypeOf(expressionType));
        });
    }

    [TestCase("{1, ...append-space, 2}")]
    [TestCase("T(1, ...append-space, 2)")]
    public void PositionalSpreadRejectsUnparenthesizedOpenExpressions(string source)
    {
        Assert.That(() => ExpressifSyntax.Parse(source), Throws.TypeOf<ExpressifSyntaxException>());
    }

    [Test]
    public void TupleElementsPreserveOrdinaryAndSpreadValuesInOrder()
    {
        const string source = "T(...T(1, 2), 3, ...T(4, 5), ...)";
        var tuple = (TupleLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;

        Assert.Multiple(() =>
        {
            Assert.That(tuple.Kind, Is.EqualTo(SyntaxKind.TupleLiteral));
            Assert.That(tuple.Text, Is.EqualTo(source));
            Assert.That(tuple.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
            Assert.That(tuple.Children, Is.EqualTo(tuple.Elements));
            Assert.That(tuple.Elements.Select(element => element.Kind), Is.All.EqualTo(SyntaxKind.TupleElement));
            Assert.That(tuple.Elements.Select(element => element.IsSpread),
                Is.EqualTo(new[] { true, false, true, true }));
            Assert.That(tuple.Elements.Select(element => element.IsImplicitSpread),
                Is.EqualTo(new[] { false, false, false, true }));
            Assert.That(tuple.Elements[0].Expression, Is.TypeOf<TupleLiteralSyntax>());
            Assert.That(tuple.Elements[1].Expression, Is.TypeOf<NumericLiteralSyntax>());
            Assert.That(tuple.Elements[2].Expression, Is.TypeOf<TupleLiteralSyntax>());
            Assert.That(tuple.Elements[3].Expression, Is.Null);
        });
    }

    [Test]
    public void TupleSpreadPreservesAuthoredSourceSpanAndChildren()
    {
        const string source = "T(0, ...T(1, 2), 3)";
        var tuple = (TupleLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;
        var spread = tuple.Elements[1];

        Assert.Multiple(() =>
        {
            Assert.That(spread.Text, Is.EqualTo("...T(1, 2)"));
            Assert.That(spread.Span, Is.EqualTo(new SourceSpan(5, 10)));
            Assert.That(spread.Children, Is.EqualTo(new SyntaxNode[] { spread.Expression! }));
            Assert.That(spread.Expression!.Text, Is.EqualTo("T(1, 2)"));
        });
    }

    [TestCase("T(...T(1, 2), 3)")]
    [TestCase("T(0, ...T(1, 2))")]
    [TestCase("T(...T(1, 2), ...T(3, 4))")]
    [TestCase("T(...)")]
    public void TupleAcceptsLeadingTrailingMultipleAndBareSpreads(string source)
        => Assert.That(((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value,
            Is.TypeOf<TupleLiteralSyntax>());

    [Test]
    public void TupleFunctionRemainsAFunctionCallWithSpreadArguments()
    {
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse("tuple(...T(1, 2), 3)")).Pipeline.Single();

        Assert.Multiple(() =>
        {
            Assert.That(call.Name, Is.EqualTo("tuple"));
            Assert.That(call.Arguments[0], Is.TypeOf<SpreadArgumentSyntax>());
            Assert.That(call.Arguments[0].Value, Is.TypeOf<TupleLiteralSyntax>());
            Assert.That(call.Arguments[1], Is.TypeOf<PositionalArgumentSyntax>());
        });
    }

    [Test]
    public void TupleProjectionCompositionCanBeAFunctionArgument()
    {
        const string source = "adjacent($1 | subtract($0) | multiply($1))";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var call = (FunctionCallSyntax)root.Pipeline.Single();
        var composition = (OpenExpressionSyntax)call.Arguments.Single().Value;

        Assert.Multiple(() =>
        {
            Assert.That(composition.Text, Is.EqualTo("$1 | subtract($0) | multiply($1)"));
            Assert.That(composition.Source, Is.TypeOf<TupleProjectionSyntax>()
                .With.Property(nameof(SyntaxNode.Text)).EqualTo("$1"));
            Assert.That(composition.Pipeline, Has.Count.EqualTo(2));
            Assert.That(composition.Children, Has.Count.EqualTo(3));
            Assert.That(composition.Span, Is.EqualTo(new SourceSpan(9, 32)));
        });
    }

    [Test]
    public void TupleProjectionCanFollowAFunctionCallInAnOpenPipeline()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("lower | $0");

        Assert.Multiple(() =>
        {
            Assert.That(root.Source, Is.Null);
            Assert.That(root.Pipeline, Has.Count.EqualTo(2));
            Assert.That(root.Pipeline[0], Is.TypeOf<FunctionCallSyntax>());
            Assert.That(root.Pipeline[1], Is.TypeOf<TupleProjectionSyntax>());
        });
    }

    [TestCase(".name", false, "name", null)]
    [TestCase(".10", false, null, 10)]
    [TestCase("^.name", true, "name", null)]
    [TestCase("^.10", true, null, 10)]
    public void RecordAccessExposesRootAndField(string source, bool original, string? name, int? index)
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        var access = (RecordAccessSyntax)root.Value;

        Assert.Multiple(() =>
        {
            Assert.That(access.IsOriginalInput, Is.EqualTo(original));
            Assert.That(access.Fields.Single().Name, Is.EqualTo(name));
            Assert.That(access.Fields.Single().Index, Is.EqualTo(index));
        });
    }

    [Test]
    public void NestedRecordAccessPreservesEveryField()
    {
        var access = (RecordAccessSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("^.customer.0")).Value;

        Assert.That(access.Fields, Is.EqualTo(new[]
        {
            new RecordFieldSelector("customer", null),
            new RecordFieldSelector(null, 0),
        }));
    }

    [TestCase("@value", typeof(VariableSyntax))]
    [TestCase("@!pi", typeof(ConstantReferenceSyntax))]
    [TestCase("{}", typeof(ArrayLiteralSyntax))]
    [TestCase("T(1, 2)", typeof(TupleLiteralSyntax))]
    [TestCase("{:}", typeof(RecordLiteralSyntax))]
    public void RemainingGrammarValuesHaveManagedSyntaxNodes(string source, Type expected)
        => Assert.That(((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value, Is.TypeOf(expected));

    [Test]
    public void ConstantReferenceIsALosslessValueNode()
    {
        const string source = "@!max-retries";
        var constant = (ConstantReferenceSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;

        Assert.Multiple(() =>
        {
            Assert.That(constant.Kind, Is.EqualTo(SyntaxKind.ConstantReference));
            Assert.That(constant.Name, Is.EqualTo("max-retries"));
            Assert.That(constant.Text, Is.EqualTo(source));
            Assert.That(constant.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
            Assert.That(constant.Children, Is.Empty);
        });
    }

    [Test]
    public void ConstantReferencesComposeAsArgumentsPipelinesAndCompoundValues()
    {
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse("foo(@!today)")).Pipeline.Single();
        var pipeline = (ClosedExpressionSyntax)ExpressifSyntax.Parse("@!pi | round");
        var record = (RecordLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("{limit := @!max-retries}")).Value;

        Assert.Multiple(() =>
        {
            Assert.That(call.Arguments.Single().Value, Is.TypeOf<ConstantReferenceSyntax>()
                .With.Property(nameof(ConstantReferenceSyntax.Name)).EqualTo("today"));
            Assert.That(pipeline.Value, Is.TypeOf<ConstantReferenceSyntax>()
                .With.Property(nameof(ConstantReferenceSyntax.Name)).EqualTo("pi"));
            Assert.That(record.Fields.Single().Value, Is.TypeOf<ConstantReferenceSyntax>()
                .With.Property(nameof(ConstantReferenceSyntax.Name)).EqualTo("max-retries"));
        });
    }

    [Test]
    public void CompoundValuesBindNestedValuesAndRecordFields()
    {
        var record = (RecordLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(
            "{name := @value, `scores` := {1, ^.total}}")).Value;

        Assert.Multiple(() =>
        {
            Assert.That(record.Fields.Select(field => field.Name.Value), Is.EqualTo(new[] { "name", "scores" }));
            Assert.That(record.Fields[1].Name.QuotingStyle, Is.EqualTo(QuotingStyle.Backtick));
            Assert.That(record.Fields[0].Value, Is.TypeOf<VariableSyntax>());
            Assert.That(((ArrayLiteralSyntax)record.Fields[1].Value!).Values[1], Is.TypeOf<RecordAccessSyntax>());
            Assert.That(record.Children, Is.EqualTo(record.Fields));
        });
    }

    [Test]
    public void IncomingValueIsALosslessValueNode()
    {
        var incoming = (IncomingValueSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("@_")).Value;

        Assert.Multiple(() =>
        {
            Assert.That(incoming.Kind, Is.EqualTo(SyntaxKind.IncomingValue));
            Assert.That(incoming.Text, Is.EqualTo("@_"));
            Assert.That(incoming.Span, Is.EqualTo(new SourceSpan(0, 2)));
            Assert.That(incoming.Children, Is.Empty);
        });
    }

    [TestCase(":text")]
    [TestCase(":integer")]
    [TestCase(":numeric")]
    [TestCase(":boolean")]
    [TestCase(":date")]
    [TestCase(":datetime")]
    [TestCase(":time")]
    [TestCase(":duration")]
    [TestCase(":array")]
    [TestCase(":tuple")]
    [TestCase(":record")]
    public void TypeLiteralsAreDedicatedLosslessValueNodes(string source)
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        var literal = (TypeLiteralSyntax)root.Value;

        Assert.Multiple(() =>
        {
            Assert.That(literal.Kind, Is.EqualTo(SyntaxKind.TypeLiteral));
            Assert.That(literal.Name, Is.EqualTo(source[1..]));
            Assert.That(literal.Text, Is.EqualTo(source));
            Assert.That(literal.Children, Is.Empty);
        });
    }

    [TestCase(":")]
    [TestCase(":1integer")]
    [TestCase(":integer-")]
    [TestCase(":integer_type")]
    public void MalformedTypeLiteralsAreRejected(string source)
    {
        Assert.That(() => ExpressifSyntax.Parse(source), Throws.TypeOf<ExpressifSyntaxException>());
    }

    [Test]
    public void RecordFieldsPreserveSpreadSemantics()
    {
        const string source = "{foo := ...args, bar := ...@args, current := ..., explicit := ...@_, baz := @value}";
        var record = (RecordLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;
        var first = record.Fields[0];

        Assert.Multiple(() =>
        {
            Assert.That(record.Fields.Select(field => field.IsSpread), Is.EqualTo(new[] { true, true, true, true, false }));
            Assert.That(record.Fields.Select(field => field.IsImplicitSpread),
                Is.EqualTo(new[] { false, false, true, false, false }));
            Assert.That(record.Fields.Select(field => field.Value?.Text),
                Is.EqualTo(new string?[] { "args", "@args", null, "@_", "@value" }));
            Assert.That(first.Value, Is.TypeOf<FunctionCallSyntax>());
            Assert.That(first.Text, Is.EqualTo("foo := ...args"));
            Assert.That(first.Span, Is.EqualTo(new SourceSpan(1, 14)));
            Assert.That(first.Children, Is.EqualTo(new SyntaxNode[] { first.Name, first.Value! }));
        });
    }

    [Test]
    public void CurrentObjectCanBeARegularCompoundValue()
    {
        var array = (ArrayLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("{1, @_, 3}")).Value;
        var record = (RecordLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("{foo := @_}")).Value;

        Assert.Multiple(() =>
        {
            Assert.That(array.Elements[1].IsSpread, Is.False);
            Assert.That(array.Elements[1].Expression, Is.TypeOf<IncomingValueSyntax>());
            Assert.That(record.Fields.Single().IsSpread, Is.False);
            Assert.That(record.Fields.Single().Value, Is.TypeOf<IncomingValueSyntax>());
        });
    }

    [Test]
    public void IncomingValueComposesAsAnArgumentAndPipelineSource()
    {
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse("foo(@_)")).Pipeline.Single();
        var pipeline = (ClosedExpressionSyntax)ExpressifSyntax.Parse("@_ | upper");

        Assert.Multiple(() =>
        {
            Assert.That(call.Arguments.Single().Value, Is.TypeOf<IncomingValueSyntax>());
            Assert.That(pipeline.Value, Is.TypeOf<IncomingValueSyntax>());
            Assert.That(pipeline.Pipeline.Single(),
                Is.TypeOf<FunctionCallSyntax>().With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("upper"));
        });
    }

    [Test]
    public void RecordSpreadRemainsDistinctAndPreservesEntryOrder()
    {
        const string source = "{ before := .name, ..., after := #true }";
        var record = (RecordLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;
        var spread = (RecordSpreadSyntax)record.Entries[1];

        Assert.Multiple(() =>
        {
            Assert.That(record.Entries, Has.Count.EqualTo(3));
            Assert.That(record.Entries.Select(entry => entry.Kind), Is.EqualTo(new[]
            {
                SyntaxKind.RecordField,
                SyntaxKind.RecordSpread,
                SyntaxKind.RecordField,
            }));
            Assert.That(record.Fields.Select(field => field.Name.Value), Is.EqualTo(new[] { "before", "after" }));
            Assert.That(record.Children, Is.EqualTo(record.Entries));
            Assert.That(spread.Text, Is.EqualTo("..."));
            Assert.That(spread.Span, Is.EqualTo(new SourceSpan(19, 3)));
            Assert.That(spread.Children, Is.Empty);
            Assert.That(record.Text, Is.EqualTo(source));
        });
    }

    [Test]
    public void RecordLiteralMaterializesEntriesOnlyOnce()
    {
        static IEnumerable<RecordEntrySyntax> CreateEntries()
        {
            yield return new RecordFieldSyntax(
                new SourceSpan(2, 10), "value := 1",
                new RecordFieldNameSyntax(new SourceSpan(2, 5), "value", "value", false, null),
                new NumericLiteralSyntax(new SourceSpan(11, 1), "1"));
            yield return new RecordSpreadSyntax(new SourceSpan(14, 3), "...");
        }

        var record = new RecordLiteralSyntax(new SourceSpan(0, 18), "{ value := 1, ... }", CreateEntries());

        Assert.Multiple(() =>
        {
            Assert.That(record.Children[0], Is.SameAs(record.Entries[0]));
            Assert.That(record.Children[1], Is.SameAs(record.Entries[1]));
            Assert.That(record.Fields[0], Is.SameAs(record.Entries[0]));
        });
    }

    [Test]
    public void SingletonIncomingValueInBracesIsARecordSpread()
    {
        var record = (RecordLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("{...}")).Value;

        Assert.That(record.Entries.Single(), Is.TypeOf<RecordSpreadSyntax>());
    }

    [Test]
    public void BareSpreadCanAppearBeforeAnotherArrayElement()
    {
        var array = (ArrayLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("{..., #true}")).Value;

        Assert.That(array.Elements.Select(element => element.IsImplicitSpread), Is.EqualTo(new[] { true, false }));
    }

    [Test]
    public void ParameterizedExpressionsPreserveSourceAndPipeline()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("skip-last-chars({@length | subtract(1) | max(0)})");
        var argument = ((FunctionCallSyntax)root.Pipeline.Single()).Arguments.Single();
        var parameterized = (ParameterizedExpressionSyntax)argument.Value;

        Assert.Multiple(() =>
        {
            Assert.That(parameterized.Source, Is.TypeOf<VariableSyntax>());
            Assert.That(((VariableSyntax)parameterized.Source).Name, Is.EqualTo("length"));
            Assert.That(parameterized.Expression.Pipeline.Cast<FunctionCallSyntax>().Select(call => call.Name),
                Is.EqualTo(new[] { "subtract", "max" }));
            Assert.That(parameterized.Children, Is.EqualTo(new SyntaxNode[] { parameterized.Source, parameterized.Expression }));
            Assert.That(parameterized.Text, Is.EqualTo("{@length | subtract(1) | max(0)}"));
        });
    }

    [TestCase("{{1, 2} | sum}", typeof(ArrayLiteralSyntax), "{1, 2}")]
    [TestCase("{T(1, 2) | some-function}", typeof(TupleLiteralSyntax), "T(1, 2)")]
    [TestCase("{{name := \"Alice\"} | some-function}", typeof(RecordLiteralSyntax), "{name := \"Alice\"}")]
    public void ParameterizedExpressionsPreserveCompoundSources(string argument, Type sourceType, string sourceText)
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse($"foo({argument})");
        var parameterized = (ParameterizedExpressionSyntax)((FunctionCallSyntax)root.Pipeline.Single()).Arguments.Single().Value;

        Assert.Multiple(() =>
        {
            Assert.That(parameterized.Source, Is.TypeOf(sourceType));
            Assert.That(parameterized.Source.Text, Is.EqualTo(sourceText));
            Assert.That(parameterized.Text, Is.EqualTo(argument));
            Assert.That(parameterized.Expression.Pipeline, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void ClosedExpressionCanBeAPositionalArgument()
    {
        const string source = "append(.firstName | Titlecase)";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var call = (FunctionCallSyntax)root.Pipeline.Single();
        var argument = call.Arguments.Single();
        var nested = (ClosedExpressionSyntax)argument.Value;

        Assert.Multiple(() =>
        {
            Assert.That(nested.Value, Is.TypeOf<RecordAccessSyntax>());
            Assert.That(((RecordAccessSyntax)nested.Value).Fields.Single().Name, Is.EqualTo("firstName"));
            Assert.That(nested.Pipeline.Cast<FunctionCallSyntax>().Select(item => item.Name),
                Is.EqualTo(new[] { "Titlecase" }));
            Assert.That(nested.Children, Is.EqualTo(new SyntaxNode[] { nested.Value, nested.Pipeline.Single() }));
            Assert.That(nested.Text, Is.EqualTo(".firstName | Titlecase"));
            Assert.That(nested.Span, Is.EqualTo(new SourceSpan(7, 22)));
            Assert.That(argument.Text, Is.EqualTo(nested.Text));
            Assert.That(argument.Children, Is.EqualTo(new SyntaxNode[] { nested }));
        });
    }

    [Test]
    public void NamedArgumentPreservesNameValueAndAuthoredSource()
    {
        const string source = "record(customer-name := .name)";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var call = (FunctionCallSyntax)root.Pipeline.Single();
        var argument = (NamedArgumentSyntax)call.Arguments.Single();

        Assert.Multiple(() =>
        {
            Assert.That(argument.Kind, Is.EqualTo(SyntaxKind.NamedArgument));
            Assert.That(argument.Name.Kind, Is.EqualTo(SyntaxKind.ArgumentName));
            Assert.That(argument.Name.Value, Is.EqualTo("customer-name"));
            Assert.That(argument.Name.IsPrivate, Is.False);
            Assert.That(argument.Name.QuotingStyle, Is.Null);
            Assert.That(argument.Name.Text, Is.EqualTo("customer-name"));
            Assert.That(argument.Name.Span, Is.EqualTo(new SourceSpan(7, 13)));
            Assert.That(argument.Value, Is.TypeOf<RecordAccessSyntax>());
            Assert.That(argument.Children, Is.EqualTo(new SyntaxNode[] { argument.Name, argument.Value }));
            Assert.That(argument.Text, Is.EqualTo("customer-name := .name"));
            Assert.That(argument.Span, Is.EqualTo(new SourceSpan(7, 22)));
            Assert.That(call.Text, Is.EqualTo(source));
        });
    }

    [TestCase("__NONAME_0", true, null)]
    [TestCase("\"display name\"", false, QuotingStyle.DoubleQuote)]
    [TestCase("`display name`", false, QuotingStyle.Backtick)]
    public void ArgumentNamesPreserveVisibilityQuotingAndAuthoredSource(
        string authoredName,
        bool isPrivate,
        QuotingStyle? quotingStyle)
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse($"example({authoredName} := 1)");
        var call = (FunctionCallSyntax)root.Pipeline.Single();
        var name = ((NamedArgumentSyntax)call.Arguments.Single()).Name;

        Assert.Multiple(() =>
        {
            Assert.That(name.Value, Is.EqualTo(authoredName.Trim('"', '`')));
            Assert.That(name.IsPrivate, Is.EqualTo(isPrivate));
            Assert.That(name.QuotingStyle, Is.EqualTo(quotingStyle));
            Assert.That(name.Text, Is.EqualTo(authoredName));
            Assert.That(name.Span, Is.EqualTo(new SourceSpan(8, authoredName.Length)));
            Assert.That(name.Children, Is.Empty);
        });
    }

    [Test]
    public void FunctionCallPreservesMixedArgumentOrder()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("example(10, rounding-mode := \"up\", 20)");
        var call = (FunctionCallSyntax)root.Pipeline.Single();

        Assert.That(call.Arguments.Select(argument => argument.Kind), Is.EqualTo(new[]
        {
            SyntaxKind.PositionalArgument,
            SyntaxKind.NamedArgument,
            SyntaxKind.PositionalArgument,
        }));
    }

    [TestCase("coerce(:integer)", new[] { "integer" })]
    [TestCase("coerce(:text, :integer)", new[] { "text", "integer" })]
    [TestCase("coerce(:text, :integer, :boolean)", new[] { "text", "integer", "boolean" })]
    public void TypeLiteralArgumentsPreserveTheirAuthoredOrder(string source, string[] expectedNames)
    {
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse(source)).Pipeline.Single();

        Assert.That(call.Arguments.Select(argument => ((TypeLiteralSyntax)argument.Value).Name),
            Is.EqualTo(expectedNames));
    }

    [TestCase("coerce(name -> :text)", new[] { "name" }, new[] { "text" })]
    [TestCase("coerce(name -> :text, age -> :integer)", new[] { "name", "age" }, new[] { "text", "integer" })]
    [TestCase("coerce($1 -> :text, $2 -> :integer)", new[] { "$1", "$2" }, new[] { "text", "integer" })]
    public void MappingArgumentsPreserveSelectorsOperatorsAndTypes(
        string source,
        string[] expectedSelectors,
        string[] expectedTypes)
    {
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse(source)).Pipeline.Single();
        var mappings = call.Arguments.Select(argument => (BinaryExpressionSyntax)argument.Value).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(mappings.Select(mapping => mapping.Left.Text), Is.EqualTo(expectedSelectors));
            Assert.That(mappings.Select(mapping => mapping.Operator.Text), Is.All.EqualTo("->"));
            Assert.That(mappings.Select(mapping => ((TypeLiteralSyntax)mapping.Right).Name), Is.EqualTo(expectedTypes));
            Assert.That(mappings.Select(mapping => mapping.Children.Count), Is.All.EqualTo(3));
        });
    }

    [TestCase("\"42\" | coerce(:integer)")]
    [TestCase("T(\"42\", \"Bob\") | coerce(:integer)")]
    [TestCase("T(\"Bob\", \"42\") | coerce(:text, :integer)")]
    [TestCase("{name := \"bob\", age := \"42\"} | coerce(age -> :integer)")]
    [TestCase("T(\"bob\", \"42\") | coerce($2 -> :integer)")]
    public void CoercionSyntaxComposesWithScalarTupleAndRecordInputs(string source)
    {
        Assert.That(ExpressifSyntax.Parse(source), Is.Not.Null);
    }

    [Test]
    public void MixedCoercionFormsRemainDistinctForSemanticValidation()
    {
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse(
            "coerce(:text, $2 -> :integer)")).Pipeline.Single();

        Assert.Multiple(() =>
        {
            Assert.That(call.Arguments[0].Value, Is.TypeOf<TypeLiteralSyntax>());
            Assert.That(call.Arguments[1].Value, Is.TypeOf<BinaryExpressionSyntax>());
        });
    }

    [Test]
    public void TypeMappingOperatorAssociatesLeftToRight()
    {
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse(
            "coerce(name -> :text -> :integer)")).Pipeline.Single();
        var outer = (BinaryExpressionSyntax)call.Arguments.Single().Value;

        Assert.Multiple(() =>
        {
            Assert.That(outer.Left, Is.TypeOf<BinaryExpressionSyntax>());
            Assert.That(outer.Operator.Text, Is.EqualTo("->"));
            Assert.That(outer.Right, Is.TypeOf<TypeLiteralSyntax>());
        });
    }

    [TestCase("coerce(name ->)")]
    [TestCase("coerce(-> :integer)")]
    public void MalformedTypeMappingsAreRejected(string source)
    {
        Assert.That(() => ExpressifSyntax.Parse(source), Throws.TypeOf<ExpressifSyntaxException>());
    }

    [Test]
    public void SpreadArgumentPreservesValueAndAuthoredSource()
    {
        const string source = "array(...@values)";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var call = (FunctionCallSyntax)root.Pipeline.Single();
        var argument = (SpreadArgumentSyntax)call.Arguments.Single();

        Assert.Multiple(() =>
        {
            Assert.That(argument.Kind, Is.EqualTo(SyntaxKind.SpreadArgument));
            Assert.That(argument.Value, Is.TypeOf<VariableSyntax>());
            Assert.That(((VariableSyntax)argument.Value).Name, Is.EqualTo("values"));
            Assert.That(argument.Children, Is.EqualTo(new SyntaxNode[] { argument.Value }));
            Assert.That(argument.Text, Is.EqualTo("...@values"));
            Assert.That(argument.Span, Is.EqualTo(new SourceSpan(6, 10)));
            Assert.That(call.Text, Is.EqualTo(source));
            Assert.That(argument.IsImplicitSpread, Is.False);
        });
    }

    [TestCase("array(...)")]
    [TestCase("example(...)")]
    [TestCase("array(0, ..., 4)")]
    [TestCase("array(0, ...@_, 4)")]
    [TestCase("@items | array(0, ..., 4)")]
    [TestCase("{1, 2, 3} | array(0, ..., 4)")]
    public void OperandlessSpreadArgumentsParseInEveryFunctionComposition(string source)
    {
        Assert.That(ExpressifSyntax.Parse(source), Is.Not.Null);
    }

    [Test]
    public void OperandlessSpreadArgumentHasNoValueOrChildren()
    {
        const string source = "array(0, ..., 4)";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var call = (FunctionCallSyntax)root.Pipeline.Single();
        var argument = (SpreadArgumentSyntax)call.Arguments[1];

        Assert.Multiple(() =>
        {
            Assert.That(argument.Kind, Is.EqualTo(SyntaxKind.SpreadArgument));
            Assert.That(argument.Value, Is.Null);
            Assert.That(argument.IsImplicitSpread, Is.True);
            Assert.That(argument.Children, Is.Empty);
            Assert.That(argument.Text, Is.EqualTo("..."));
            Assert.That(argument.Span, Is.EqualTo(new SourceSpan(9, 3)));
            Assert.That(call.Text, Is.EqualTo(source));
        });
    }

    [Test]
    public void IncomingValueSpreadArgumentRemainsExplicit()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("array(0, ...@_, 4)");
        var call = (FunctionCallSyntax)root.Pipeline.Single();
        var argument = (SpreadArgumentSyntax)call.Arguments[1];

        Assert.Multiple(() =>
        {
            Assert.That(argument.Value, Is.TypeOf<IncomingValueSyntax>());
            Assert.That(argument.IsImplicitSpread, Is.False);
            Assert.That(argument.Children, Is.EqualTo(new SyntaxNode[] { argument.Value! }));
        });
    }

    [Test]
    public void FunctionCallPreservesSpreadArgumentOrderAndTrailingComma()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("array(1, name := 2, ...@values,)");
        var call = (FunctionCallSyntax)root.Pipeline.Single();

        Assert.That(call.Arguments.Select(argument => argument.Kind), Is.EqualTo(new[]
        {
            SyntaxKind.PositionalArgument,
            SyntaxKind.NamedArgument,
            SyntaxKind.SpreadArgument,
        }));
    }

    [TestCase("{\"Nikola\", \"Tesla\"} | text(\"foo\", ..., \"bar\")")]
    [TestCase("text(\"foo\", ...{\"Nikola\", \"Tesla\"}, \"bar\")")]
    [TestCase("text(\"foo\", ...@names, \"bar\")")]
    [TestCase("text(\"foo\", ...{\"Nikola\", \"Tesla\"} | prepend-space, \"bar\")")]
    public void TextFunctionAcceptsSpreadArguments(string source)
    {
        var root = ExpressifSyntax.Parse(source);
        var call = root switch
        {
            OpenExpressionSyntax open => (FunctionCallSyntax)open.Pipeline.Last(),
            ClosedExpressionSyntax closed => (FunctionCallSyntax)closed.Pipeline.Last(),
            _ => throw new AssertionException($"Unexpected root syntax type {root.GetType().Name}."),
        };
        var spread = (SpreadArgumentSyntax)call.Arguments[1];

        Assert.Multiple(() =>
        {
            Assert.That(call.Name, Is.EqualTo("text"));
            Assert.That(call.Arguments.Select(argument => argument.Kind), Is.EqualTo(new[]
            {
                SyntaxKind.PositionalArgument,
                SyntaxKind.SpreadArgument,
                SyntaxKind.PositionalArgument,
            }));
            Assert.That(spread.IsImplicitSpread, Is.EqualTo(spread.Text == "..."));
            Assert.That(root.Text, Is.EqualTo(source));
        });
    }

    [TestCase("text(\"a\", ..., \"b\")", null, true)]
    [TestCase("text(\"a\", ...@values, \"b\")", typeof(VariableSyntax), false)]
    [TestCase("text(\"a\", ...{3,4}, \"b\")", typeof(ArrayLiteralSyntax), false)]
    [TestCase("text(\"a\", ...T(3,4), \"b\")", typeof(TupleLiteralSyntax), false)]
    [TestCase("text(\"a\", ...(@values |> append-space), \"b\")", typeof(ParenthesizedExpressionSyntax), false)]
    [TestCase("text(\"a\", ...(append-space), \"b\")", typeof(ParenthesizedExpressionSyntax), false)]
    [TestCase("text(\"a\", ...@values |> append-space, \"b\")", typeof(ClosedExpressionSyntax), false)]
    public void FunctionSpreadsSharePositionalSpreadOperandSyntax(string source, Type? valueType, bool isImplicit)
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var call = (FunctionCallSyntax)root.Pipeline.Single();
        var spread = (SpreadArgumentSyntax)call.Arguments[1];

        Assert.Multiple(() =>
        {
            Assert.That(spread.IsImplicitSpread, Is.EqualTo(isImplicit));
            Assert.That(spread.Value, valueType is null ? Is.Null : Is.TypeOf(valueType));
        });
    }

    [Test]
    public void FunctionSpreadRejectsUnparenthesizedOpenExpressions()
    {
        Assert.That(() => ExpressifSyntax.Parse("text(\"a\", ...append-space, \"b\")"),
            Throws.TypeOf<ExpressifSyntaxException>());
    }

    [Test]
    public void TextFunctionPreservesPipelinedSpreadOperand()
    {
        const string source = "text(\"foo\", ...{\"Nikola\", \"Tesla\"} | prepend-space, \"bar\")";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var call = (FunctionCallSyntax)root.Pipeline.Single();
        var spread = (SpreadArgumentSyntax)call.Arguments[1];
        var pipeline = (ClosedExpressionSyntax)spread.Value!;

        Assert.Multiple(() =>
        {
            Assert.That(pipeline.Value, Is.TypeOf<ArrayLiteralSyntax>());
            Assert.That(pipeline.Pipeline.Single(), Is.TypeOf<FunctionCallSyntax>()
                .With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("prepend-space"));
            Assert.That(spread.Text, Is.EqualTo("...{\"Nikola\", \"Tesla\"} | prepend-space"));
            Assert.That(spread.Children, Is.EqualTo(new SyntaxNode[] { pipeline }));
        });
    }

    [Test]
    public void OpenExpressionCanBeAPositionalArgument()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("broadcast(sum)");
        var broadcast = (FunctionCallSyntax)root.Pipeline.Single();
        var argument = broadcast.Arguments.Single();
        var nested = (OpenExpressionSyntax)argument.Value;

        Assert.Multiple(() =>
        {
            Assert.That(broadcast.Name, Is.EqualTo("broadcast"));
            Assert.That(nested.Pipeline.Single(),
                Is.TypeOf<FunctionCallSyntax>().With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("sum"));
            Assert.That(argument.Children, Is.EqualTo(new SyntaxNode[] { nested }));
        });
    }

    [Test]
    public void LeadingMapShorthandCanBeAPositionalArgument()
    {
        const string source = "summarize(|> .score | sum)";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var summarize = (FunctionCallSyntax)root.Pipeline.Single();
        var argument = summarize.Arguments.Single();
        var nested = (OpenExpressionSyntax)argument.Value;
        var shorthand = (MapShorthandSyntax)nested.Pipeline[0];

        Assert.Multiple(() =>
        {
            Assert.That(summarize.Name, Is.EqualTo("summarize"));
            Assert.That(nested.Text, Is.EqualTo("|> .score | sum"));
            Assert.That(nested.Pipeline, Has.Count.EqualTo(2));
            Assert.That(shorthand.Expression.Pipeline.Single(), Is.TypeOf<RecordAccessSyntax>()
                .With.Property(nameof(RecordAccessSyntax.Fields)).Count.EqualTo(1));
            Assert.That(nested.Pipeline[1], Is.TypeOf<FunctionCallSyntax>()
                .With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("sum"));
            Assert.That(argument.Children, Is.EqualTo(new SyntaxNode[] { nested }));
        });
    }

    [Test]
    public void ParenthesizedPipelineIsLossless()
    {
        const string source = "(absolute | add(5))";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var grouped = (ParenthesizedExpressionSyntax)root.Pipeline.Single();
        var expression = (OpenExpressionSyntax)grouped.Expression;

        Assert.Multiple(() =>
        {
            Assert.That(grouped.Kind, Is.EqualTo(SyntaxKind.ParenthesizedExpression));
            Assert.That(grouped.Text, Is.EqualTo(source));
            Assert.That(grouped.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
            Assert.That(grouped.Children, Is.EqualTo(new SyntaxNode[] { expression }));
            Assert.That(expression.Text, Is.EqualTo("absolute | add(5)"));
            Assert.That(expression.Pipeline.Cast<FunctionCallSyntax>().Select(call => call.Name),
                Is.EqualTo(new[] { "absolute", "add" }));
        });
    }

    [Test]
    public void UnaryShorthandPreservesOperatorOperandAndAuthoredSource()
    {
        const string source = " ! less-than(5) ";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var unary = (UnaryExpressionSyntax)root.Pipeline.Single();

        Assert.Multiple(() =>
        {
            Assert.That(unary.Kind, Is.EqualTo(SyntaxKind.UnaryExpression));
            Assert.That(unary.Operator.Kind, Is.EqualTo(SyntaxKind.UnaryOperator));
            Assert.That(unary.Operator.Text, Is.EqualTo("!"));
            Assert.That(unary.Operator.Span, Is.EqualTo(new SourceSpan(1, 1)));
            Assert.That(unary.Operand, Is.TypeOf<FunctionCallSyntax>()
                .With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("less-than"));
            Assert.That(unary.Children, Is.EqualTo(new SyntaxNode[] { unary.Operator, unary.Operand }));
            Assert.That(unary.Text, Is.EqualTo("! less-than(5)"));
            Assert.That(unary.Span, Is.EqualTo(new SourceSpan(1, 14)));
        });
    }

    [TestCase("*trim", typeof(FunctionCallSyntax))]
    [TestCase("*add(1)", typeof(FunctionCallSyntax))]
    [TestCase("*(trim | append-space)", typeof(ParenthesizedExpressionSyntax))]
    public void GuardedExpressionsPreserveTheirExactOperandBoundary(string source, Type expressionType)
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var guarded = (GuardedExpressionSyntax)root.Pipeline.Single();

        Assert.Multiple(() =>
        {
            Assert.That(guarded.Kind, Is.EqualTo(SyntaxKind.GuardedExpression));
            Assert.That(guarded.Expression, Is.TypeOf(expressionType));
            Assert.That(guarded.Text, Is.EqualTo(source));
            Assert.That(guarded.Children, Is.EqualTo(new SyntaxNode[] { guarded.Expression }));
            Assert.That(guarded.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
        });
    }

    [TestCase("walk(*trim)", typeof(FunctionCallSyntax))]
    [TestCase("walk(*add(1))", typeof(FunctionCallSyntax))]
    [TestCase("walk(*(trim | append-space))", typeof(ParenthesizedExpressionSyntax))]
    public void GuardedExpressionsComposeAsFunctionArguments(string source, Type expressionType)
    {
        var walk = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse(source)).Pipeline.Single();
        var guarded = (GuardedExpressionSyntax)walk.Arguments.Single().Value;

        Assert.That(guarded.Expression, Is.TypeOf(expressionType));
    }

    [TestCase("42 | *trim | append-space")]
    [TestCase("42|*trim|append-space")]
    [TestCase("42 |* trim| append-space")]
    public void BareGuardOnlyScopesTheFollowingPipelineStage(string source)
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        var guarded = (GuardedExpressionSyntax)root.Pipeline[0];

        Assert.Multiple(() =>
        {
            Assert.That(root.Pipeline, Has.Count.EqualTo(2));
            Assert.That(guarded.Expression, Is.TypeOf<FunctionCallSyntax>());
            Assert.That(((FunctionCallSyntax)guarded.Expression).Name, Is.EqualTo("trim"));
            Assert.That(root.Pipeline[1], Is.TypeOf<FunctionCallSyntax>()
                .With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("append-space"));
        });
    }

    [TestCase("42 | *(trim | append-space)")]
    [TestCase("42|*(trim|append-space)")]
    public void ParenthesesExtendTheGuardAcrossTheCompletePipeline(string source)
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        var guarded = (GuardedExpressionSyntax)root.Pipeline.Single();
        var grouped = (ParenthesizedExpressionSyntax)guarded.Expression;
        var expression = (OpenExpressionSyntax)grouped.Expression;

        Assert.Multiple(() =>
        {
            Assert.That(expression.Pipeline.Cast<FunctionCallSyntax>().Select(call => call.Name),
                Is.EqualTo(new[] { "trim", "append-space" }));
            Assert.That(guarded.Text.StartsWith("*", StringComparison.Ordinal), Is.True);
            Assert.That(guarded.Text.Contains("|*", StringComparison.Ordinal), Is.False);
        });
    }

    [Test]
    public void GuardMarkerRequiresAnExpression()
    {
        Assert.That(() => ExpressifSyntax.Parse("*"), Throws.TypeOf<ExpressifSyntaxException>());
    }

    [Test]
    public void RepeatedUnaryShorthandRemainsNestedSyntax()
    {
        var outer = (UnaryExpressionSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse("!!is-null")).Pipeline.Single();
        var inner = (UnaryExpressionSyntax)outer.Operand;

        Assert.Multiple(() =>
        {
            Assert.That(inner.Operand, Is.TypeOf<FunctionCallSyntax>()
                .With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("is-null"));
            Assert.That(outer.Text, Is.EqualTo("!!is-null"));
            Assert.That(inner.Text, Is.EqualTo("!is-null"));
        });
    }

    [TestCase("{foo := 10, bar := 20}")]
    [TestCase("{_tmp := 20, _x1 := 30}")]
    [TestCase("{foo := 10, _internalValue := 20, bar := 30}")]
    public void PublicPrivateAndMixedRecordFieldsParse(string source)
        => Assert.That(ExpressifSyntax.Parse(source), Is.TypeOf<ClosedExpressionSyntax>());

    [Test]
    public void RecordFieldNamesExposeVisibilityAndPreserveSource()
    {
        const string source = "{foo := 10, _tmp := 20, `display name` := 30}";
        var record = (RecordLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;
        var names = record.Fields.Select(field => field.Name).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(names.Select(name => name.Kind), Is.All.EqualTo(SyntaxKind.RecordFieldName));
            Assert.That(names.Select(name => name.Value), Is.EqualTo(new[] { "foo", "_tmp", "display name" }));
            Assert.That(names.Select(name => name.IsPrivate), Is.EqualTo(new[] { false, true, false }));
            Assert.That(names.Select(name => name.Text), Is.EqualTo(new[] { "foo", "_tmp", "`display name`" }));
            Assert.That(names.Select(name => name.Span), Is.EqualTo(new[]
            {
                new SourceSpan(1, 3),
                new SourceSpan(12, 4),
                new SourceSpan(24, 14),
            }));
            Assert.That(names[2].QuotingStyle, Is.EqualTo(QuotingStyle.Backtick));
            Assert.That(names.SelectMany(name => name.Children), Is.Empty);
            Assert.That(record.Fields[1].Children,
                Is.EqualTo(new SyntaxNode[] { names[1], record.Fields[1].Value! }));
        });
    }

    [TestCase("123 | !equal-to(125)")]
    [TestCase("123 | ! equal-to(125) ")]
    [TestCase("123 | !equal-to(125) |OR even ")]
    [TestCase("123 | ( ! equal-to(125) ) ")]
    [TestCase("123 | ( ! equal-to(125) |OR even ) |AND !null ")]
    public void NegatedPredicatesParseInPipelinesAndGroupedExpressions(string source)
    {
        var root = ExpressifSyntax.Parse(source);

        Assert.That(root, Is.TypeOf<ClosedExpressionSyntax>());
    }

    [TestCase("|AND")]
    [TestCase("|OR")]
    [TestCase("|XOR")]
    public void BinaryShorthandPreservesOperatorAndOperands(string operatorText)
    {
        var source = $"foo {operatorText} bar";
        var binary = (BinaryExpressionSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse(source)).Pipeline.Single();

        Assert.Multiple(() =>
        {
            Assert.That(binary.Kind, Is.EqualTo(SyntaxKind.BinaryExpression));
            Assert.That(binary.Left, Is.TypeOf<FunctionCallSyntax>()
                .With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("foo"));
            Assert.That(binary.Operator.Kind, Is.EqualTo(SyntaxKind.BinaryOperator));
            Assert.That(binary.Operator.Text, Is.EqualTo(operatorText));
            Assert.That(binary.Right, Is.TypeOf<FunctionCallSyntax>()
                .With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("bar"));
            Assert.That(binary.Children, Is.EqualTo(new SyntaxNode[] { binary.Left, binary.Operator, binary.Right }));
            Assert.That(binary.Text, Is.EqualTo(source));
            Assert.That(binary.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
        });
    }

    [Test]
    public void BinaryShorthandChainsLeftAssociatively()
    {
        var outer = (BinaryExpressionSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse("a |AND b |OR c")).Pipeline.Single();
        var left = (BinaryExpressionSyntax)outer.Left;

        Assert.Multiple(() =>
        {
            Assert.That(left.Operator.Text, Is.EqualTo("|AND"));
            Assert.That(outer.Operator.Text, Is.EqualTo("|OR"));
            Assert.That(((FunctionCallSyntax)outer.Right).Name, Is.EqualTo("c"));
        });
    }

    [Test]
    public void BinaryShorthandAcceptsGenericExpressionOperands()
    {
        var binary = (BinaryExpressionSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse("$0 |AND .active")).Pipeline.Single();

        Assert.Multiple(() =>
        {
            Assert.That(binary.Left, Is.TypeOf<TupleProjectionSyntax>());
            Assert.That(binary.Right, Is.TypeOf<RecordAccessSyntax>());
        });
    }

    [Test]
    public void UnaryShorthandBindsMoreTightlyThanBinaryShorthand()
    {
        var binary = (BinaryExpressionSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse("!foo |AND bar")).Pipeline.Single();

        Assert.That(binary.Left, Is.TypeOf<UnaryExpressionSyntax>());
    }

    [Test]
    public void ParenthesesGroupShorthandWithoutLoweringItToFunctions()
    {
        var outer = (BinaryExpressionSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse("(a |OR b) |AND c")).Pipeline.Single();
        var parenthesized = (ParenthesizedExpressionSyntax)outer.Left;
        var innerRoot = (OpenExpressionSyntax)parenthesized.Expression;
        var inner = (BinaryExpressionSyntax)innerRoot.Pipeline.Single();

        Assert.Multiple(() =>
        {
            Assert.That(parenthesized.Text, Is.EqualTo("(a |OR b)"));
            Assert.That(inner.Operator.Text, Is.EqualTo("|OR"));
            Assert.That(outer.Operator.Text, Is.EqualTo("|AND"));
            Assert.That(innerRoot.Pipeline, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void ShorthandComposesAsAnArgumentAndWithAnOrdinaryPipeline()
    {
        var argumentRoot = (OpenExpressionSyntax)ExpressifSyntax.Parse("foo(!less-than(5))");
        var pipelineRoot = (OpenExpressionSyntax)ExpressifSyntax.Parse("foo |AND bar | some-function");

        Assert.Multiple(() =>
        {
            var foo = (FunctionCallSyntax)argumentRoot.Pipeline.Single();
            Assert.That(foo.Arguments.Single().Value, Is.TypeOf<UnaryExpressionSyntax>());
            Assert.That(pipelineRoot.Pipeline[0], Is.TypeOf<BinaryExpressionSyntax>());
            Assert.That(pipelineRoot.Pipeline[1], Is.TypeOf<FunctionCallSyntax>()
                .With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("some-function"));
        });
    }

    [Test]
    public void ParenthesizedPipelineComposesAsAnArgumentAndPipelineOperation()
    {
        var argumentRoot = (OpenExpressionSyntax)ExpressifSyntax.Parse("map((absolute | add(5)))");
        var pipelineRoot = (OpenExpressionSyntax)ExpressifSyntax.Parse("trim | (lower | upper)");

        Assert.Multiple(() =>
        {
            var map = (FunctionCallSyntax)argumentRoot.Pipeline.Single();
            Assert.That(map.Arguments.Single().Value, Is.TypeOf<ParenthesizedExpressionSyntax>());
            Assert.That(pipelineRoot.Pipeline[1], Is.TypeOf<ParenthesizedExpressionSyntax>());
        });
    }

    [Test]
    public void ParenthesizedClosedPipelineCanBeGrouped()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("(5 | add(2)) | multiply(3)");
        var grouped = (ParenthesizedExpressionSyntax)root.Pipeline[0];

        Assert.Multiple(() =>
        {
            Assert.That(grouped.Expression, Is.TypeOf<ClosedExpressionSyntax>());
            Assert.That(root.Pipeline[1],
                Is.TypeOf<FunctionCallSyntax>().With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("multiply"));
        });
    }

    [Test]
    public void ParenthesizedMapPipelinePreservesOuterPipelineBoundary()
    {
        const string source = "{-1,2,-3} |> (absolute | add(5)) | reverse";
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        var map = (MapShorthandSyntax)root.Pipeline[0];
        var grouped = (ParenthesizedExpressionSyntax)map.Expression.Pipeline.Single();

        Assert.Multiple(() =>
        {
            Assert.That(map.Kind, Is.EqualTo(SyntaxKind.MapShorthand));
            Assert.That(map.Text, Is.EqualTo("|> (absolute | add(5))"));
            Assert.That(map.Children, Is.EqualTo(new SyntaxNode[] { map.Expression }));
            Assert.That(((OpenExpressionSyntax)grouped.Expression).Pipeline.Cast<FunctionCallSyntax>().Select(call => call.Name),
                Is.EqualTo(new[] { "absolute", "add" }));
            Assert.That(root.Pipeline[1],
                Is.TypeOf<FunctionCallSyntax>().With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("reverse"));
        });
    }

    [Test]
    public void ParenthesizedMapShorthandIsValidAtTheRoot()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("(|> absolute)");
        var grouped = (ParenthesizedExpressionSyntax)root.Pipeline.Single();
        var inner = (OpenExpressionSyntax)grouped.Expression;

        Assert.That(inner.Pipeline.Single(), Is.TypeOf<MapShorthandSyntax>());
    }

    [Test]
    public void MapShorthandAcceptsAParenthesizedOperation()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("|> (absolute)");
        var shorthand = (MapShorthandSyntax)root.Pipeline.Single();

        Assert.That(shorthand.Expression.Pipeline.Single(), Is.TypeOf<ParenthesizedExpressionSyntax>());
    }

    [TestCase("foo(5,)", "foo(5)")]
    [TestCase("record(name := \"Alice\",)", "record(name := \"Alice\")")]
    [TestCase("record(name := \"Alice\", age := 30,)", "record(name := \"Alice\", age := 30)")]
    public void FunctionCallsAcceptATrailingComma(string source, string equivalentSource)
    {
        var withComma = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse(source)).Pipeline.Single();
        var withoutComma = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse(equivalentSource)).Pipeline.Single();

        Assert.Multiple(() =>
        {
            Assert.That(withComma.Arguments.Select(argument => argument.Kind),
                Is.EqualTo(withoutComma.Arguments.Select(argument => argument.Kind)));
            Assert.That(withComma.Arguments.OfType<NamedArgumentSyntax>().Select(argument => argument.Name.Value),
                Is.EqualTo(withoutComma.Arguments.OfType<NamedArgumentSyntax>().Select(argument => argument.Name.Value)));
        });
    }

    [Test]
    public void RecordFieldShorthandCanContinueAPipeline()
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(".address | .city | .name");

        Assert.Multiple(() =>
        {
            Assert.That(((RecordAccessSyntax)root.Value).Fields.Single().Name, Is.EqualTo("address"));
            Assert.That(root.Pipeline.Cast<RecordAccessSyntax>()
                .Select(access => access.Fields.Single().Name), Is.EqualTo(new[] { "city", "name" }));
        });
    }

    [Test]
    public void RecordFieldShorthandAcceptsUnderscores()
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(".first_name._display_name");
        var access = (RecordAccessSyntax)root.Value;

        Assert.That(access.Fields.Select(field => field.Name),
            Is.EqualTo(new[] { "first_name", "_display_name" }));
    }

    [Test]
    public void RecordFieldShorthandRejectsOperators()
        => Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse(".+"));

    [Test]
    public void LeadingMapShorthandPreservesOuterPipelineBoundary()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("|> add(1) | sum");
        var shorthand = (MapShorthandSyntax)root.Pipeline[0];

        Assert.Multiple(() =>
        {
            Assert.That(shorthand.Expression.Pipeline.Cast<FunctionCallSyntax>().Select(call => call.Name),
                Is.EqualTo(new[] { "add" }));
            Assert.That(root.Pipeline[1],
                Is.TypeOf<FunctionCallSyntax>().With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("sum"));
        });
    }

    [Test]
    public void ParenthesizedMapShorthandCannotFollowAnOrdinaryPipe()
        => Assert.That(
            () => ExpressifSyntax.Parse("absolute | (|> absolute)"),
            Throws.TypeOf<ExpressifSyntaxException>());

    [Test]
    public void BinderSupportsEveryGrammarValueNodeType()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "node-types.json")));
        var value = document.RootElement.EnumerateArray().Single(node => node.GetProperty("type").GetString() == "value");
        var grammarTypes = value.GetProperty("subtypes").EnumerateArray()
            .Select(node => node.GetProperty("type").GetString()!)
            .ToHashSet();

        Assert.That(ExpressifSyntax.SupportedValueNodeTypes, Is.EquivalentTo(grammarTypes));
    }

    [Test]
    public void SourceTextAndRangesAreLossless()
    {
        const string source = "10 | add(5)";
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);

        Assert.Multiple(() =>
        {
            Assert.That(root.Text, Is.EqualTo(source));
            Assert.That(root.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
            Assert.That(root.Value.Span, Is.EqualTo(new SourceSpan(0, 2)));
            var call = (FunctionCallSyntax)root.Pipeline.Single();
            Assert.That(call.Span, Is.EqualTo(new SourceSpan(5, 6)));
            Assert.That(call.Arguments.Single().Span, Is.EqualTo(new SourceSpan(9, 1)));
        });
    }

    [TestCase("I[1, 10]", true, true)]
    [TestCase("I[1, 10)", true, false)]
    [TestCase("I[1, 10[", true, false)]
    [TestCase("I(1, 10]", false, true)]
    [TestCase("I]1, 10]", false, true)]
    [TestCase("I(1, 10)", false, false)]
    [TestCase("I]1, 10[", false, false)]
    public void IntervalDelimitersExposeNormalizedInclusivity(string source, bool lowerInclusive, bool upperInclusive)
    {
        var interval = (IntervalLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;

        Assert.Multiple(() =>
        {
            Assert.That(interval.LowerBound.Value, Is.TypeOf<NumericLiteralSyntax>()
                .With.Property(nameof(SyntaxNode.Text)).EqualTo("1"));
            Assert.That(interval.UpperBound.Value, Is.TypeOf<NumericLiteralSyntax>()
                .With.Property(nameof(SyntaxNode.Text)).EqualTo("10"));
            Assert.That(interval.IsLowerInclusive, Is.EqualTo(lowerInclusive));
            Assert.That(interval.IsUpperInclusive, Is.EqualTo(upperInclusive));
            Assert.That(interval.Children, Is.EqualTo(new[] { interval.LowerBound.Value, interval.UpperBound.Value }));
        });
    }

    [Test]
    public void InfiniteAndTemporalBoundsRetainTheirSemantics()
    {
        var infinite = (IntervalLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("I[-INF, +INF]")).Value;
        var temporal = (IntervalLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(
            "I[#\"2022-12-10\", #\"2022-12-31\"[")).Value;

        Assert.Multiple(() =>
        {
            Assert.That(infinite.LowerBound.Kind, Is.EqualTo(IntervalBoundKind.NegativeInfinity));
            Assert.That(infinite.UpperBound.Kind, Is.EqualTo(IntervalBoundKind.PositiveInfinity));
            Assert.That(infinite.Children, Is.Empty);
            Assert.That(temporal.LowerBound.Value, Is.TypeOf<DateLiteralSyntax>());
            Assert.That(temporal.UpperBound.Value, Is.TypeOf<DateLiteralSyntax>());
            Assert.That(temporal.IsUpperInclusive, Is.False);
        });
    }

    [TestCase("I(0+)", IntervalBoundKind.Finite, IntervalBoundKind.PositiveInfinity, true, true)]
    [TestCase("I(+)", IntervalBoundKind.Finite, IntervalBoundKind.PositiveInfinity, false, true)]
    [TestCase("I(0-)", IntervalBoundKind.NegativeInfinity, IntervalBoundKind.Finite, true, true)]
    [TestCase("I(-)", IntervalBoundKind.NegativeInfinity, IntervalBoundKind.Finite, true, false)]
    public void IntervalShorthandsMapToFirstClassSemantics(
        string source,
        IntervalBoundKind lowerKind,
        IntervalBoundKind upperKind,
        bool lowerInclusive,
        bool upperInclusive)
    {
        var interval = (IntervalLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;

        Assert.Multiple(() =>
        {
            Assert.That(interval.LowerBound.Kind, Is.EqualTo(lowerKind));
            Assert.That(interval.UpperBound.Kind, Is.EqualTo(upperKind));
            Assert.That(interval.LowerBound.Value?.Text ?? interval.UpperBound.Value?.Text, Is.EqualTo("0"));
            Assert.That(interval.IsLowerInclusive, Is.EqualTo(lowerInclusive));
            Assert.That(interval.IsUpperInclusive, Is.EqualTo(upperInclusive));
        });
    }

    [TestCase("I(>40)", IntervalBoundKind.Finite, IntervalBoundKind.PositiveInfinity, false, true, "40")]
    [TestCase("I(<40)", IntervalBoundKind.NegativeInfinity, IntervalBoundKind.Finite, true, false, "40")]
    [TestCase("I(>=40)", IntervalBoundKind.Finite, IntervalBoundKind.PositiveInfinity, true, true, "40")]
    [TestCase("I(<=40)", IntervalBoundKind.NegativeInfinity, IntervalBoundKind.Finite, true, true, "40")]
    [TestCase("I(positive)", IntervalBoundKind.Finite, IntervalBoundKind.PositiveInfinity, true, true, "0")]
    [TestCase("I(negative)", IntervalBoundKind.NegativeInfinity, IntervalBoundKind.Finite, true, true, "0")]
    [TestCase("I(absolutely-positive)", IntervalBoundKind.Finite, IntervalBoundKind.PositiveInfinity, false, true, "0")]
    [TestCase("I(absolutely-negative)", IntervalBoundKind.NegativeInfinity, IntervalBoundKind.Finite, true, false, "0")]
    public void ComparisonAndWordIntervalShorthandsMapToFirstClassSemantics(
        string source,
        IntervalBoundKind lowerKind,
        IntervalBoundKind upperKind,
        bool lowerInclusive,
        bool upperInclusive,
        string finiteText)
    {
        var interval = (IntervalLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;
        var finite = interval.LowerBound.Value ?? interval.UpperBound.Value;
        var authoredValueStart = source.IndexOf(finiteText, StringComparison.Ordinal);
        var expectedSpan = authoredValueStart >= 0
            ? new SourceSpan(authoredValueStart, finiteText.Length)
            : new SourceSpan(2, 0);

        Assert.Multiple(() =>
        {
            Assert.That(interval.Text, Is.EqualTo(source));
            Assert.That(interval.LowerBound.Kind, Is.EqualTo(lowerKind));
            Assert.That(interval.UpperBound.Kind, Is.EqualTo(upperKind));
            Assert.That(interval.IsLowerInclusive, Is.EqualTo(lowerInclusive));
            Assert.That(interval.IsUpperInclusive, Is.EqualTo(upperInclusive));
            Assert.That(finite, Is.TypeOf<NumericLiteralSyntax>()
                .With.Property(nameof(SyntaxNode.Text)).EqualTo(finiteText));
            Assert.That(finite!.Span, Is.EqualTo(expectedSpan));
            Assert.That(interval.Children, Is.EqualTo(new[] { finite }));
        });
    }

    [Test]
    public void BareDateLookingIntervalBoundsAreRejected()
        => Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse("I[2022-12-10, 2022-12-31]"));

    [Test]
    public void PairLiteralPreservesKeyValueChildrenAndSource()
    {
        const string source = "(\"BE\" => 42)";
        var pair = (PairLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;

        Assert.Multiple(() =>
        {
            Assert.That(pair.Kind, Is.EqualTo(SyntaxKind.PairLiteral));
            Assert.That(pair.Text, Is.EqualTo(source));
            Assert.That(pair.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
            Assert.That(pair.Key, Is.TypeOf<QuotedLiteralSyntax>()
                .With.Property(nameof(SyntaxNode.Text)).EqualTo("\"BE\""));
            Assert.That(pair.Value, Is.TypeOf<NumericLiteralSyntax>()
                .With.Property(nameof(SyntaxNode.Text)).EqualTo("42"));
            Assert.That(pair.Children, Is.EqualTo(new[] { pair.Key, pair.Value }));
        });
    }

    [Test]
    public void PairLiteralAcceptsExpressionOperandsAndNesting()
    {
        var accessPair = (PairLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("(.country => .amount)")).Value;
        var nestedPair = (PairLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("(\"outer\" => (\"inner\" => 42))")).Value;

        Assert.Multiple(() =>
        {
            Assert.That(accessPair.Key, Is.TypeOf<RecordAccessSyntax>());
            Assert.That(accessPair.Value, Is.TypeOf<RecordAccessSyntax>());
            Assert.That(nestedPair.Value, Is.TypeOf<PairLiteralSyntax>());
        });
    }

    [TestCase("$key", PairComponent.Key)]
    [TestCase("$value", PairComponent.Value)]
    public void PairComponentAccessorsAreDedicatedExpressionNodes(string source, PairComponent component)
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var access = (PairComponentAccessSyntax)root.Source!;

        Assert.Multiple(() =>
        {
            Assert.That(access.Kind, Is.EqualTo(SyntaxKind.PairComponentAccess));
            Assert.That(access.Component, Is.EqualTo(component));
            Assert.That(access.Text, Is.EqualTo(source));
            Assert.That(access.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
            Assert.That(access.Children, Is.Empty);
        });
    }

    [Test]
    public void PairSyntaxComposesInArraysArgumentsAndPipelines()
    {
        var array = (ArrayLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("{(\"BE\" => 42)}")).Value;
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse("consume((\"BE\" => 42))")).Pipeline.Single();
        var pipeline = (OpenExpressionSyntax)ExpressifSyntax.Parse("$key | trim");

        Assert.Multiple(() =>
        {
            Assert.That(array.Elements.Single().Expression, Is.TypeOf<PairLiteralSyntax>());
            Assert.That(call.Arguments.Single().Value, Is.TypeOf<PairLiteralSyntax>());
            Assert.That(pipeline.Source, Is.TypeOf<PairComponentAccessSyntax>());
            Assert.That(pipeline.Pipeline.Single(), Is.TypeOf<FunctionCallSyntax>());
        });
    }

    [TestCase("(=> 1)")]
    [TestCase("(1 =>)")]
    [TestCase("1 => 2")]
    [TestCase("(1 => 2 => 3)")]
    public void MalformedPairLiteralsAreRejected(string source)
        => Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse(source));

    [Test]
    public void EmptyGroupingLiteralIsADedicatedLosslessValue()
    {
        var grouping = (GroupingLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("#{}")).Value;

        Assert.Multiple(() =>
        {
            Assert.That(grouping.Kind, Is.EqualTo(SyntaxKind.GroupingLiteral));
            Assert.That(grouping.Text, Is.EqualTo("#{}"));
            Assert.That(grouping.Span, Is.EqualTo(new SourceSpan(0, 3)));
            Assert.That(grouping.Entries, Is.Empty);
            Assert.That(grouping.Children, Is.Empty);
        });
    }

    [Test]
    public void GroupingLiteralPreservesOrderedPairEntries()
    {
        const string source = "#{(\"BE\" => {\"Alice\", \"Bob\"}), (\"FR\" => {\"Charlie\"})}";
        var grouping = (GroupingLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;

        Assert.Multiple(() =>
        {
            Assert.That(grouping.Text, Is.EqualTo(source));
            Assert.That(grouping.Entries, Has.Count.EqualTo(2));
            Assert.That(grouping.Entries.Select(entry => entry.Key.Text), Is.EqualTo(new[] { "\"BE\"", "\"FR\"" }));
            Assert.That(grouping.Entries.Select(entry => entry.Value), Is.All.TypeOf<ArrayLiteralSyntax>());
            Assert.That(grouping.Children, Is.EqualTo(grouping.Entries));
        });
    }

    [Test]
    public void GroupingLiteralPreservesPairsForDownstreamContextualValidation()
    {
        var scalar = (GroupingLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("#{(\"BE\" => \"Alice\")}")).Value;
        var computed = (GroupingLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("#{(\"BE\" => @people)}")).Value;

        Assert.Multiple(() =>
        {
            Assert.That(scalar.Entries.Single().Value, Is.TypeOf<QuotedLiteralSyntax>());
            Assert.That(computed.Entries.Single().Value, Is.TypeOf<VariableSyntax>());
        });
    }

    [Test]
    public void GroupingLiteralComposesAsAFunctionArgument()
    {
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse("consume(#{(\"BE\" => {\"Alice\"})})")).Pipeline.Single();
        Assert.That(call.Arguments.Single().Value, Is.TypeOf<GroupingLiteralSyntax>());
    }

    [TestCase("# {(\"BE\" => {\"Alice\"})}")]
    [TestCase("#{1}")]
    [TestCase("#{(\"BE\" => {\"Alice\"})")]
    public void MalformedGroupingLiteralsAreRejected(string source)
        => Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse(source));

    [Test]
    public void EmptyDictionaryLiteralIsADedicatedLosslessValue()
    {
        var dictionary = (DictionaryLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("!{}")).Value;

        Assert.Multiple(() =>
        {
            Assert.That(dictionary.Kind, Is.EqualTo(SyntaxKind.DictionaryLiteral));
            Assert.That(dictionary.Text, Is.EqualTo("!{}"));
            Assert.That(dictionary.Span, Is.EqualTo(new SourceSpan(0, 3)));
            Assert.That(dictionary.Entries, Is.Empty);
            Assert.That(dictionary.Children, Is.Empty);
        });
    }

    [Test]
    public void DictionaryLiteralPreservesOrderedPairEntries()
    {
        const string source = "!{(\"BE\" => \"Belgium\"), (\"FR\" => \"France\")}";
        var dictionary = (DictionaryLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;

        Assert.Multiple(() =>
        {
            Assert.That(dictionary.Text, Is.EqualTo(source));
            Assert.That(dictionary.Entries, Has.Count.EqualTo(2));
            Assert.That(dictionary.Entries.Select(entry => entry.Key.Text), Is.EqualTo(new[] { "\"BE\"", "\"FR\"" }));
            Assert.That(dictionary.Entries.Select(entry => entry.Value.Text), Is.EqualTo(new[] { "\"Belgium\"", "\"France\"" }));
            Assert.That(dictionary.Children, Is.EqualTo(dictionary.Entries));
        });
    }

    [Test]
    public void DictionaryLiteralComposesAsAFunctionArgument()
    {
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse("consume(!{(\"BE\" => \"Belgium\")})")).Pipeline.Single();
        Assert.That(call.Arguments.Single().Value, Is.TypeOf<DictionaryLiteralSyntax>());
    }

    [Test]
    public void DictionaryLiteralPreservesDuplicateLookingEntriesForSemanticValidation()
    {
        var dictionary = (DictionaryLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("!{(\"BE\" => 1), (\"BE\" => 2)}")).Value;
        Assert.That(dictionary.Entries, Has.Count.EqualTo(2));
    }

    [TestCase("! {(\"BE\" => \"Belgium\")}")]
    [TestCase("!{1}")]
    [TestCase("!{(\"BE\" => \"Belgium\")")]
    public void MalformedDictionaryLiteralsAreRejected(string source)
        => Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse(source));

    [TestCase("add(", false)]
    [TestCase("10 |", false)]
    [TestCase("\"unterminated", false)]
    [TestCase("foo({| lower})", false)]
    [TestCase("append(.firstName |)", false)]
    [TestCase("foo(name :=)", true)]
    [TestCase("foo(, 5)", false)]
    [TestCase("foo(5,,6)", false)]
    [TestCase("!", false)]
    [TestCase("foo |AND", true)]
    [TestCase("|AND foo", false)]
    [TestCase("foo |BAD bar", false)]
    [TestCase("(foo |AND bar", false)]
    [TestCase("....", false)]
    [TestCase("T(..)", false)]
    [TestCase("T(....)", false)]
    [TestCase("{ ..., }", false)]
    [TestCase("@!", false)]
    [TestCase("@!1foo", false)]
    [TestCase("@! foo", false)]
    [TestCase("@!foo-", false)]
    [TestCase("@_foo", false)]
    [TestCase("@!_", false)]
    public void MalformedInputExposesTreeSitterErrors(string source, bool hasMissingError)
    {
        var exception = Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse(source));
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Errors, Is.Not.Empty);
            Assert.That(exception.Errors.Any(error => error.IsMissing), Is.EqualTo(hasMissingError));
        });
    }
}
