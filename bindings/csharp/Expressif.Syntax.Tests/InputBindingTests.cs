namespace Expressif.Syntax.Tests;

public class InputBindingTests
{
    internal static IReadOnlyList<ExpressionSyntax> Pipeline(RootExpressionSyntax root)
        => root switch
        {
            OpenExpressionSyntax open => open.Pipeline,
            ClosedExpressionSyntax closed => closed.Pipeline,
            _ => throw new InvalidOperationException(),
        };

    internal static InputBindingExpressionSyntax Binding(string source)
        => Pipeline(ExpressifSyntax.Parse(source)).OfType<InputBindingExpressionSyntax>().Single();

    internal static RootExpressionSyntax GroupedBody(InputBindingExpressionSyntax binding)
        => ((ParenthesizedExpressionSyntax)((OpenExpressionSyntax)binding.Body).Pipeline.Single()).Expression;

    [TestCase("10 | input :> @input | add(1)", "input")]
    [TestCase("10|input1:>@input1 | add(1)", "input1")]
    [TestCase("trim | a1b :> @a1b | upper", "a1b")]
    public void NamedBindingPreservesDeclarationAndCompleteBody(string source, string name)
    {
        var expression = Binding(source);
        var binding = (BindingNameSyntax)expression.Binding!;
        var body = (ClosedExpressionSyntax)expression.Body;
        var start = source.IndexOf(name, StringComparison.Ordinal);
        Assert.Multiple(() =>
        {
            Assert.That(expression.Kind, Is.EqualTo(SyntaxKind.InputBindingExpression));
            Assert.That(binding.Kind, Is.EqualTo(SyntaxKind.BindingName));
            Assert.That(binding.Name, Is.EqualTo(name));
            Assert.That(binding.Children, Is.Empty);
            Assert.That(binding.Span, Is.EqualTo(new SourceSpan(start, name.Length)));
            Assert.That(expression.Children, Is.EqualTo(new SyntaxNode[] { binding, body }));
            Assert.That(body.Value, Is.TypeOf<VariableSyntax>());
            Assert.That(body.Value.Text, Is.EqualTo("@" + name));
            Assert.That(body.Pipeline, Has.Count.EqualTo(1));
            Assert.That(expression.Text, Is.EqualTo(source[start..]));
            Assert.That(expression.Span, Is.EqualTo(new SourceSpan(start, source.Length - start)));
        });
    }

    [TestCase("10 | :> add(2)")]
    [TestCase("10|:>add(2)")]
    [TestCase("10 |:> add(2)")]
    [TestCase("10| :> add(2)")]
    public void CompactAndSpacedAnonymousBindingHaveIdenticalStructure(string source)
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        var binding = (InputBindingExpressionSyntax)root.Pipeline.Single();
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)binding.Body).Pipeline.Single();
        Assert.Multiple(() =>
        {
            Assert.That(root.Value.Text, Is.EqualTo("10"));
            Assert.That(binding.Binding, Is.Null);
            Assert.That(binding.Children, Is.EqualTo(new[] { binding.Body }));
            Assert.That(call.Name, Is.EqualTo("add"));
            Assert.That(call.Arguments.Single().Value!.Text, Is.EqualTo("2"));
            Assert.That(binding.Span.Start, Is.EqualTo(source.IndexOf(":>", StringComparison.Ordinal)));
        });
    }

    [TestCase("@n | add(1)", typeof(ClosedExpressionSyntax), "@n")]
    [TestCase("42 | add(1)", typeof(ClosedExpressionSyntax), "42")]
    [TestCase(".field | upper", typeof(ClosedExpressionSyntax), ".field")]
    [TestCase("$0 | add($1)", typeof(OpenExpressionSyntax), "$0")]
    [TestCase("^^$1 | subtract(^$0)", typeof(OpenExpressionSyntax), "^^$1")]
    [TestCase("trim | upper", typeof(OpenExpressionSyntax), "trim")]
    public void GroupedBodyRetainsItsInnerRootAndOuterContinuation(string bodyText, Type rootType, string firstText)
    {
        var source = "10 | n :> (" + bodyText + ") | round";
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        var binding = (InputBindingExpressionSyntax)root.Pipeline[0];
        var wrapper = (OpenExpressionSyntax)binding.Body;
        var group = (ParenthesizedExpressionSyntax)wrapper.Pipeline.Single();
        var inner = group.Expression;
        Assert.Multiple(() =>
        {
            Assert.That(root.Pipeline, Has.Count.EqualTo(2));
            Assert.That(root.Pipeline[1].Text, Is.EqualTo("round"));
            Assert.That(inner, Is.TypeOf(rootType));
            Assert.That(inner.Children[0].Text, Is.EqualTo(firstText));
            Assert.That(inner.Text, Is.EqualTo(bodyText));
            Assert.That(group.Text, Is.EqualTo("(" + bodyText + ")"));
            Assert.That(wrapper.Text, Is.EqualTo(group.Text));
            Assert.That(wrapper.Children, Is.EqualTo(new[] { group }));
            Assert.That(group.Children, Is.EqualTo(new[] { inner }));
            Assert.That(group.Span, Is.EqualTo(new SourceSpan(source.IndexOf('('), bodyText.Length + 2)));
            Assert.That(inner.Span, Is.EqualTo(new SourceSpan(source.IndexOf('(') + 1, bodyText.Length)));
            Assert.That(binding.Span.End, Is.EqualTo(source.LastIndexOf(')') + 1));
        });
    }

    [TestCase("10|:>(@_ | add(1))|round")]
    [TestCase("10 | :> (@_ | add(1)) | round")]
    public void CompactGroupedAnonymousBindingKeepsOuterContinuation(string source)
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse(source);
        var binding = (InputBindingExpressionSyntax)root.Pipeline[0];
        Assert.Multiple(() =>
        {
            Assert.That(root.Pipeline, Has.Count.EqualTo(2));
            Assert.That(binding.Binding, Is.Null);
            Assert.That(((ClosedExpressionSyntax)GroupedBody(binding)).Value, Is.TypeOf<IncomingValueSyntax>());
            Assert.That(root.Pipeline[1].Text, Is.EqualTo("round"));
        });
    }

    [TestCase("@n | add(1) | multiply(2)")]
    [TestCase("$0 | add(1) | multiply(2)")]
    [TestCase("trim | upper | lower")]
    public void UngroupedBodyOwnsRemainingPipeline(string bodyText)
    {
        var root = (ClosedExpressionSyntax)ExpressifSyntax.Parse("10 | n :> " + bodyText);
        var binding = (InputBindingExpressionSyntax)root.Pipeline.Single();
        Assert.Multiple(() =>
        {
            Assert.That(binding.Body.Text, Is.EqualTo(bodyText));
            Assert.That(binding.Body.Children, Has.Count.EqualTo(3));
        });
    }

    [TestCase("apply(trim | input :> (@input | upper))", "trim")]
    [TestCase("adjacent($1 | current :> (@current | multiply(2)))", "$1")]
    public void ArgumentPipelineHasItsOwnPrecedingExpression(string source, string preceding)
    {
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse(source)).Pipeline.Single();
        var argument = (RootExpressionSyntax)call.Arguments.Single().Value!;
        var binding = Pipeline(argument).OfType<InputBindingExpressionSyntax>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(argument.Children[0].Text, Is.EqualTo(preceding));
            Assert.That(argument.Children[1], Is.SameAs(binding));
            Assert.That(((ClosedExpressionSyntax)GroupedBody(binding)).Value, Is.TypeOf<VariableSyntax>());
        });
    }

    [Test]
    public void NestedBindingsAndSiblingArgumentsRetainTheirBoundaries()
    {
        var root = (OpenExpressionSyntax)ExpressifSyntax.Parse("choose(trim | outer :> apply(@outer | inner :> @inner | upper) | lower, 10 | :> add(1))");
        var call = (FunctionCallSyntax)root.Pipeline.Single();
        var outer = (InputBindingExpressionSyntax)((OpenExpressionSyntax)call.Arguments[0].Value!).Pipeline[1];
        var outerBody = (OpenExpressionSyntax)outer.Body;
        var innerCall = (FunctionCallSyntax)outerBody.Pipeline[0];
        var inner = (InputBindingExpressionSyntax)((ClosedExpressionSyntax)innerCall.Arguments[0].Value!).Pipeline.Single();
        var sibling = (InputBindingExpressionSyntax)((ClosedExpressionSyntax)call.Arguments[1].Value!).Pipeline.Single();
        Assert.Multiple(() =>
        {
            Assert.That(call.Arguments, Has.Count.EqualTo(2));
            Assert.That(outerBody.Pipeline, Has.Count.EqualTo(2));
            Assert.That(inner.Body.Text, Is.EqualTo("@inner | upper"));
            Assert.That(sibling.Body.Text, Is.EqualTo("add(1)"));
        });
    }

    [Test]
    public void NamedArgumentCanContainPipelineBindingAndBareNameRemainsFunction()
    {
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse("apply(expression := trim | input :> input, :text)")).Pipeline.Single();
        var pipeline = (OpenExpressionSyntax)((NamedArgumentSyntax)call.Arguments[0]).Value;
        var binding = (InputBindingExpressionSyntax)pipeline.Pipeline[1];
        Assert.That(((OpenExpressionSyntax)binding.Body).Pipeline.Single(), Is.TypeOf<FunctionCallSyntax>());
        Assert.That(call.Arguments, Has.Count.EqualTo(2));
    }

    [Test]
    public void MultilineSpansAndCommentsRemainLossless()
    {
        const string source = "trim |\n input /* 🌍 */ :>\n (@input | upper)\n | lower";
        var document = ExpressifSyntax.ParseDocument(source);
        var binding = Binding(source);
        var group = (ParenthesizedExpressionSyntax)((OpenExpressionSyntax)binding.Body).Pipeline.Single();
        Assert.Multiple(() =>
        {
            Assert.That(document.Comments.Single().Text, Is.EqualTo("/* 🌍 */"));
            Assert.That(binding.Text, Is.EqualTo("input /* 🌍 */ :>\n (@input | upper)"));
            Assert.That(binding.Span, Is.EqualTo(new SourceSpan(source.IndexOf("input", StringComparison.Ordinal), binding.Text.Length)));
            Assert.That(group.Expression.Span.Start, Is.EqualTo(source.IndexOf('@')));
            Assert.That(group.Span.End, Is.EqualTo(source.IndexOf(')') + 1));
        });
    }

    [TestCase("apply(input :> @input)")]
    [TestCase("apply(:> $0 | add($1))")]
    [TestCase("map(input :> @input)")]
    [TestCase("apply(expression := input :> @input)")]
    [TestCase("apply((input :> @input))")]
    [TestCase("apply((:> $0))")]
    [TestCase("apply(expression := (:> $0))")]
    [TestCase("input :> @input")]
    [TestCase(":> $0")]
    [TestCase("|:> $0")]
    [TestCase("apply(|:> $0)")]
    [TestCase("trim | (:> $0)")]
    [TestCase("trim | (input :> @input)")]
    public void LeadingBindingIsRejectedAtUnsupportedConstruct(string source)
    {
        var error = Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse(source));
        var marker = source.IndexOf(":>", StringComparison.Ordinal);
        var constructStart = marker > 0 && source[marker - 1] == '|' ? marker - 1 : marker;
        Assert.That(error!.Errors.Any(e => e.Span.Start <= marker + 1 && e.Span.End >= constructStart), Is.True,
            "A diagnostic must identify the binding or its missing preceding expression.");
    }

    [TestCase("trim | :>")]
    [TestCase("trim | name :>")]
    [TestCase("trim | name : > upper")]
    [TestCase("trim | 1name :> upper")]
    [TestCase("trim | first-name :> upper")]
    [TestCase("trim | _name :> upper")]
    [TestCase("trim | @name :> upper")]
    [TestCase("trim | name :> upper |")]
    [TestCase("trim | name :> (upper")]
    [TestCase("trim | name :> ()")]
    public void MalformedPipelineBindingIsRejected(string source)
        => Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse(source));
}
