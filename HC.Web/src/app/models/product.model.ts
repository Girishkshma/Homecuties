export interface Product {
  ProductID: number;
  ProductName: string;
  ProductTitle: string;
  ProductDescription: string;
  PromoImage: string;
  ProductImages: string[];
  /**
   * What one unit costs the customer: the listing price, everything the product's 'Pricing & Charges' and 'Taxes'
   * sections say included - the unit price, the shop's margin, its packaging / storage / delivery charges, both
   * discounts and the tax on what is left. It is the figure the cart adds up, the checkout charges and the order is
   * written at, so it is the one a listing shows (see the API's ProductPricing).
   */
  ListingPrice: number;
  /** The same price with both discounts still on it - what a listing strikes through. */
  PreDiscountListingPrice: number;
  /**
   * The same price with only the first discount read - what one unit costs once it is off, the additional discount
   * still to come. It is the middle step of the walk the product page shows a shopper: the sell price, this, and
   * ListingPrice.
   */
  PostDiscountListingPrice: number;
  /** The shop's declared margin inside ListingPrice. */
  MarginAmount: number;
  /** The packaging, storage and delivery charges inside ListingPrice, added up. */
  ChargesAmount: number;
  /** The GST inside ListingPrice, and the value and rate it was charged on. */
  TaxableValue: number;
  GstRatePercent: number;
  GstAmount: number;
  /**
   * What the two discounts do to the goods ALONE: the unit price and what each of them leaves of it, the additional
   * discount coming off what the first one left. Kept for the screens that speak about the discount itself - the
   * price a customer pays (with the shop's margin, its own charges and the tax in it) is ListingPrice.
   */
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
