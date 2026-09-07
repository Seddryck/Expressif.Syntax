namespace Expressif.Syntax.Tests;

public class TupleRootTests
{
    [TestCase("$1", 0, 1, TupleProjectionDirection.FromStart)]
    [TestCase("$^0", 0, 0, TupleProjectionDirection.FromEnd)]
    [TestCase("$^1", 0, 1, TupleProjectionDirection.FromEnd)]
    [TestCase("^$1", 1, 1, TupleProjectionDirection.FromStart)]
    [TestCase("^^$1", 2, 1, TupleProjectionDirection.FromStart)]
    [TestCase("^^^$1", 3, 1, TupleProjectionDirection.FromStart)]
    [TestCase("^^^^$1", 4, 1, TupleProjectionDirection.FromStart)]
    [TestCase("^^^^^^^^^^$12", 10, 12, TupleProjectionDirection.FromStart)]
    public void ProjectionPreservesRootAndPosition(string text, int depth, int index, TupleProjectionDirection direction)
    {
        var projection = (TupleProjectionSyntax)((OpenExpressionSyntax)ExpressifSyntax.Parse(text)).Source!;
        Assert.Multiple(() =>
        {
            Assert.That(projection.Kind, Is.EqualTo(SyntaxKind.TupleProjection));
            Assert.That(projection.Text, Is.EqualTo(text));
            Assert.That(projection.Span, Is.EqualTo(new SourceSpan(0, text.Length)));
            Assert.That(projection.RootDepth, Is.EqualTo(depth));
            Assert.That(projection.Direction, Is.EqualTo(direction));
            Assert.That(projection.Index, Is.EqualTo(index));
            Assert.That(projection.Children, Is.EqualTo(projection.Root is null ? [] : new SyntaxNode[] { projection.Root }));
        });
        if (depth == 0)
        {
            Assert.That(projection.Root, Is.Null);
            return;
        }

        var record = (RecordAccessSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(new string('^', depth) + ".name")).Value;
        Assert.Multiple(() =>
        {
            Assert.That(projection.Root, Is.TypeOf<ExpressionRootSyntax>());
            Assert.That(projection.Root!.Kind, Is.EqualTo(SyntaxKind.ExpressionRoot));
            Assert.That(projection.Root.Depth, Is.EqualTo(record.Root!.Depth));
            Assert.That(projection.RootDepth, Is.EqualTo(record.RootDepth));
            Assert.That(projection.Root.Text, Is.EqualTo(new string('^', depth) + "$"));
            Assert.That(projection.Root.Span, Is.EqualTo(new SourceSpan(0, depth + 1)));
            Assert.That(projection.Root.Children, Is.Empty);
        });
    }

    [TestCase("  ^^^$12  ")]
    [TestCase("^^^$12 | upper")]
    [TestCase("lower | ^^^$12")]
    [TestCase("T(1, 2) | ^^^$12")]
    [TestCase("select(^^^$12)")]
    [TestCase("select(^^^$12 | upper)")]
    [TestCase("select((^^^$12))")]
    [TestCase("apply(T(1, 2) | add(^^^$12))")]
    [TestCase("select({^^^$12 | upper})")]
    [TestCase("tuple(...^^^$12)")]
    public void CompositionsPreserveAuthoredSpans(string source)
    {
        var projection = Descendants(ExpressifSyntax.Parse(source)).OfType<TupleProjectionSyntax>().Single();
        var offset = source.IndexOf("^^^$12", StringComparison.Ordinal);
        Assert.Multiple(() =>
        {
            Assert.That(projection.Text, Is.EqualTo("^^^$12"));
            Assert.That(projection.Span, Is.EqualTo(new SourceSpan(offset, 6)));
            Assert.That(projection.Root!.Text, Is.EqualTo("^^^$"));
            Assert.That(projection.Root.Span, Is.EqualTo(new SourceSpan(offset, 4)));
            Assert.That(projection.RootDepth, Is.EqualTo(3));
        });
    }

    [TestCase("^$")]
    [TestCase("^^$")]
    [TestCase("^$-1")]
    [TestCase("^^$^1")]
    [TestCase("^ $1")]
    [TestCase("^$ 1")]
    [TestCase("^ ^$1")]
    [TestCase("^$01")]
    [TestCase("^$2147483648")]
    public void MalformedReferencesAreRejected(string source)
        => Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse(source));

    [Test]
    public void QuotedTextAndCommentsAreNotReferences()
    {
        const string source = "select(\"^^^$1\", `^^^^$2`) /* ^^$3 */ // ^$4";
        var document = ExpressifSyntax.ParseDocument(source);
        Assert.Multiple(() =>
        {
            Assert.That(document.Text, Is.EqualTo(source));
            Assert.That(Descendants(document.Expression).OfType<TupleProjectionSyntax>(), Is.Empty);
            Assert.That(document.Comments.Select(comment => comment.Text), Is.EqualTo(new[] { "/* ^^$3 */", "// ^$4" }));
            Assert.That(Descendants(document.Expression).OfType<QuotedLiteralSyntax>().Select(literal => literal.Text),
                Is.EqualTo(new[] { "\"^^^$1\"", "`^^^^$2`" }));
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
