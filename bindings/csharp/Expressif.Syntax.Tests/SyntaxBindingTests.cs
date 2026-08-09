namespace Expressif.Syntax.Tests;

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

    [TestCase("add(")]
    [TestCase("10 |")]
    [TestCase("\"unterminated")]
    public void MalformedInputExposesTreeSitterErrors(string source)
    {
        var exception = Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse(source));
        Assert.That(exception!.Errors, Is.Not.Empty);
    }
}
