export interface Product {
  ProductID: number;
  ProductName: string;
  ProductTitle: string;
  ProductDescription: string;
  PromoImage: string;
  ProductImages: string[];
  SalesPrice: number;
  PreDiscountSalesPrice: number;
  PostDiscountSalesPrice: number;
  PostAdditionalDiscountSalesPrice: number;
  DiscountPercent: number;
  AdditionalDiscountPercent: number;
  CGSTPercent: number;
  SGSTPercent: number;
  IGSTPercent: number;
  IsInStock: boolean;
  AvailableQty: number;
  Features: ProductFeature[];
  Categories: Category[];
}

export interface ProductFeature {
  FeatureID: number;
  Feature: string;
  IsActive: boolean;
}

export interface Category {
  CategoryID: number;
  CategoryName: string;
  ParentCategoryID: number | null;
}

export interface CartItem {
  ProductID: number;
  Quantity: number;
}

export interface Cart {
  Items: CartItem[];
}

export interface CartCalculation {
  Count: number;
  SalesPrice: number;
  Discount: number;
  AddDiscount: number;
  GST: string;
  GSTCharge: number;
  SubTotal: number;
  GrandTotal: number;
}

export interface ApiResult {
  Result: number;
  Messages: string[];
}

export interface Customer {
  CustomerID: number;
  FirstName: string;
  MiddleName: string;
  LastName: string;
  IsGuest: boolean;
}

export interface HomeStats {
  ProductCount: number;
  CategoryCount: number;
  CustomerCount: number;
}
