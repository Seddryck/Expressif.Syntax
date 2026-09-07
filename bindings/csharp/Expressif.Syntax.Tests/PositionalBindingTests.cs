namespace Expressif.Syntax.Tests;

public class PositionalBindingTests
{
    [Test]
    public void PositionalPipelineBindingPreservesPrecedingStagesPatternAndGroupedBody()
    {
        const string source = "Tuple(10, 20) | extend(30) | (current, minus, factor) :> (@current | subtract(@minus) | multiply(@factor))";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var expression = (InputBindingExpressionSyntax)root.Pipeline[2];
        var pattern = (PositionalBindingPatternSyntax)expression.Binding!;
        var body = (ClosedExpressionSyntax)InputBindingTests.GroupedBody(expression);
        Assert.Multiple(() =>
        {
            Assert.That(root.Pipeline, Has.Count.EqualTo(3));
            Assert.That(root.Pipeline[0].Text, Is.EqualTo("Tuple(10, 20)"));
            Assert.That(root.Pipeline[1].Text, Is.EqualTo("extend(30)"));
            Assert.That(pattern.Kind, Is.EqualTo(SyntaxKind.PositionalBindingPattern));
            Assert.That(pattern.Names.Select(n => n.Name), Is.EqualTo(new[] { "current", "minus", "factor" }));
            Assert.That(pattern.Text, Is.EqualTo("(current, minus, factor)"));
            Assert.That(pattern.Span, Is.EqualTo(new SourceSpan(source.IndexOf("(current", StringComparison.Ordinal), pattern.Text.Length)));
            Assert.That(pattern.Children, Is.EqualTo(pattern.Names));
            Assert.That(expression.Children, Is.EqualTo(new SyntaxNode[] { pattern, expression.Body }));
            Assert.That(body.Value, Is.TypeOf<VariableSyntax>());
            Assert.That(body.Value.Text, Is.EqualTo("@current"));
            Assert.That(body.Pipeline, Has.Count.EqualTo(2));
            Assert.That(expression.Span.End, Is.EqualTo(source.Length));
        });
    }

    [Test]
    public void NamesAndBodyDoNotConsumeSiblingArguments()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("choose(T(1, 2) | (a, b) :> @a | add(@b), 42)");
        var call = (FunctionCallSyntax)root.Pipeline.Single();
        var pipeline = (ClosedExpressionSyntax)call.Arguments[0].Value!;
        var binding = (InputBindingExpressionSyntax)pipeline.Pipeline.Single();
        Assert.Multiple(() =>
        {
            Assert.That(call.Arguments, Has.Count.EqualTo(2));
            Assert.That(binding.Body.Text, Is.EqualTo("@a | add(@b)"));
            Assert.That(call.Arguments[1].Value!.Text, Is.EqualTo("42"));
            Assert.That(((PositionalBindingPatternSyntax)binding.Binding!).Names, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void GroupedPositionalBindingPermitsOuterContinuation()
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse("T(1, 2) | (a, b) :> (@a | add(@b)) | round");
        var binding = (InputBindingExpressionSyntax)root.Pipeline[0];
        Assert.Multiple(() =>
        {
            Assert.That(root.Pipeline, Has.Count.EqualTo(2));
            Assert.That(InputBindingTests.GroupedBody(binding).Text, Is.EqualTo("@a | add(@b)"));
            Assert.That(root.Pipeline[1].Text, Is.EqualTo("round"));
        });
    }

    [Test]
    public void NamedArgumentAndNestedBindingsReusePatterns()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("apply(transform := T(1, 2) | (a1,b2):> apply(T(3, 4) | (x,y) :> @x) | apply(@a1 | c :> @c))");
        var call = (FunctionCallSyntax)root.Pipeline.Single();
        var outer = (InputBindingExpressionSyntax)((ClosedExpressionSyntax)((NamedArgumentSyntax)call.Arguments[0]).Value).Pipeline.Single();
        var body = (OpenExpressionSyntax)outer.Body;
        var inner = (InputBindingExpressionSyntax)((ClosedExpressionSyntax)((FunctionCallSyntax)body.Pipeline[0]).Arguments[0].Value!).Pipeline.Single();
        var named = (InputBindingExpressionSyntax)((ClosedExpressionSyntax)((FunctionCallSyntax)body.Pipeline[1]).Arguments[0].Value!).Pipeline.Single();
        Assert.Multiple(() =>
        {
            Assert.That(((PositionalBindingPatternSyntax)outer.Binding!).Names.Select(n => n.Name), Is.EqualTo(new[] { "a1", "b2" }));
            Assert.That(((PositionalBindingPatternSyntax)inner.Binding!).Names.Select(n => n.Name), Is.EqualTo(new[] { "x", "y" }));
            Assert.That(named.Binding, Is.TypeOf<BindingNameSyntax>());
        });
    }

    [Test]
    public void DuplicateNamesAndMultilineCommentsRetainIndividualSpans()
    {
        const string source = "T(1, 2) | (\n a, /* 🌍 */ a\n) :> @a";
        var document = ExpressifSyntax.ParseDocument(source);
        var binding = InputBindingTests.Binding(source);
        var pattern = (PositionalBindingPatternSyntax)binding.Binding!;
        var first = source.IndexOf('a');
        var second = source.IndexOf('a', first + 1);
        Assert.Multiple(() =>
        {
            Assert.That(document.Comments.Single().Text, Is.EqualTo("/* 🌍 */"));
            Assert.That(pattern.Names.Select(n => n.Name), Is.EqualTo(new[] { "a", "a" }));
            Assert.That(pattern.Names.Select(n => n.Span.Start), Is.EqualTo(new[] { first, second }));
            Assert.That(pattern.Children, Is.EqualTo(pattern.Names));
            Assert.That(pattern.Text, Is.EqualTo("(\n a, /* 🌍 */ a\n)"));
            Assert.That(binding.Body.Span.Start, Is.EqualTo(source.IndexOf('@')));
        });
    }

    [Test]
    public void PatternRemainsDistinctFromOtherParenthesizedForms()
    {
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse("choose(T(1, 2) | (a, b) :> (@a | trim), (\"key\" => 2), (trim), T(1, 2))")).Pipeline.Single();
        var binding = (InputBindingExpressionSyntax)((ClosedExpressionSyntax)call.Arguments[0].Value!).Pipeline.Single();
        Assert.Multiple(() =>
        {
            Assert.That(binding.Binding, Is.TypeOf<PositionalBindingPatternSyntax>());
            Assert.That(call.Arguments[1].Value, Is.TypeOf<PairLiteralSyntax>());
            Assert.That(call.Arguments[2].Value, Is.TypeOf<ParenthesizedExpressionSyntax>());
            Assert.That(call.Arguments[3].Value, Is.TypeOf<TupleLiteralSyntax>());
        });
    }

    [TestCase("adjacent((previous, current) :> @current | subtract(@previous))")]
    [TestCase("apply(expression := (a,b) :> @a)")]
    [TestCase("apply(((a,b) :> @a))")]
    [TestCase("(a,b) :> @a")]
    [TestCase("trim | ((a,b) :> @a)")]
    [TestCase("trim | () :> upper")]
    [TestCase("trim | (a) :> upper")]
    [TestCase("trim | (a,) :> upper")]
    [TestCase("trim | (a,b,) :> upper")]
    [TestCase("trim | (a,,b) :> upper")]
    [TestCase("trim | (a,b) :>")]
    [TestCase("trim | (a,b :> upper")]
    [TestCase("trim | (a,1b) :> upper")]
    [TestCase("trim | (a,first-name) :> upper")]
    [TestCase("trim | (a,(b,c)) :> upper")]
    [TestCase("trim | (a,...b) :> upper")]
    public void UnsupportedPositionalBindingIsRejected(string source)
        => Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse(source));
}
