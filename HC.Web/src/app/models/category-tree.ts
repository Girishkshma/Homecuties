import { Category } from './product.model';

/**
 * One row of the storefront's category menus: a category and how deep it sits beneath the heading it is read under
 * (0 for the heading's own shelf, 1 for a shelf of a shelf, and so on down).
 */
export interface CategoryRow {
  category: Category;
  depth: number;
}

/**
 * A heading of the catalogue and the rows read under it, depth first: a row always before the ones beneath it, and
 * the rows of one parent in the order the API listed them. The heading itself is not one of the rows.
 */
export interface CategoryHeading {
  heading: Category;
  rows: CategoryRow[];
}

/**
 * The catalogue as the storefront draws it: one entry per heading, and the rows no heading leads into.
 */
export interface CategoryTree {
  headings: CategoryHeading[];
  ungrouped: CategoryRow[];
}

/**
 * The category rows the API lists (see ProductService.GetCategoriesAsync and CategoryBranch.ForBrowsing in
 * HC.Business) put into the shape the screens draw: one entry per heading of the catalogue, with the shelves read
 * under it.
 *
 * A heading is a category with no parent - and one whose parent is not in the list, because the row it names is gone.
 * That is the same reading the server takes (see CategoryTree.GroupedByHeading in HC.Business), and it is the reason
 * a shelf is drawn under a name rather than on its own: the shop's heading is not a place a product sits, so the
 * headings travel with the shelves they hold (the API sends them along) and are read here as the group each shelf
 * belongs to.
 *
 * Nothing is dropped: a row caught in a loop in the catalogue (two rows each naming the other as parent) is reached
 * by no heading, so it comes back in 'ungrouped' and the screens list it plainly. Dropping it would hide the products
 * filed under it from the whole shop.
 */
export function groupedByHeading(categories: Category[]): CategoryTree {
  const ids = new Set(categories.map(category => category.CategoryID));
  const childrenOf = new Map<number, Category[]>();

  // Only a child whose parent is in the list is filed under anything: a category whose parent row is gone is a
  // heading here, rather than the child of a row nobody can draw.
  for (const category of categories) {
    const parentId = category.ParentCategoryID;
    if (parentId === null || !ids.has(parentId)) {
      continue;
    }

    const beneath = childrenOf.get(parentId);
    if (beneath) {
      beneath.push(category);
    } else {
      childrenOf.set(parentId, [category]);
    }
  }

  const headings: CategoryHeading[] = [];
  const placed = new Set<number>();

  // The headings in the list's own order, so the shop's own ordering survives, each with everything beneath it.
  for (const category of categories) {
    const parentId = category.ParentCategoryID;
    if (parentId !== null && ids.has(parentId)) {
      continue;
    }

    placed.add(category.CategoryID);
    headings.push({ heading: category, rows: beneathOf(category, 0, childrenOf, placed) });
  }

  // What is left can only sit in a loop in the catalogue, which no heading leads into. It is listed on its own rather
  // than left out, and flat: the row it would step in under is in the same broken pair.
  const ungrouped = categories
    .filter(category => !placed.has(category.CategoryID))
    .map(category => ({ category, depth: 0 }));

  return { headings, ungrouped };
}

/**
 * Where a category sits in the catalogue, heading first: 'Decoratives / Vases'. It is the label a listing puts on a
 * product, so a shopper reads which collection a piece belongs to and not only the shelf it happens to be on.
 *
 * A category whose parent is not in the list is named on its own, and a loop in the catalogue ends the walk instead
 * of running it (the same guard as the grouping above).
 */
export function pathOf(category: Category, categories: Category[]): string {
  const byId = new Map<number, Category>(categories.map(row => [row.CategoryID, row]));
  const names: string[] = [];
  const walked = new Set<number>();
  let row: Category | undefined = category;

  while (row && !walked.has(row.CategoryID)) {
    walked.add(row.CategoryID);
    names.unshift(row.CategoryName);

    const parentId: number | null = row.ParentCategoryID;
    row = parentId === null ? undefined : byId.get(parentId);
  }

  return names.join(' / ');
}

/** Everything beneath a category, depth first, with the depth each row sits at. A row already placed is not stepped in again, which is what ends a loop. */
function beneathOf(
  category: Category,
  depth: number,
  childrenOf: Map<number, Category[]>,
  placed: Set<number>
): CategoryRow[] {
  const rows: CategoryRow[] = [];

  for (const child of childrenOf.get(category.CategoryID) ?? []) {
    if (placed.has(child.CategoryID)) {
      continue;
    }

    placed.add(child.CategoryID);
    rows.push({ category: child, depth });
    rows.push(...beneathOf(child, depth + 1, childrenOf, placed));
  }

  return rows;
}
