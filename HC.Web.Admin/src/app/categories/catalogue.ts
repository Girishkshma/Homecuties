import { AdminManagedCategory } from '../models/admin.model';

/**
 * How the Categories screen reads the catalogue it was handed.
 *
 * The server owns the tree: it answers the rows in display order, with how deep each one sits (see
 * AdminDashboardService.GetManagedCategoriesAsync and HC.Business.CategoryTree), and it owns every rule about what may
 * be written to it (HC.Business.CategoryCatalog). What is left for the screen is what it draws from those rows - the
 * path a shelf is named by, which rows are headings, which are the only rows that may become a category's parent, and
 * what stands in the way of taking one out - and that is here rather than in the component so it can be pinned by
 * tests, in the same way the storefront's own category tree is.
 *
 * The words are the shop's: a heading has nothing above it in the catalogue, and everything beneath one is read as a
 * shelf of it.
 */

/**
 * How long a category name may be: 'Categories.CategoryName' is varchar(20) in the shop's database, and the server
 * refuses a longer one with its own sentence (see HC.Business.CategoryCatalog.MaxNameLength). It is repeated here only
 * so the form can stop an over-long name being typed, not to decide anything: what the server says is what is shown.
 */
export const CATEGORY_NAME_MAX_LENGTH = 20;

/** One entry of the category form's 'sits under' list. */
export interface CategoryChoice {
  categoryId: number;
  /** The full path of the category, heading first: 'Decoratives / Vases'. */
  label: string;
}

/**
 * Where a category sits in the catalogue, heading first: 'Decoratives / Vases'. It is the label the product form puts
 * on a category, so the list of parents to choose from reads the way the rest of the shop does.
 *
 * A category whose parent is not in the list is named on its own - the same reading the server takes of a row whose
 * parent row is gone (see CategoryTree.TopsOfTree) - and a loop in the catalogue ends the walk instead of running it.
 */
export function categoryPath(category: AdminManagedCategory, categories: AdminManagedCategory[]): string {
  const byId = new Map<number, AdminManagedCategory>(categories.map(row => [row.categoryId, row]));
  const names: string[] = [];
  const walked = new Set<number>();
  let row: AdminManagedCategory | undefined = category;

  while (row && !walked.has(row.categoryId)) {
    walked.add(row.categoryId);
    names.unshift(row.categoryName);

    const parentId: number | null = row.parentCategoryId ?? null;
    row = parentId === null ? undefined : byId.get(parentId);
  }

  return names.join(' / ');
}

/**
 * What a row is, in the words the rest of the shop uses: a heading has nothing above it in the catalogue - its parent
 * is absent, or the row it names is not in the list at all - and every other row is a shelf of one.
 *
 * It is the server's own reading (see CategoryTree.TopsOfTree): a category whose parent row is gone is a heading here
 * too, because there is nothing left to file a product under, and the screen says so rather than showing a heading as
 * a shelf.
 */
export function categoryType(category: AdminManagedCategory, categories: AdminManagedCategory[]): 'Heading' | 'Shelf' {
  const ids = new Set<number>(categories.map(row => row.categoryId));
  const parentId: number | null = category.parentCategoryId ?? null;

  return parentId === null || !ids.has(parentId) ? 'Heading' : 'Shelf';
}

/**
 * What is broken about a row, or '' when nothing is - the two states a catalogue can be left in that the shop has to
 * be able to see in order to repair it, and both of them are drawn from the rows themselves rather than hidden:
 *
 * - a row naming a parent that is not in the catalogue: the storefront reads it as a heading of its own, so it is
 *   listed as one here too, with a note saying why the parent is not named;
 * - a row caught in a loop (two rows each naming the other as parent): no heading leads into it, so it and the products
 *   filed on it are listed under the catalogue rather than under a heading (see the storefront's own reading in
 *   HC.Business.CategoryBranch).
 */
export function catalogueNote(category: AdminManagedCategory, categories: AdminManagedCategory[]): string {
  const byId = new Map<number, AdminManagedCategory>(categories.map(row => [row.categoryId, row]));
  const walked = new Set<number>([category.categoryId]);
  let row: AdminManagedCategory | undefined = category;

  while (row?.parentCategoryId != null) {
    const parent = byId.get(row.parentCategoryId);

    if (!parent) {
      return 'The category it sits under is no longer in the catalogue, so it is read as a heading.';
    }

    if (walked.has(parent.categoryId)) {
      return 'Its parent links form a loop, so no heading leads into it: it is listed here rather than under a heading.';
    }

    walked.add(parent.categoryId);
    row = parent;
  }

  return '';
}

/**
 * A category and everything filed beneath it at any depth - the rows that cannot become its parent.
 *
 * Walking down rather than up is what makes a deep shelf count: moving a heading under a shelf of a shelf of itself is
 * the same mistake as moving it under its own child, and the whole branch would be left beyond the reach of the
 * catalogue either way. A row already walked is not walked again, so a loop in the table ends the walk.
 */
export function subtreeOf(categoryId: number, categories: AdminManagedCategory[]): Set<number> {
  const childrenOf = new Map<number, AdminManagedCategory[]>();

  for (const category of categories) {
    const parentId: number | null = category.parentCategoryId ?? null;
    if (parentId === null) {
      continue;
    }

    const beneath = childrenOf.get(parentId);
    if (beneath) {
      beneath.push(category);
    } else {
      childrenOf.set(parentId, [category]);
    }
  }

  const subtree = new Set<number>([categoryId]);
  const toWalk: number[] = [categoryId];

  while (toWalk.length > 0) {
    for (const child of childrenOf.get(toWalk.pop() as number) ?? []) {
      if (!subtree.has(child.categoryId)) {
        subtree.add(child.categoryId);
        toWalk.push(child.categoryId);
      }
    }
  }

  return subtree;
}

/**
 * What the 'sits under' list of the category form offers: every category of the catalogue, labelled by its path, leaving
 * out the row being edited and everything beneath it - a category cannot sit under itself or under one of its own
 * shelves, and the server refuses both (see its HC.Business.CategoryCatalog.ParentProblem).
 *
 * Adding a category offers the whole catalogue, headings included, because a shelf may sit under any category at any
 * depth. A row that shows as broken here is offered as a parent too: moving a shelf out of a loop, or under a heading
 * that has lost its own parent, is one of the things this screen is for.
 */
export function parentChoices(categories: AdminManagedCategory[], editingId: number | null): CategoryChoice[] {
  const excluded = editingId === null ? new Set<number>() : subtreeOf(editingId, categories);

  return categories
    .filter(category => !excluded.has(category.categoryId))
    .map(category => ({ categoryId: category.categoryId, label: categoryPath(category, categories) }));
}

/**
 * What would be left behind if this category were taken out, as the shop reads it - nothing at all on a row that can
 * go. The server refuses the delete for either of these (see its HC.Business.CategoryCatalog.DeleteProblem), so the
 * screen says what is in the way before the admin asks rather than showing them a refusal afterwards.
 */
export function deleteBlockers(category: AdminManagedCategory): string[] {
  const blockers: string[] = [];

  if (category.childCount === 1) {
    blockers.push('one category sits under it');
  } else if (category.childCount > 1) {
    blockers.push(`${category.childCount} categories sit under it`);
  }

  if (category.productCount === 1) {
    blockers.push('one product is filed on it');
  } else if (category.productCount > 1) {
    blockers.push(`${category.productCount} products are filed on it`);
  }

  return blockers;
}

/** The blockers above as one sentence, or '' when the category can be taken out. */
export function deleteBlockerSentence(category: AdminManagedCategory): string {
  const blockers = deleteBlockers(category);

  return blockers.length === 0
    ? ''
    : `The catalogue still holds this: ${blockers.join(' and ')}. Move them first.`;
}
