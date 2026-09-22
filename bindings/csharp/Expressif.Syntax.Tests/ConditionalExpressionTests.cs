namespace Expressif.Syntax.Tests;

public class ConditionalExpressionTests
{
    [TestCase("#false ?> divide(0)", ConditionalDirection.Forward, "?>")]
    [TestCase("absolute <? greater-than(10)", ConditionalDirection.Backward, "<?")]
    public void ConditionalExpressionPreservesAuthoredSyntax(
        string source,
        ConditionalDirection direction,
        string operatorText)
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var conditional = (ConditionalExpressionSyntax)root.Pipeline.Single();

        Assert.Multiple(() =>
        {
            Assert.That(conditional.Kind, Is.EqualTo(SyntaxKind.ConditionalExpression));
            Assert.That(conditional.Text, Is.EqualTo(source));
            Assert.That(conditional.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
            Assert.That(conditional.Operator.Kind, Is.EqualTo(SyntaxKind.ConditionalOperator));
            Assert.That(conditional.Operator.Text, Is.EqualTo(operatorText));
            Assert.That(conditional.Operator.Direction, Is.EqualTo(direction));
            Assert.That(conditional.Children, Is.EqualTo(new SyntaxNode[]
            {
                conditional.Left,
                conditional.Operator,
                conditional.Right,
            }));
        });
    }

    [Test]
    public void ConditionalExpressionDoesNotConsumeFollowingPipelineStages()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(
            "absolute | add(5) <? greater-than(20) | finish");

        Assert.Multiple(() =>
        {
            Assert.That(root.Pipeline, Has.Count.EqualTo(3));
            Assert.That(root.Pipeline[0], Is.TypeOf<FunctionCallSyntax>());
            Assert.That(root.Pipeline[1], Is.TypeOf<ConditionalExpressionSyntax>());
            Assert.That(root.Pipeline[2], Is.TypeOf<FunctionCallSyntax>());
        });
    }

    [TestCase("absolute ?>")]
    [TestCase("?> absolute")]
    [TestCase("absolute ?> identity <? finish")]
    public void MalformedOrUngroupedConditionalExpressionsAreRejected(string source)
        => Assert.That(() => ExpressifSyntax.Parse(source), Throws.TypeOf<ExpressifSyntaxException>());
}
