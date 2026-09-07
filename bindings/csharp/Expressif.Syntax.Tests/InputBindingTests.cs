namespace Expressif.Syntax.Tests;

public class InputBindingTests
{
    private static InputBoundExpressionSyntax Binding(string source)
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var call = (FunctionCallSyntax)root.Pipeline.Single();
        return (InputBoundExpressionSyntax)((PositionalArgumentSyntax)call.Arguments[0]).Value;
    }

    [TestCase("apply(input :> @input | trim)", "input")]
    [TestCase("apply(input1:> @input1 | trim)", "input1")]
    [TestCase("apply(a1b :> @a1b | trim)", "a1b")]
    public void NamedBindingPreservesDeclarationBodyAndReferences(string source, string name)
    {
        var expression = Binding(source);
        var binding = (BindingNameSyntax)expression.Binding!;
        var body = (ClosedExpressionSyntax)expression.Body;
        Assert.Multiple(() =>
        {
            Assert.That(expression.Kind, Is.EqualTo(SyntaxKind.InputBoundExpression));
            Assert.That(binding.Kind, Is.EqualTo(SyntaxKind.BindingName));
            Assert.That(binding.Name, Is.EqualTo(name));
            Assert.That(binding.Text, Is.EqualTo(name));
            Assert.That(binding.Children, Is.Empty);
            Assert.That(binding.Span, Is.EqualTo(new SourceSpan(6, name.Length)));
            Assert.That(expression.Children, Is.EqualTo(new SyntaxNode[] { binding, body }));
            Assert.That(body.Value, Is.TypeOf<VariableSyntax>());
            Assert.That(body.Value.Text, Is.EqualTo("@" + name));
            Assert.That(body.Pipeline, Has.Count.EqualTo(1));
            Assert.That(expression.Text, Is.EqualTo(source[6..^1]));
            Assert.That(expression.Span, Is.EqualTo(new SourceSpan(6, source.Length - 7)));
            Assert.That(body.Text, Is.EqualTo("@" + name + " | trim"));
        });
    }

    [TestCase("adjacent(:> $1 | subtract($0) | multiply($1))", "$1 | subtract($0) | multiply($1)")]
    [TestCase("apply(:> .field | trim)", ".field | trim")]
    [TestCase("apply(:> #null)", "#null")]
    [TestCase("apply(:> ^^$1 | subtract(^$0))", "^^$1 | subtract(^$0)")]
    public void AnonymousBindingHasOnlyItsCompleteBody(string source, string bodyText)
    {
        var expression = Binding(source);
        Assert.Multiple(() =>
        {
            Assert.That(expression.Binding, Is.Null);
            Assert.That(expression.Children, Is.EqualTo(new[] { expression.Body }));
            Assert.That(expression.Body.Text, Is.EqualTo(bodyText));
            Assert.That(expression.Body.Span, Is.EqualTo(new SourceSpan(source.IndexOf(bodyText, StringComparison.Ordinal), bodyText.Length)));
        });
    }

    [Test]
    public void NestedBindingsEndAtTheirOwnArgumentBoundaries()
    {
        const string source = "choose(outer :> apply(inner :> @inner | trim) | upper, :> #null)";
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse(source);
        var call = (FunctionCallSyntax)root.Pipeline.Single();
        var outer = (InputBoundExpressionSyntax)((PositionalArgumentSyntax)call.Arguments[0]).Value;
        var outerBody = (OpenExpressionSyntax)outer.Body;
        var innerCall = (FunctionCallSyntax)outerBody.Pipeline[0];
        var inner = (InputBoundExpressionSyntax)((PositionalArgumentSyntax)innerCall.Arguments[0]).Value;
        var sibling = (InputBoundExpressionSyntax)((PositionalArgumentSyntax)call.Arguments[1]).Value;
        Assert.Multiple(() =>
        {
            Assert.That(call.Arguments, Has.Count.EqualTo(2));
            Assert.That(outerBody.Pipeline, Has.Count.EqualTo(2));
            Assert.That(inner.Text, Is.EqualTo("inner :> @inner | trim"));
            Assert.That(sibling.Text, Is.EqualTo(":> #null"));
        });
    }

    [Test]
    public void NamedArgumentCanContainBindingAndBareBodyNameRemainsFunction()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("apply(transform := input :> input, :text)");
        var call = (FunctionCallSyntax)root.Pipeline.Single();
        var argument = (NamedArgumentSyntax)call.Arguments[0];
        var expression = (InputBoundExpressionSyntax)argument.Value;
        var body = (OpenExpressionSyntax)expression.Body;
        Assert.Multiple(() =>
        {
            Assert.That(call.Arguments, Has.Count.EqualTo(2));
            Assert.That(body.Pipeline.Single(), Is.TypeOf<FunctionCallSyntax>());
            Assert.That(body.Pipeline.Single().Text, Is.EqualTo("input"));
        });
    }

    [Test]
    public void MultilineBindingPreservesCommentsAndUtf16Spans()
    {
        const string source = "apply(\n  input /* 🌍 */ :>\n  @input | trim\n)";
        var document = ExpressifSyntax.ParseDocument(source);
        var expression = Binding(source);
        Assert.Multiple(() =>
        {
            Assert.That(document.Comments.Single().Text, Is.EqualTo("/* 🌍 */"));
            Assert.That(expression.Text, Is.EqualTo("input /* 🌍 */ :>\n  @input | trim"));
            Assert.That(expression.Span, Is.EqualTo(new SourceSpan(9, expression.Text.Length)));
            Assert.That(expression.Body.Span.Start, Is.EqualTo(source.IndexOf('@')));
            Assert.That(expression.Body.Span.End, Is.EqualTo(source.IndexOf("\n)", StringComparison.Ordinal)));
        });
    }

    [TestCase("apply(:>)")]
    [TestCase("apply(name :>)")]
    [TestCase("apply(name : > trim)")]
    [TestCase("apply(1name :> trim)")]
    [TestCase("apply(first-name :> trim)")]
    [TestCase("apply(_name :> trim)")]
    [TestCase("apply(@name :> trim)")]
    [TestCase("apply(name :> trim |)")]
    public void MalformedBindingProducesSyntaxDiagnostic(string source)
    {
        var error = Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse(source));
        Assert.That(error!.Errors, Is.Not.Empty);
    }
}
