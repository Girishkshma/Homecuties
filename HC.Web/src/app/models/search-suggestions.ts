import { SearchSuggestions } from './product.model';

/**
 * The shortest search the header's box asks the shop about, matching the API (see ProductSearch.ShortestSuggestedQuery
 * in HC.Business): one letter names half the shop, so no list is drawn for it.
 */
export const SHORTEST_SUGGESTED_QUERY = 2;

/**
 * One row of the header's suggestion list: where a click - or Enter, once the arrows are on it - takes the shopper.
 *
 * The list is drawn as rows rather than as three lists so the keyboard can walk it as one thing (see
 * SideMenuComponent.onSearchKeydown), and each row carries what it is, so nothing has to be worked out while drawing.
 */
export interface SuggestionRow {
  kind: 'search' | 'category' | 'product';
  label: string;
  icon: string;
  hint: string;
  categoryId?: number;
  productId?: number;
}

/**
 * The rows the header's box draws under itself for what the shop answered (see ProductService.getSearchSuggestions):
 * the words themselves first, so a shopper can always go to the listing, then the collections the words name, then the
 * pieces whose names they appear in.
 *
 * The search row is there whether or not the shop found anything - 'nothing matched' is what the shop page says, and a
 * box that went quiet on a word it does not know would leave a shopper wondering whether the search happened at all.
 * Below SHORTEST_SUGGESTED_QUERY there are no rows: the shop is not asked about a letter, so there is nothing to offer
 * and nothing to say.
 */
export function rowsFor(text: string, suggestions: SearchSuggestions): SuggestionRow[] {
  if (text.length < SHORTEST_SUGGESTED_QUERY) {
    return [];
  }

  const rows: SuggestionRow[] = [{
    kind: 'search',
    label: `Search the shop for \u201C${text}\u201D`,
    icon: 'bi-search',
    hint: 'All results'
  }];

  for (const category of suggestions.Categories) {
    rows.push({
      kind: 'category',
      label: category.CategoryName,
      icon: 'bi-collection',
      hint: 'Collection',
      categoryId: category.CategoryID
    });
  }

  for (const product of suggestions.Products) {
    rows.push({
      kind: 'product',
      label: product.ProductTitle,
      icon: 'bi-box-seam',
      hint: 'Product',
      productId: product.ProductID
    });
  }

  return rows;
}