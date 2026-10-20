using HC.Business;
using Xunit;

namespace HC.Tests;

/// <summary>
/// What the storefront's header search box is read as. The text is typed freely, so the reading is what makes "wooden
/// vase" and "vase wooden" the same search - and what keeps a blank search from being read as a request for the whole
/// shop (see <see cref="ProductSearch"/>).
/// </summary>
public class ProductSearchTests
{
    /// <summary>
    /// A search box left empty, or filled with nothing but spaces, asks for nothing: the query answers with no
    /// products rather than with every product.
    /// </summary>
    [Fact]
    public void ABlankSearchAsksForNothing()
    {
        Assert.Empty(ProductSearch.Terms(null));
        Assert.Empty(ProductSearch.Terms(""));
        Assert.Empty(ProductSearch.Terms("   "));
    }

    /// <summary>The words are read in the order they were typed, and the run of spaces between them means nothing.</summary>
    [Fact]
    public void TheTextIsReadAsItsWordsInTheOrderTheyWereTyped()
    {
        Assert.Equal(new[] { "wooden", "vase" }, ProductSearch.Terms("  wooden   vase "));
    }

    /// <summary>
    /// A word typed twice is asked for once: the second one would ask the same question, and the case it was typed in
    /// makes no difference to what is looked for.
    /// </summary>
    [Fact]
    public void ARepeatedWordIsAskedForOnce()
    {
        Assert.Equal(new[] { "Vase", "wooden" }, ProductSearch.Terms("Vase wooden vase VASE"));
    }

    /// <summary>A search is read as its first words only, so a pasted paragraph cannot become a query of its own.</summary>
    [Fact]
    public void ASearchIsReadAsItsFirstWordsOnly()
    {
        var terms = ProductSearch.Terms("one two three four five six seven eight nine ten");

        Assert.Equal(ProductSearch.MaxTerms, terms.Count);
        Assert.Equal("eight", terms[^1]);
    }

    /// <summary>A word longer than the limit is cut to it rather than turned into one unbounded pattern.</summary>
    [Fact]
    public void AWordLongerThanTheLimitIsCutDown()
    {
        var word = new string('a', ProductSearch.MaxTermLength + 20);

        Assert.Equal(ProductSearch.MaxTermLength, Assert.Single(ProductSearch.Terms(word)).Length);
    }
}
