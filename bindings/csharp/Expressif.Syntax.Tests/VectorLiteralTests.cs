namespace Expressif.Syntax.Tests;

public class VectorLiteralTests
{
    [Test]
    public void EmptyVectorPreservesAuthoredSyntax()
    {
        const string source = "V()";
        var vector = (VectorLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;

        Assert.Multiple(() =>
        {
            Assert.That(vector.Kind, Is.EqualTo(SyntaxKind.VectorLiteral));
            Assert.That(vector.Text, Is.EqualTo(source));
            Assert.That(vector.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
            Assert.That(vector.Elements, Is.Empty);
            Assert.That(vector.Children, Is.Empty);
        });
    }

    [Test]
    public void VectorElementsPreserveOrderSpreadAndSpans()
    {
        const string source = "V(1, ...V(2, 3), ...)";
        var vector = (VectorLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;

        Assert.Multiple(() =>
        {
            Assert.That(vector.Children, Is.EqualTo(vector.Elements));
            Assert.That(vector.Elements.Select(element => element.Kind), Is.All.EqualTo(SyntaxKind.VectorElement));
            Assert.That(vector.Elements.Select(element => element.IsSpread), Is.EqualTo(new[] { false, true, true }));
            Assert.That(vector.Elements.Select(element => element.IsImplicitSpread), Is.EqualTo(new[] { false, false, true }));
            Assert.That(vector.Elements[0].Expression, Is.TypeOf<NumericLiteralSyntax>());
            Assert.That(vector.Elements[1].Expression, Is.TypeOf<VectorLiteralSyntax>());
            Assert.That(vector.Elements[1].Text, Is.EqualTo("...V(2, 3)"));
            Assert.That(vector.Elements[1].Span, Is.EqualTo(new SourceSpan(5, 10)));
            Assert.That(vector.Elements[2].Expression, Is.Null);
        });
    }

    [TestCase("V(")]
    [TestCase("V(1,)")]
    [TestCase("V(1,,2)")]
    public void MalformedVectorsAreRejected(string source)
        => Assert.That(() => ExpressifSyntax.Parse(source), Throws.TypeOf<ExpressifSyntaxException>());
}
