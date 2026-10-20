namespace HC.Business;

/// <summary>
/// What a shopper's search text is read as: the words the catalogue is asked for.
///
/// The header's search box is typed into freely - "wooden vase", "vase wooden", "Vase" - and all three mean the same
/// shelf. Reading the text word by word is what makes them mean it: each word has to appear somewhere in a product (see
/// <see cref="ProductService.SearchProductsAsync"/>), so the order the words are typed in does not matter and a second
/// word narrows the answer instead of being looked for as one long phrase.
///
/// The two limits live here rather than in the query. Each word becomes one LIKE pattern, so a word longer than
/// <see cref="MaxTermLength"/> is cut down and a search beyond <see cref="MaxTerms"/> words is read as its first ones:
/// an unbounded pattern is a walk of the catalogue's rows paid for by a shopper who is only guessing.
/// </summary>
public static class ProductSearch
{
    /// <summary>The most words a search is read as; a narrower search is the shopper's to make.</summary>
    public const int MaxTerms = 8;

    /// <summary>The longest a single word may be; a longer one is cut to this length.</summary>
    public const int MaxTermLength = 64;

    /// <summary>
    /// The shortest search the header's box asks the shop about while a shopper is still typing: one letter names
    /// half the catalogue, so nothing is suggested until two letters are in the box. The full search (see
    /// <see cref="ProductService.SearchProductsAsync"/>) has no such floor - a shopper who submits a single letter has
    /// asked for it.
    /// </summary>
    public const int ShortestSuggestedQuery = 2;

    /// <summary>
    /// The most pieces the header's box suggests. A suggestion is a shortcut to one piece, not a listing - the rest of
    /// what the words answer with is the shop page's to show (see <see cref="ProductService.SearchProductsAsync"/>).
    /// </summary>
    public const int MaxSuggestedProducts = 6;

    /// <summary>
    /// The most collections the header's box suggests. They are offered before the pieces, because a collection is the
    /// narrower thing to mean: a shopper typing a heading's name is on their way to the collection, and opening it
    /// shows everything filed beneath it.
    /// </summary>
    public const int MaxSuggestedCategories = 4;

    /// <summary>
    /// The words of a shopper's search text, in the order they were typed, without the repeats (a word typed twice
    /// would only ask the same question twice) and without blank entries. An empty or blank search is no words at all,
    /// which the query reads as 'nothing was asked' rather than as 'everything was asked for'.
    /// </summary>
    public static IReadOnlyList<string> Terms(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<string>();

        return query
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => word.Length > MaxTermLength ? word[..MaxTermLength] : word)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTerms)
            .ToList();
    }
}
