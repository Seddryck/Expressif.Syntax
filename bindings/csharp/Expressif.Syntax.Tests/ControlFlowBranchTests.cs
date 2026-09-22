namespace Expressif.Syntax.Tests;

public class ControlFlowBranchTests
{
    [Test]
    public void SwitchExposesConditionalAndFallbackBranches()
    {
        const string source = "switch(is-positive => 1, _ => 0)";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var call = (ControlFlowCallSyntax)root.Pipeline.Single();
        var branch = (ConditionalControlFlowBranchSyntax)call.Branches[0];
        var fallback = (ControlFlowFallbackSyntax)call.Branches[1];

        Assert.Multiple(() =>
        {
            Assert.That(call.Kind, Is.EqualTo(SyntaxKind.ControlFlowCall));
            Assert.That(call.Name, Is.EqualTo("switch"));
            Assert.That(call.Text, Is.EqualTo(source));
            Assert.That(call.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
            Assert.That(branch.Text, Is.EqualTo("is-positive => 1"));
            Assert.That(branch.Condition.Text, Is.EqualTo("is-positive"));
            Assert.That(branch.Action.Text, Is.EqualTo("1"));
            Assert.That(branch.Children, Is.EqualTo(new[] { branch.Condition, branch.Action }));
            Assert.That(fallback.Text, Is.EqualTo("_ => 0"));
            Assert.That(fallback.Action.Text, Is.EqualTo("0"));
            Assert.That(fallback.Children, Is.EqualTo(new[] { fallback.Action }));
            Assert.That(call.Children, Is.EqualTo(call.Branches));
        });
    }

    [Test]
    public void ControlFlowCallsSupportNestingAndPipelines()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(
            "TRY(value | absolute => switch(#true => 1, _ => 0), _ => #\"none\":text)");
        var outer = (ControlFlowCallSyntax)root.Pipeline.Single();
        var first = (ConditionalControlFlowBranchSyntax)outer.Branches[0];

        Assert.Multiple(() =>
        {
            Assert.That(outer.Name, Is.EqualTo("TRY"));
            Assert.That(first.Condition, Is.TypeOf<OpenExpressionSyntax>());
            Assert.That(first.Action, Is.TypeOf<OpenExpressionSyntax>());
            Assert.That(((OpenExpressionSyntax)first.Action).Pipeline.Single(), Is.TypeOf<ControlFlowCallSyntax>());
            Assert.That(outer.Branches[1].Action, Is.TypeOf<QuotedTypedLiteralSyntax>());
        });
    }

    [Test]
    public void OrdinaryCallsRetainOrdinaryArguments()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("choose(1, 2)");
        Assert.That(root.Pipeline.Single(), Is.TypeOf<FunctionCallSyntax>());
    }

    [TestCase("switch()")]
    [TestCase("try(is-positive =>)")]
    [TestCase("switch(=> 1)")]
    [TestCase("switch(is-positive, _ => 0)")]
    public void MalformedControlFlowBranchesAreRejected(string source)
        => Assert.That(() => ExpressifSyntax.Parse(source), Throws.TypeOf<ExpressifSyntaxException>());
}
