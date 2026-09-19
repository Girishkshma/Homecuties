import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Product, Category, HomeStats } from '../models/product.model';
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
