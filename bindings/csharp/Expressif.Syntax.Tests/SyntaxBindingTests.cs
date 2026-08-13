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
            Assert.That(root.Pipeline.Select(x => x.Name), Is.EqualTo(new[] { "LOWER", "text-to-lower", "unknown" }));
            Assert.That(root.Pipeline.Select(x => x.HasParentheses), Is.EqualTo(new[] { false, true, true }));
            Assert.That(root.Pipeline[2].Arguments.Select(x => x.Value.Text), Is.EqualTo(new[] { "5", "10" }));
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
        Assert.That(root.Pipeline.Single().Name, Is.EqualTo(source));
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
            Assert.That(argumentRoot.Pipeline.Single().Arguments.Single().Value,
                Is.TypeOf<TupleProjectionSyntax>());
            Assert.That(pipelineRoot.Source, Is.TypeOf<TupleProjectionSyntax>());
            Assert.That(pipelineRoot.Pipeline.Single().Name, Is.EqualTo("upper"));
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
    public void ParameterizedExpressionsPreserveSourceAndPipeline()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("skip-last-chars({@length | subtract(1) | max(0)})");
        var argument = root.Pipeline.Single().Arguments.Single();
        var parameterized = (ParameterizedExpressionSyntax)argument.Value;

        Assert.Multiple(() =>
        {
            Assert.That(parameterized.Source, Is.TypeOf<VariableSyntax>());
            Assert.That(((VariableSyntax)parameterized.Source).Name, Is.EqualTo("length"));
            Assert.That(parameterized.Expression.Pipeline.Select(call => call.Name), Is.EqualTo(new[] { "subtract", "max" }));
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
        var parameterized = (ParameterizedExpressionSyntax)root.Pipeline.Single().Arguments.Single().Value;

        Assert.Multiple(() =>
        {
            Assert.That(parameterized.Source, Is.TypeOf(sourceType));
            Assert.That(parameterized.Source.Text, Is.EqualTo(sourceText));
            Assert.That(parameterized.Text, Is.EqualTo(argument));
            Assert.That(parameterized.Expression.Pipeline, Has.Count.EqualTo(1));
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
            Assert.That(root.Pipeline.Single().Span, Is.EqualTo(new SourceSpan(5, 6)));
            Assert.That(root.Pipeline.Single().Arguments.Single().Span, Is.EqualTo(new SourceSpan(9, 1)));
        });
    }

    [TestCase("add(", false)]
    [TestCase("10 |", false)]
    [TestCase("\"unterminated", false)]
    [TestCase("foo({| lower})", true)]
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
