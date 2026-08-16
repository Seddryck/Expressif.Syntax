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
    public void ParsePreservesRootExpressionKind(string source, Type expected)
        => Assert.That(ExpressifSyntax.Parse(source), Is.TypeOf(expected));

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

    [TestCase("0")]
    [TestCase("42")]
    [TestCase("-5")]
    [TestCase("3.14")]
    [TestCase("-3.14")]
    public void NumericLiteralsPreserveLexicalText(string source)
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        Assert.That(root.Value, Is.TypeOf<NumericLiteralSyntax>().With.Property(nameof(SyntaxNode.Text)).EqualTo(source));
    }

    [TestCase("#true", true)]
    [TestCase("#false", false)]
    public void BooleanLiteralsExposeTypedValue(string source, bool expected)
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        Assert.That(((BooleanLiteralSyntax)root.Value).Value, Is.EqualTo(expected));
    }

    [TestCase("true")]
    [TestCase("false")]
    public void BareBooleanWordsRemainFunctionCalls(string source)
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        Assert.That(((FunctionCallSyntax)root.Pipeline.Single()).Name, Is.EqualTo(source));
    }

    [TestCase("\"foo\"", QuotingStyle.DoubleQuote)]
    [TestCase("\"\"", QuotingStyle.DoubleQuote)]
    [TestCase("\"Alice said \\\"hello\\\".\"", QuotingStyle.DoubleQuote)]
    [TestCase("`foo`", QuotingStyle.Backtick)]
    [TestCase("` foo bar `", QuotingStyle.Backtick)]
    public void QuotedLiteralsPreserveStyleAndEscapedSource(string source, QuotingStyle style)
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        var literal = (QuotedLiteralSyntax)root.Value;
        Assert.Multiple(() =>
        {
            Assert.That(literal.Text, Is.EqualTo(source));
            Assert.That(literal.QuotingStyle, Is.EqualTo(style));
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

    [TestCase("add(", false)]
    [TestCase("10 |", false)]
    [TestCase("\"unterminated", false)]
    [TestCase("foo({| lower})", false)]
    [TestCase("append(.firstName |)", false)]
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
