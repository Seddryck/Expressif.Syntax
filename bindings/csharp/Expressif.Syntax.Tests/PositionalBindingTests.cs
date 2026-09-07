namespace Expressif.Syntax.Tests;

public class PositionalBindingTests
{
    private static FunctionCallSyntax Call(string source)
        => (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse(source)).Pipeline.Single();

    [Test]
    public void PositionalBindingPreservesNamesBodyAndArgumentBoundaries()
    {
        const string source = "choose((previous, current) :> @current | subtract(@previous) | multiply(@current), :> #null)";
        var call = Call(source);
        var expression = (InputBoundExpressionSyntax)call.Arguments[0].Value!;
        var pattern = (PositionalBindingPatternSyntax)expression.Binding!;
        var body = (ClosedExpressionSyntax)expression.Body;
        Assert.Multiple(() =>
        {
            Assert.That(call.Arguments, Has.Count.EqualTo(2));
            Assert.That(expression.Kind, Is.EqualTo(SyntaxKind.InputBoundExpression));
            Assert.That(pattern.Kind, Is.EqualTo(SyntaxKind.PositionalBindingPattern));
            Assert.That(pattern.Names.Select(n => n.Name), Is.EqualTo(new[] { "previous", "current" }));
            Assert.That(pattern.Names.All(n => n.Kind == SyntaxKind.BindingName), Is.True);
            Assert.That(pattern.Text, Is.EqualTo("(previous, current)"));
            Assert.That(pattern.Span, Is.EqualTo(new SourceSpan(7, pattern.Text.Length)));
            Assert.That(pattern.Names[0].Span, Is.EqualTo(new SourceSpan(8, 8)));
            Assert.That(pattern.Names[1].Span, Is.EqualTo(new SourceSpan(18, 7)));
            Assert.That(pattern.Children, Is.EqualTo(pattern.Names));
            Assert.That(expression.Children, Is.EqualTo(new SyntaxNode[] { pattern, body }));
            Assert.That(body.Value, Is.TypeOf<VariableSyntax>());
            Assert.That(body.Pipeline, Has.Count.EqualTo(2));
            Assert.That(body.Text, Is.EqualTo("@current | subtract(@previous) | multiply(@current)"));
            Assert.That(expression.Text, Is.EqualTo(source[7..source.IndexOf(", :>", StringComparison.Ordinal)]));
            Assert.That(expression.Span, Is.EqualTo(new SourceSpan(7, expression.Text.Length)));
            Assert.That(call.Arguments[1].Value!.Text, Is.EqualTo(":> #null"));
        });
    }

    [Test]
    public void PositionalPatternCanBeNestedAndUsedInNamedArgument()
    {
        var call = Call("apply(transform := (a1,b2):> apply((x,y) :> @x) | apply(c :> @c))");
        var outer = (InputBoundExpressionSyntax)((NamedArgumentSyntax)call.Arguments[0]).Value;
        var body = (OpenExpressionSyntax)outer.Body;
        var inner = (InputBoundExpressionSyntax)((FunctionCallSyntax)body.Pipeline[0]).Arguments[0].Value!;
        var named = (InputBoundExpressionSyntax)((FunctionCallSyntax)body.Pipeline[1]).Arguments[0].Value!;
        Assert.Multiple(() =>
        {
            Assert.That(((PositionalBindingPatternSyntax)outer.Binding!).Names.Select(n => n.Name), Is.EqualTo(new[] { "a1", "b2" }));
            Assert.That(((PositionalBindingPatternSyntax)inner.Binding!).Names.Select(n => n.Name), Is.EqualTo(new[] { "x", "y" }));
            Assert.That(named.Binding, Is.TypeOf<BindingNameSyntax>());
            Assert.That(inner.Body.Text, Is.EqualTo("@x"));
            Assert.That(named.Body.Text, Is.EqualTo("@c"));
        });
    }

    [Test]
    public void DuplicateNamesArePreservedForSemanticDiagnostics()
    {
        var expression = (InputBoundExpressionSyntax)Call("apply((a, a, a) :> @a)").Arguments[0].Value!;
        var pattern = (PositionalBindingPatternSyntax)expression.Binding!;
        Assert.Multiple(() =>
        {
            Assert.That(pattern.Names.Select(n => n.Name), Is.EqualTo(new[] { "a", "a", "a" }));
            Assert.That(pattern.Names.Select(n => n.Span.Start), Is.EqualTo(new[] { 7, 10, 13 }));
        });
    }

    [Test]
    public void PatternCommentsAndMultilineSpansAreLossless()
    {
        const string source = "apply((\n a, /* 🌍 */ b\n) :>\n @b | trim)";
        var document = ExpressifSyntax.ParseDocument(source);
        var expression = (InputBoundExpressionSyntax)Call(source).Arguments[0].Value!;
        var pattern = (PositionalBindingPatternSyntax)expression.Binding!;
        Assert.Multiple(() =>
        {
            Assert.That(document.Comments.Single().Text, Is.EqualTo("/* 🌍 */"));
            Assert.That(pattern.Text, Is.EqualTo("(\n a, /* 🌍 */ b\n)"));
            Assert.That(pattern.Span, Is.EqualTo(new SourceSpan(6, pattern.Text.Length)));
            Assert.That(pattern.Children.Select(n => n.Text), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(pattern.Names[1].Span.Start, Is.EqualTo(source.IndexOf("b\n", StringComparison.Ordinal)));
            Assert.That(expression.Body.Span, Is.EqualTo(new SourceSpan(source.IndexOf('@'), "@b | trim".Length)));
        });
    }

    [Test]
    public void PatternIsDistinctFromGroupingAndPairLiterals()
    {
        var call = Call("choose((a, b) :> (@a | trim), (\"key\" => 2), (trim), T(1, 2))");
        var binding = (InputBoundExpressionSyntax)call.Arguments[0].Value!;
        Assert.Multiple(() =>
        {
            Assert.That(binding.Binding, Is.TypeOf<PositionalBindingPatternSyntax>());
            Assert.That(((OpenExpressionSyntax)binding.Body).Pipeline.Single(), Is.TypeOf<ParenthesizedExpressionSyntax>());
            Assert.That(call.Arguments[1].Value, Is.TypeOf<PairLiteralSyntax>());
            Assert.That(call.Arguments[2].Value, Is.TypeOf<ParenthesizedExpressionSyntax>());
            Assert.That(call.Arguments[3].Value, Is.TypeOf<TupleLiteralSyntax>());
        });
    }

    [TestCase("apply(() :> trim)")]
    [TestCase("apply((a) :> trim)")]
    [TestCase("apply((a,) :> trim)")]
    [TestCase("apply((a, b,) :> trim)")]
    [TestCase("apply((a,,b) :> trim)")]
    [TestCase("apply((a, b) :>)")]
    [TestCase("apply((a, b :> trim)")]
    [TestCase("apply((a, 1b) :> trim)")]
    [TestCase("apply((a, first-name) :> trim)")]
    [TestCase("apply((a, (b, c)) :> trim)")]
    [TestCase("apply((a, ...b) :> trim)")]
    public void MalformedPositionalPatternsAreRejected(string source)
        => Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse(source));
}
