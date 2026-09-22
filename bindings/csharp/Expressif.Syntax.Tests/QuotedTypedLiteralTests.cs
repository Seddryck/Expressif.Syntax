namespace Expressif.Syntax.Tests;

public class QuotedTypedLiteralTests
{
    [TestCase("#\"BE\"", "BE", null)]
    [TestCase("#\"42\":integer", "42", "integer")]
    [TestCase("#\"say \\\"hello\\\"\":text", "say \"hello\"", "text")]
    public void QuotedTypedLiteralPreservesRepresentationAndOptionalType(
        string source,
        string expectedRepresentation,
        string? expectedType)
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        var literal = (QuotedTypedLiteralSyntax)root.Value;

        Assert.Multiple(() =>
        {
            Assert.That(literal.Kind, Is.EqualTo(SyntaxKind.QuotedTypedLiteral));
            Assert.That(literal.Text, Is.EqualTo(source));
            Assert.That(literal.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
            Assert.That(literal.Representation.Value, Is.EqualTo(expectedRepresentation));
            Assert.That(literal.Representation.QuotingStyle, Is.EqualTo(QuotingStyle.DoubleQuote));
            Assert.That(literal.Type?.Name, Is.EqualTo(expectedType));
            Assert.That(literal.Children, Is.EqualTo(
                literal.Type is null
                    ? new SyntaxNode[] { literal.Representation }
                    : new SyntaxNode[] { literal.Representation, literal.Type }));
        });
    }

    [Test]
    public void QuotedTypedLiteralCanBeUsedAsAnIntervalBound()
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse("I[#\"A\":text,#\"Z\":text]");
        var interval = (IntervalLiteralSyntax)root.Value;

        Assert.Multiple(() =>
        {
            Assert.That(interval.LowerBound.Value, Is.TypeOf<QuotedTypedLiteralSyntax>());
            Assert.That(interval.UpperBound.Value, Is.TypeOf<QuotedTypedLiteralSyntax>());
        });
    }

    [TestCase("#\"value\":1bad")]
    [TestCase("#\"value\":bad-")]
    [TestCase("#\"unterminated")]
    public void InvalidQuotedTypedLiteralsAreRejected(string source)
        => Assert.That(() => ExpressifSyntax.Parse(source), Throws.TypeOf<ExpressifSyntaxException>());

    [TestCase("#\"2025-12-17\"", typeof(DateLiteralSyntax))]
    [TestCase("#\"14:30:00\"", typeof(TimeLiteralSyntax))]
    public void ExistingTemporalFormsKeepTheirSpecializedNodes(string source, Type expectedType)
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        Assert.That(root.Value, Is.TypeOf(expectedType));
    }
}
