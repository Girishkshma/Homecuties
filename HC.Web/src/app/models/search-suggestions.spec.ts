import { rowsFor, SHORTEST_SUGGESTED_QUERY } from './search-suggestions';

/**
 * What the header's search box draws under itself while a shopper is still typing.
 *
 * The rows are where a click goes, so what they say and the order they are in is the whole of the list's meaning: the
 * shopper's own search first (so the listing is always one row away), the collections the word names next (the shop's
 * answer to a heading, which opens the branch under it), then the pieces by name. Below the shortest suggested search
 * the shop is not asked at all, so there is nothing to draw.
 */
describe('Search suggestion rows', () => {
  it('offers the shopper their own search first, whatever else the shop answered', () => {
    const rows = rowsFor('vase', {
      Categories: [category(6, 'Vases')],
      Products: [piece(10006, 'Flower Vase, Peacock Painted, Glossy Finish')]
    });

    expect(rows.length).toBe(3);
    expect(rows[0].kind).toBe('search');
    expect(rows[0].label).toBe('Search the shop for \u201Cvase\u201D');
    expect(rows[0].hint).toBe('All results');

    // The words lead to the shop's listing, which is what the form submits: no id is needed to go there.
    expect(rows[0].categoryId).toBeUndefined();
    expect(rows[0].productId).toBeUndefined();
  });

  it('offers the collections before the pieces, each row carrying where it opens', () => {
    const rows = rowsFor('decor', {
      Categories: [category(2, 'Decoratives'), category(60, 'Decorative Lights')],
      Products: [piece(10001, 'Pot Set, Decorative, 6 assorted sizes')]
    });

    expect(rows.map(row => row.kind)).toEqual(['search', 'category', 'category', 'product']);
    expect(rows.map(row => row.label)).toEqual([
      'Search the shop for \u201Cdecor\u201D',
      'Decoratives',
      'Decorative Lights',
      'Pot Set, Decorative, 6 assorted sizes'
    ]);

    // A collection row opens the shop on that collection; a piece row opens that piece's page.
    expect(rows[1].categoryId).toBe(2);
    expect(rows[2].categoryId).toBe(60);
    expect(rows[3].productId).toBe(10001);
    expect(rows[3].categoryId).toBeUndefined();
    expect(rows[1].hint).toBe('Collection');
    expect(rows[3].hint).toBe('Product');
  });

  it('draws nothing for a search the shop was not asked about', () => {
    const answered = { Categories: [category(6, 'Vases')], Products: [piece(10006, 'Flower Vase')] };

    // One letter names half the shop, so the shop is not asked and no list is drawn - not even the search row.
    expect(SHORTEST_SUGGESTED_QUERY).toBe(2);
    expect(rowsFor('v', answered)).toEqual([]);
    expect(rowsFor('', answered)).toEqual([]);
  });

  it('still offers the search when the shop has nothing to suggest', () => {
    // A word no name carries: 'nothing matched' is the shop page's to say, and the row leads there.
    const rows = rowsFor('zzz', { Categories: [], Products: [] });

    expect(rows.length).toBe(1);
    expect(rows[0].kind).toBe('search');
    expect(rows[0].label).toBe('Search the shop for \u201Czzz\u201D');
  });
});

function category(categoryId: number, categoryName: string) {
  return { CategoryID: categoryId, CategoryName: categoryName, ParentCategoryID: null };
}

function piece(productId: number, productTitle: string) {
  return { ProductID: productId, ProductTitle: productTitle };
}
