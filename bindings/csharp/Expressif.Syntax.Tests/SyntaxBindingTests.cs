namespace Expressif.Syntax.Tests;

using System.Text.Json;

public class SyntaxBindingTests
{
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
    [TestCase("{}", typeof(ArrayLiteralSyntax))]
    [TestCase("T(1, 2)", typeof(TupleLiteralSyntax))]
    [TestCase("{:}", typeof(RecordLiteralSyntax))]
    public void RemainingGrammarValuesHaveManagedSyntaxNodes(string source, Type expected)
        => Assert.That(((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value, Is.TypeOf(expected));

    [Test]
    public void CompoundValuesBindNestedValuesAndRecordFields()
    {
        var record = (RecordLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(
            "{name := @value, `scores` := {1, ^.total}}")).Value;

        Assert.Multiple(() =>
        {
            Assert.That(record.Fields.Select(field => field.Name), Is.EqualTo(new[] { "name", "scores" }));
            Assert.That(record.Fields[1].QuotingStyle, Is.EqualTo(QuotingStyle.Backtick));
            Assert.That(record.Fields[0].Value, Is.TypeOf<VariableSyntax>());
            Assert.That(((ArrayLiteralSyntax)record.Fields[1].Value).Values[1], Is.TypeOf<RecordAccessSyntax>());
            Assert.That(record.Children, Is.EqualTo(record.Fields));
        });
    }

    [Test]
    public void IncomingValueIsALosslessValueNode()
    {
        var incoming = (IncomingValueSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("...")).Value;

        Assert.Multiple(() =>
        {
            Assert.That(incoming.Kind, Is.EqualTo(SyntaxKind.IncomingValue));
            Assert.That(incoming.Text, Is.EqualTo("..."));
            Assert.That(incoming.Span, Is.EqualTo(new SourceSpan(0, 3)));
            Assert.That(incoming.Children, Is.Empty);
        });
    }

    [Test]
    public void IncomingValueCanBeEmbeddedInARecordField()
    {
        var record = (RecordLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("{ original := ... }")).Value;
        var field = record.Fields.Single();

        Assert.Multiple(() =>
        {
            Assert.That(field.Value, Is.TypeOf<IncomingValueSyntax>());
            Assert.That(field.Value.Kind, Is.EqualTo(SyntaxKind.IncomingValue));
            Assert.That(field.Value.Text, Is.EqualTo("..."));
            Assert.That(field.Value.Span, Is.EqualTo(new SourceSpan(14, 3)));
            Assert.That(field.Children, Is.EqualTo(new SyntaxNode[] { field.Value }));
        });
    }

    [Test]
    public void IncomingValueComposesAsAnArgumentAndPipelineSource()
    {
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse("foo(...)")).Pipeline.Single();
        var pipeline = (ClosedExpressionSyntax)ExpressifSyntax.Parse("... | upper");

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
            Assert.That(record.Fields.Select(field => field.Name), Is.EqualTo(new[] { "before", "after" }));
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
                new SourceSpan(2, 10), "value := 1", "value", null,
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
    public void IncomingValueCanAppearInAnArrayWhenAnotherValueDisambiguatesIt()
    {
        var array = (ArrayLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("{..., #true}")).Value;

        Assert.That(array.Values.Select(value => value.Kind),
            Is.EqualTo(new[] { SyntaxKind.IncomingValue, SyntaxKind.BooleanLiteral }));
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
            Assert.That(argument.Name, Is.EqualTo("customer-name"));
            Assert.That(argument.Value, Is.TypeOf<RecordAccessSyntax>());
            Assert.That(argument.Children, Is.EqualTo(new SyntaxNode[] { argument.Value }));
            Assert.That(argument.Text, Is.EqualTo("customer-name := .name"));
            Assert.That(argument.Span, Is.EqualTo(new SourceSpan(7, 22)));
            Assert.That(call.Text, Is.EqualTo(source));
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
            Assert.That(withComma.Arguments.OfType<NamedArgumentSyntax>().Select(argument => argument.Name),
                Is.EqualTo(withoutComma.Arguments.OfType<NamedArgumentSyntax>().Select(argument => argument.Name)));
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

    [Test]
    public void BareDateLookingIntervalBoundsAreRejected()
        => Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse("I[2022-12-10, 2022-12-31]"));

    [TestCase("add(", false)]
    [TestCase("10 |", false)]
    [TestCase("\"unterminated", false)]
    [TestCase("foo({| lower})", false)]
    [TestCase("append(.firstName |)", false)]
    [TestCase("foo(name :=)", false)]
    [TestCase("foo(, 5)", false)]
    [TestCase("foo(5,,6)", false)]
    [TestCase("....", false)]
    [TestCase("{ ..., }", false)]
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
