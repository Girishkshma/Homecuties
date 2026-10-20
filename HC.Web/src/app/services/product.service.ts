import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Product, Category, HomeStats, SearchSuggestions } from '../models/product.model';
import { ApiConfigService } from './api.service';

@Injectable({
  providedIn: 'root'
})
export class ProductService {
  constructor(private http: HttpClient, private config: ApiConfigService) { }

  getProductsForHomepage(): Observable<Product[]> {
    return this.http.get<Product[]>(this.config.getBaseServUrl() + 'products/GetProductsForHomepage');
  }

  /** All active (non-suspended) products - used by the Shop when no category is selected. */
  getActiveProducts(): Observable<Product[]> {
    return this.http.get<Product[]>(this.config.getBaseServUrl() + 'products/GetActiveProducts');
  }

  getProduct(productId: string): Observable<Product> {
    return this.http.get<Product>(this.config.getBaseServUrl() + 'Products/GetProduct/' + productId);
  }

  getProductsByCategory(categoryId: number): Observable<Product[]> {
    return this.http.get<Product[]>(this.config.getBaseServUrl() + 'products/GetProductsByCategory/' + categoryId);
  }

  /**
   * The products matching what a shopper typed in the header's search box. The words are read by the API (see
   * ProductSearch in HC.Business): every one of them has to appear in the product's title, its name, its description or
   * the name of a shelf it is filed on, so 'wooden vase' and 'vase wooden' answer with the same pieces.
   */
  searchProducts(query: string): Observable<Product[]> {
    const params = new HttpParams().set('query', query);
    return this.http.get<Product[]>(this.config.getBaseServUrl() + 'products/SearchProducts', { params });
  }

  /**
   * What the header's search box offers while a shopper is still typing: the collections the words name and the pieces
   * whose own names they appear in (see the API's GetSearchSuggestionsAsync). It is asked for on a pause in typing
   * rather than on every key (see SideMenuComponent), which is why its answer is a handful of names and nothing more.
   */
  getSearchSuggestions(query: string): Observable<SearchSuggestions> {
    const params = new HttpParams().set('query', query);
    return this.http.get<SearchSuggestions>(this.config.getBaseServUrl() + 'products/SearchSuggestions', { params });
  }

  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(this.config.getBaseServUrl() + 'products/GetCategories');
  }

  /** Live counts for the home page hero: products, customers and categories. */
  getHomeStats(): Observable<HomeStats> {
    return this.http.get<HomeStats>(this.config.getBaseServUrl() + 'products/GetHomeStats');
  }

  getCategory(categoryId: number): Observable<Category> {
    return this.http.get<Category>(this.config.getBaseServUrl() + 'Products/GetCategory/' + categoryId);
  }
}
