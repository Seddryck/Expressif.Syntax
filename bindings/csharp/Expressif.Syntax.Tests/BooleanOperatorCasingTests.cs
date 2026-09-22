namespace Expressif.Syntax.Tests;

public class BooleanOperatorCasingTests
{
    [TestCase("|and")]
    [TestCase("|OR")]
    [TestCase("|xOr")]
    [TestCase("|Nand")]
    [TestCase("|nor")]
    [TestCase("|NxOR")]
    public void BooleanOperatorsPreserveAuthoredTextAndSpan(string operatorText)
    {
        var source = $"left {operatorText} right";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var binary = (BinaryExpressionSyntax)root.Pipeline.Single();

        Assert.Multiple(() =>
        {
            Assert.That(binary.Operator.Text, Is.EqualTo(operatorText));
            Assert.That(binary.Operator.Span, Is.EqualTo(new SourceSpan(5, operatorText.Length)));
            Assert.That(binary.Text, Is.EqualTo(source));
        });
    }

    [TestCase("left |android right")]
    [TestCase("left |or2 right")]
    [TestCase("left |xor_name right")]
    [TestCase("left |nand-more right")]
    public void LongerNamesAreNotPartiallyTokenized(string source)
        => Assert.That(() => ExpressifSyntax.Parse(source), Throws.TypeOf<ExpressifSyntaxException>());
}
