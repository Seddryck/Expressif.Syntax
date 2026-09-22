namespace Expressif.Syntax.Tests;

public class ValueReferenceStageTests
{
    [Test]
    public void PipelineValueReferenceHasDedicatedAuthoredNode()
    {
        const string source = "source | @transform | finish";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var stage = (ValueReferenceStageSyntax)root.Pipeline[1];

        Assert.Multiple(() =>
        {
            Assert.That(stage.Kind, Is.EqualTo(SyntaxKind.ValueReferenceStage));
            Assert.That(stage.Text, Is.EqualTo("@transform"));
            Assert.That(stage.Span, Is.EqualTo(new SourceSpan(9, 10)));
            Assert.That(stage.Reference.Name, Is.EqualTo("transform"));
            Assert.That(stage.Reference.Text, Is.EqualTo("@transform"));
            Assert.That(stage.Reference.Span, Is.EqualTo(stage.Span));
            Assert.That(stage.Children, Is.EqualTo(new SyntaxNode[] { stage.Reference }));
            Assert.That(root.Pipeline[2], Is.TypeOf<FunctionCallSyntax>());
        });
    }

    [Test]
    public void StandaloneValueReferenceRemainsAVariableValue()
        => Assert.That(((ClosedExpressionSyntax)ExpressifSyntax.Parse("@transform")).Value,
            Is.TypeOf<VariableSyntax>());

    [TestCase("source | @")]
    [TestCase("source | @1")]
    public void MalformedValueReferenceStagesAreRejected(string source)
        => Assert.That(() => ExpressifSyntax.Parse(source), Throws.TypeOf<ExpressifSyntaxException>());
}
