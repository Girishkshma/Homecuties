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

/**
 * One entry in the signed-in customer's address book ('api/Customer/GetAddresses').
 * Checkout offers these as the address an order is shipped to, and - separately - as the address it
 * is billed to, so a customer can store several addresses instead of typing one per order.
 */
export interface CustomerAddress {
  AddressId: number;
  /** The customer's own label for it ("Home", "Office", ...). */
  AddressTitle: string;
  ContactName: string;
  AddressLine1: string;
  AddressLine2: string;
  City: string;
  State: string;
  Country: string;
  Zipcode: string;
  MobileNumber: string;
  EmailId: string | null;
}

export interface HomeStats {
  ProductCount: number;
  CategoryCount: number;
  CustomerCount: number;
}
