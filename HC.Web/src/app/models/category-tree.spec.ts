import { Category } from './product.model';
import { groupedByHeading, pathOf } from './category-tree';

/**
 * The storefront's reading of the catalogue: which heading each shelf is drawn under, and how a category is named on
 * a listing.
 *
 * The rules are the ones the server walks the catalogue by (see CategoryTree and CategoryBranch in HC.Business), and
 * the live shop is the shape they are written for: 'Decoratives' is a heading with 'Vases', 'Pot Houses', 'Musicians'
 * and 'Show Pieces' under it, and nothing filed on the heading itself - so a menu that read the rows flat would list
 * the shelves with no way of telling whose collection each is, and a heading would never appear at all.
 *
 * Two things have to hold whatever the catalogue looks like, and they are why the broken shapes are pinned here as
 * well as the live one: every shelf is read under the heading it belongs to (a row cannot be stepped in under itself
 * forever, so a heading that leads into a loop ends the walk), and nothing is ever left out - a row no heading leads
 * into is showable rather than lost, because the products filed under it are on sale.
 */
describe('Category tree', () => {
  it('reads the shelves under the heading each one belongs to', () => {
    const tree = groupedByHeading([
      category(2, 'Decoratives', null),
      category(5, 'Vases', 2),
      category(6, 'Pot Houses', 2),
      category(1, 'Toys', null),
      category(14, 'For Kids', 1)
    ]);

    expect(tree.headings.map(heading => heading.heading.CategoryName)).toEqual(['Decoratives', 'Toys']);
    expect(namesOf(tree.headings[0].rows)).toEqual(['Vases', 'Pot Houses']);
    expect(namesOf(tree.headings[1].rows)).toEqual(['For Kids']);

    // The heading is not one of its own rows, or it would be drawn twice.
    expect(tree.ungrouped).toEqual([]);
  });

  it('steps a shelf of a shelf in under the row it belongs to', () => {
    const tree = groupedByHeading([
      category(1, 'Toys', null),
      category(14, 'For Kids', 1),
      category(17, 'Plush', 14),
      category(15, 'Learning', 1)
    ]);

    // Depth first, siblings in the order the API listed them: 'Plush' sits inside 'For Kids', so it is read there.
    expect(namesOf(tree.headings[0].rows)).toEqual(['For Kids', 'Plush', 'Learning']);
    expect(tree.headings[0].rows.map(row => row.depth)).toEqual([0, 1, 0]);
  });

  it('reads a category whose heading is gone as a heading of its own', () => {
    // The API sends the rows whose parent row the catalogue still holds, so a shelf left without its heading is read
    // as a heading here instead of vanishing under a row nobody can draw.
    const tree = groupedByHeading([category(60, 'Orphan', 99)]);

    expect(tree.headings.map(heading => heading.heading.CategoryName)).toEqual(['Orphan']);
    expect(tree.headings[0].rows).toEqual([]);
  });

  it('lists the rows of a loop in the catalogue rather than dropping them', () => {
    const tree = groupedByHeading([
      category(2, 'Decoratives', null),
      category(5, 'Vases', 2),
      category(70, 'Oddities', 71),
      category(71, 'Odder', 70)
    ]);

    expect(tree.headings.map(heading => heading.heading.CategoryName)).toEqual(['Decoratives']);
    expect(namesOf(tree.headings[0].rows)).toEqual(['Vases']);

    // No heading leads into the broken pair, so it is shown on its own - seen, rather than lost with its products.
    expect(namesOf(tree.ungrouped)).toEqual(['Oddities', 'Odder']);
    expect(tree.ungrouped.map(row => row.depth)).toEqual([0, 0]);
  });

  it('names a category by the way there, heading first', () => {
    const categories = [
      category(2, 'Decoratives', null),
      category(5, 'Vases', 2),
      category(18, 'Tall Vases', 5),
      category(60, 'Orphan', 99)
    ];

    expect(pathOf(categories[2], categories)).toBe('Decoratives / Vases / Tall Vases');
    expect(pathOf(categories[1], categories)).toBe('Decoratives / Vases');
    expect(pathOf(categories[0], categories)).toBe('Decoratives');

    // A heading the list does not hold is not invented, and a loop in the catalogue ends the walk.
    expect(pathOf(categories[3], categories)).toBe('Orphan');
    expect(pathOf(category(70, 'Oddities', 71), [category(70, 'Oddities', 71), category(71, 'Odder', 70)]))
      .toBe('Odder / Oddities');
  });
});

const namesOf = (rows: { category: Category }[]) => rows.map(row => row.category.CategoryName);

const category = (categoryId: number, categoryName: string, parentCategoryId: number | null): Category => ({
  CategoryID: categoryId,
  CategoryName: categoryName,
  ParentCategoryID: parentCategoryId
});
