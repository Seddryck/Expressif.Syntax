namespace Expressif.Syntax.Tests;

public class GroupingMapShorthandTests
{
    [Test]
    public void GroupingMapPreservesAuthoredSyntaxAndMappedExpression()
    {
        const string source = "groups |#> (sum | add(1)) | reverse";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var shorthand = (GroupingMapShorthandSyntax)root.Pipeline[1];

        Assert.Multiple(() =>
        {
            Assert.That(shorthand.Kind, Is.EqualTo(SyntaxKind.GroupingMapShorthand));
            Assert.That(shorthand.Text, Is.EqualTo("|#> (sum | add(1))"));
            Assert.That(shorthand.Span, Is.EqualTo(new SourceSpan(7, 18)));
            Assert.That(shorthand.Children, Is.EqualTo(new SyntaxNode[] { shorthand.Expression }));
            Assert.That(shorthand.Expression.Text, Is.EqualTo("(sum | add(1))"));
            Assert.That(shorthand.Expression.Pipeline.Single(), Is.TypeOf<ParenthesizedExpressionSyntax>());
            Assert.That(root.Pipeline[2], Is.TypeOf<FunctionCallSyntax>()
                .With.Property(nameof(FunctionCallSyntax.Name)).EqualTo("reverse"));
        });
    }

    [Test]
    public void GroupingMapCanLeadANestedExpression()
    {
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse("apply(|#> .score | sum)")).Pipeline.Single();
        var nested = (OpenExpressionSyntax)call.Arguments.Single().Value;

        Assert.Multiple(() =>
        {
            Assert.That(nested.Pipeline[0], Is.TypeOf<GroupingMapShorthandSyntax>());
            Assert.That(nested.Pipeline[1], Is.TypeOf<FunctionCallSyntax>());
        });
    }

    [TestCase("| #> sum")]
    [TestCase("|# > sum")]
    [TestCase("|#>")]
    public void MalformedGroupingMapIsRejected(string source)
        => Assert.That(() => ExpressifSyntax.Parse(source), Throws.TypeOf<ExpressifSyntaxException>());
}
