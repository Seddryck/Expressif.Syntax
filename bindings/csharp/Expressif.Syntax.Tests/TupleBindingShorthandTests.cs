namespace Expressif.Syntax.Tests;

public class TupleBindingShorthandTests
{
    [TestCase("{0}")]
    [TestCase("/*before*/ {0} /*after*/")]
    [TestCase("\uFEFF\u200B{0}\u2060")]
    [TestCase("  {0}  ")]
    [TestCase("{0} | upper")]
    [TestCase("lower | {0}")]
    [TestCase("T(120, 135) | {0}")]
    [TestCase("T(1, 2, 3) | {0}")]
    [TestCase("extend({0})")]
    [TestCase("extend(fn := {0})")]
    [TestCase("extend({0} | upper)")]
    [TestCase("extend(({0}))")]
    [TestCase("({0})")]
    [TestCase("*{0}")]
    [TestCase("!{0}")]
    [TestCase("*!{0}")]
    [TestCase("{0} |AND check")]
    [TestCase("|> {0}")]
    [TestCase("T(1, 2) |> {0}")]
    [TestCase("extend(\n  {0}\n)")]
    [TestCase("extend(\"\u00e9\ud83d\ude00\", {0})")]
    public void CompositionsPreserveIdentityDirectionAndSpans(string format)
    {
        foreach (var name in new[] { "subtract", "Unknown-function" })
        foreach (var prefix in new[] { true, false })
        {
            var shorthand = prefix ? "~" + name : name + "~";
            var source = string.Format(format, shorthand);
            var node = Descendants(ExpressifSyntax.Parse(source)).OfType<TupleBindingShorthandSyntax>().Single();
            Assert.Multiple(() =>
            {
                Assert.That(node.Kind, Is.EqualTo(SyntaxKind.TupleBindingShorthand));
                Assert.That(node.Name, Is.EqualTo(name));
                Assert.That(node.Direction, Is.EqualTo(prefix ? TupleBindingDirection.Prefix : TupleBindingDirection.Postfix));
                Assert.That(node.Text, Is.EqualTo(shorthand));
                Assert.That(node.Span, Is.EqualTo(new SourceSpan(source.IndexOf(shorthand, StringComparison.Ordinal), shorthand.Length)));
                Assert.That(node.NameSpan, Is.EqualTo(new SourceSpan(source.IndexOf(name, StringComparison.Ordinal), name.Length)));
                Assert.That(node.TildeSpan, Is.EqualTo(new SourceSpan(source.IndexOf('~'), 1)));
                Assert.That(node.Children, Is.Empty);
            });
        }
    }

    [TestCase("~")]
    [TestCase("~~subtract")]
    [TestCase("subtract~~")]
    [TestCase("~subtract~")]
    [TestCase("~\u200Bsubtract")]
    [TestCase("subtract\uFEFF~")]
    [TestCase("~ subtract")]
    [TestCase("subtract ~")]
    [TestCase("~\nsubtract")]
    [TestCase("subtract\n~")]
    [TestCase("~/*comment*/subtract")]
    [TestCase("subtract/*comment*/~")]
    [TestCase("~subtract()")]
    [TestCase("subtract()~")]
    [TestCase("subtract~(1)")]
    [TestCase("~(subtract)")]
    [TestCase("(subtract)~")]
    [TestCase("~!subtract")]
    [TestCase("~*subtract")]
    [TestCase("~@subtract")]
    [TestCase("~subtract1")]
    [TestCase("subtract1~")]
    [TestCase("~subtract_name")]
    [TestCase("subtract_name~")]
    [TestCase("~subtract-")]
    [TestCase("extend(~, upper)")]
    public void MalformedFormsAreRejected(string source)
    {
        var error = Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse(source));
        Assert.That(error!.Errors, Is.Not.Empty);
    }

    private static IEnumerable<SyntaxNode> Descendants(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Descendants(child))
                yield return descendant;
    }
}
