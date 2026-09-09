namespace Expressif.Syntax.Tests;

public class FromEndTupleTests
{
    [TestCase("$0", 0, TupleProjectionDirection.FromStart)]
    [TestCase("$-1", 1, TupleProjectionDirection.FromEnd)]
    [TestCase("$-2", 2, TupleProjectionDirection.FromEnd)]
    [TestCase("$-12", 12, TupleProjectionDirection.FromEnd)]
    [TestCase("$-2147483647", int.MaxValue, TupleProjectionDirection.FromEnd)]
    [TestCase("$^0", 0, TupleProjectionDirection.FromEnd)]
    [TestCase("$^1", 1, TupleProjectionDirection.FromEnd)]
    [TestCase("$^12", 12, TupleProjectionDirection.FromEnd)]
    public void ReferencePreservesWrittenIndex(string source, int index, TupleProjectionDirection direction)
    {
        var projection = Descendants(ExpressifSyntax.Parse(source)).OfType<TupleProjectionSyntax>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(projection.Kind, Is.EqualTo(SyntaxKind.TupleProjection));
            Assert.That(projection.Direction, Is.EqualTo(direction));
            Assert.That(projection.Index, Is.EqualTo(index));
            Assert.That(projection.Text, Is.EqualTo(source));
            Assert.That(projection.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
            Assert.That(projection.Root, Is.Null);
            Assert.That(projection.RootDepth, Is.Zero);
            Assert.That(projection.Children, Is.Empty);
        });
    }

    [TestCase("  {0}  ")]
    [TestCase("{0} | upper")]
    [TestCase("lower | {0}")]
    [TestCase("T(1, 2) | {0}")]
    [TestCase("select({0})")]
    [TestCase("select({0} | upper)")]
    [TestCase("select(({0}))")]
    [TestCase("apply(T(1, 2) | add({0}))")]
    [TestCase("select({{{0} | upper}})")]
    [TestCase("tuple(...{0})")]
    [TestCase("select(\n  {0}\n)")]
    public void CompositionsPreserveSpellingAndSpans(string format)
    {
        foreach (var reference in new[] { "$-12", "$^12" })
        {
            var source = string.Format(format, reference);
            var projection = Descendants(ExpressifSyntax.Parse(source)).OfType<TupleProjectionSyntax>().Single();
            Assert.Multiple(() =>
            {
                Assert.That(projection.Text, Is.EqualTo(reference));
                Assert.That(projection.Span, Is.EqualTo(new SourceSpan(source.IndexOf(reference, StringComparison.Ordinal), reference.Length)));
                Assert.That(projection.Direction, Is.EqualTo(TupleProjectionDirection.FromEnd));
                Assert.That(projection.Index, Is.EqualTo(12));
                Assert.That(projection.RootDepth, Is.Zero);
                Assert.That(projection.Children, Is.Empty);
            });
        }
    }

    [TestCase("$-0")]
    [TestCase("$-00")]
    [TestCase("$-01")]
    [TestCase("$-")]
    [TestCase("$--1")]
    [TestCase("$-+1")]
    [TestCase("$^-1")]
    [TestCase("$-^1")]
    [TestCase("$ -1")]
    [TestCase("$- 1")]
    [TestCase("$-1.5")]
    [TestCase("$-2147483648")]
    [TestCase("^^$-1")]
    [TestCase("^^$^1")]
    public void MalformedReferencesAreRejected(string source)
        => Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse(source));

    [Test]
    public void QuotedTextAndCommentsAreNotReferences()
    {
        const string source = "select(\"$-1 $^0\", `$-0 $^1`) /* $-2 */ // $^2";
        var document = ExpressifSyntax.ParseDocument(source);
        Assert.Multiple(() =>
        {
            Assert.That(document.Text, Is.EqualTo(source));
            Assert.That(Descendants(document.Expression).OfType<TupleProjectionSyntax>(), Is.Empty);
            Assert.That(document.Comments.Select(comment => comment.Text), Is.EqualTo(new[] { "/* $-2 */", "// $^2" }));
            Assert.That(Descendants(document.Expression).OfType<QuotedLiteralSyntax>().Select(literal => literal.Text),
                Is.EqualTo(new[] { "\"$-1 $^0\"", "`$-0 $^1`" }));
        });
    }

    private static IEnumerable<SyntaxNode> Descendants(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Descendants(child))
                yield return descendant;
    }
}
