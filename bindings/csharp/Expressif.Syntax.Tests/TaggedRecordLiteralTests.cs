namespace Expressif.Syntax.Tests;

public class TaggedRecordLiteralTests
{
    [Test]
    public void TaggedRecordPreservesTagRecordSourceAndNesting()
    {
        const string source = "Custom-Type{ payload := Nested{value := 42}, metadata := {source := \"api\"}, ... }";
        var tagged = (TaggedRecordLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;
        var nested = (TaggedRecordLiteralSyntax)tagged.Record.Fields[0].Value!;

        Assert.Multiple(() =>
        {
            Assert.That(tagged.Kind, Is.EqualTo(SyntaxKind.TaggedRecordLiteral));
            Assert.That(tagged.Tag, Is.EqualTo("Custom-Type"));
            Assert.That(tagged.TagSpan, Is.EqualTo(new SourceSpan(0, 11)));
            Assert.That(tagged.Text, Is.EqualTo(source));
            Assert.That(tagged.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
            Assert.That(tagged.Record.Text, Is.EqualTo("{ payload := Nested{value := 42}, metadata := {source := \"api\"}, ... }"));
            Assert.That(tagged.Record.Entries, Has.Count.EqualTo(3));
            Assert.That(tagged.Record.Children, Is.EqualTo(tagged.Record.Entries));
            Assert.That(tagged.Children, Is.EqualTo(new[] { tagged.Record }));
            Assert.That(nested.Tag, Is.EqualTo("Nested"));
            Assert.That(nested.Record.Fields.Single().Name.Value, Is.EqualTo("value"));
            Assert.That(tagged.Record.Fields[1].Value, Is.TypeOf<RecordLiteralSyntax>());
            Assert.That(tagged.Record.Entries[2], Is.TypeOf<RecordSpreadSyntax>());
        });
    }

    [Test]
    public void EmptyTaggedRecordUsesExistingEmptyRecordShape()
    {
        var tagged = (TaggedRecordLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse("SortTable{:}")).Value;

        Assert.Multiple(() =>
        {
            Assert.That(tagged.Tag, Is.EqualTo("SortTable"));
            Assert.That(tagged.Record.Kind, Is.EqualTo(SyntaxKind.RecordLiteral));
            Assert.That(tagged.Record.Text, Is.EqualTo("{:}"));
            Assert.That(tagged.Record.Entries, Is.Empty);
        });
    }

    [Test]
    public void EmptyArraySpellingIsNotAcceptedAsAnEmptyTaggedRecord()
        => Assert.Throws<ExpressifSyntaxException>(() => ExpressifSyntax.Parse("SortTable{}"));

    [Test]
    public void UntaggedRecordRemainsDistinct()
    {
        var untagged = ((ClosedExpressionSyntax)ExpressifSyntax.Parse("{headers := {}, rows := {}}")).Value;
        var tagged = ((ClosedExpressionSyntax)ExpressifSyntax.Parse("SortTable{headers := {}, rows := {}}")).Value;

        Assert.Multiple(() =>
        {
            Assert.That(untagged, Is.TypeOf<RecordLiteralSyntax>());
            Assert.That(tagged, Is.TypeOf<TaggedRecordLiteralSyntax>());
        });
    }

    [TestCase("sort-term(42, compare-numeric~)")]
    [TestCase("sort-key(.country | sort-term(compare-ordinal~), .price | sort-term(compare-numeric~) | desc)")]
    [TestCase("sort-by(.name -> :text | asc | nulls-last)")]
    [TestCase("sort-by(.country -> :text | asc, .price -> :numeric | desc | nulls-last)")]
    [TestCase("SortTerm(42, compare-numeric~, #true, #false)")]
    [TestCase("SortKey(SortTerm(\"BE\", compare-ordinal~, #true, #false), SortTerm(42, compare-numeric~, #false, #false))")]
    public void OrdinarySortSyntaxUsesExistingGrammar(string source)
    {
        var root = ExpressifSyntax.Parse(source);
        var call = (FunctionCallSyntax)((OpenExpressionSyntax)root).Pipeline.Single();

        Assert.That(call.Name, Is.EqualTo(source[..source.IndexOf('(')]));
    }

    [Test]
    public void SerializedSortTableBindsTheCompleteManagedTree()
    {
        const string source = """
            SortTable{
                headers := {
                    T(compare-ordinal~, #true, #false),
                    T(compare-numeric~, #false, #false)
                },
                rows := {
                    T(T("BE", 42), {name := "Alice"})
                }
            }
            """;

        var tagged = (TaggedRecordLiteralSyntax)((ClosedExpressionSyntax)ExpressifSyntax.Parse(source)).Value;
        var headers = (ArrayLiteralSyntax)tagged.Record.Fields[0].Value!;
        var firstHeader = (TupleLiteralSyntax)headers.Elements[0].Expression!;
        var comparer = (OpenExpressionSyntax)firstHeader.Elements[0].Expression!;

        Assert.Multiple(() =>
        {
            Assert.That(tagged.Tag, Is.EqualTo("SortTable"));
            Assert.That(tagged.Record.Fields.Select(field => field.Name.Value), Is.EqualTo(new[] { "headers", "rows" }));
            Assert.That(headers.Elements, Has.Count.EqualTo(2));
            Assert.That(comparer.Pipeline.Single(), Is.TypeOf<TupleBindingShorthandSyntax>()
                .With.Property(nameof(TupleBindingShorthandSyntax.Name)).EqualTo("compare-ordinal"));
            Assert.That(tagged.Text, Is.EqualTo(source));
        });
    }
}
