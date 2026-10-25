import {
  BRAND_NAME,
  DESCRIPTION_LIMIT,
  SITE_URL,
  absoluteUrl,
  metaDescription,
  pagePath,
  plainText,
  productImageUrls,
  productMetadata,
  productName,
  productStructuredData,
  titleWithBrand
} from './page-metadata';
import { Product } from '../models/product.model';
import { environment } from '../../environments/environment';

/**
 * A product as the API sends one, with the fields a test cares about filled in. The prices are the ones a listing
 * shows, so the structured data has something to write down.
 */
function product(fields: Partial<Product>): Product {
  return {
    ProductID: 10040,
    ProductName: 'I10040',
    ProductTitle: 'Channapatna Wooden Dancing Doll',
    ProductDescription: 'A hand-turned wooden doll, painted with natural colours.',
    PromoImage: 'I10040_S_01.jpg',
    Pictures: [
      { Large: 'I10040_L_01.jpg', Thumbnail: 'I10040_S_01.jpg' },
      { Large: 'I10040_L_02.jpg', Thumbnail: 'I10040_S_02.jpg' }
    ],
    ListingPrice: 1499,
    PreDiscountListingPrice: 1799,
    PostDiscountListingPrice: 1599,
    MarginAmount: 100,
    ChargesAmount: 50,
    TaxableValue: 1000,
    GstRatePercent: 18,
    GstAmount: 180,
    SalesPrice: 1200,
    PreDiscountSalesPrice: 1500,
    PostDiscountSalesPrice: 1300,
    PostAdditionalDiscountSalesPrice: 1250,
    DiscountPercent: 10,
    AdditionalDiscountPercent: 5,
    CGSTPercent: 9,
    SGSTPercent: 9,
    IGSTPercent: 0,
    IsInStock: true,
    AvailableQty: 4,
    Features: [{ FeatureID: 1, Feature: 'Hand-turned wood', IsActive: true }],
    Categories: [{ CategoryID: 3, CategoryName: 'Wooden Toys', ParentCategoryID: 1 }],
    ...fields
  };
}

/**
 * What ends up in the head of a page, kept out of the service that writes it there so it can be checked without a
 * browser: the title a tab shows, the description a result draws, the canonical address a crawler is given, and the
 * Product a product page declares.
 */
describe('pageMetadata', () => {
  describe('the title a tab shows', () => {
    it('should put the brand after the page', () => {
      expect(titleWithBrand('Shop Handcrafted Home Decor')).toBe(`Shop Handcrafted Home Decor | ${BRAND_NAME}`);
    });

    it('should not name the brand twice', () => {
      expect(titleWithBrand('Homecuties — Handcrafted Home Decor')).toBe('Homecuties — Handcrafted Home Decor');
      expect(titleWithBrand('homecuties sale')).toBe('homecuties sale');
    });

    it('should fall back to the brand alone when a page has no title of its own', () => {
      expect(titleWithBrand('')).toBe(BRAND_NAME);
      expect(titleWithBrand('   ')).toBe(BRAND_NAME);
    });

    it('should collapse the whitespace a title arrives with', () => {
      expect(titleWithBrand('  Painted   Pots ')).toBe(`Painted Pots | ${BRAND_NAME}`);
    });
  });

  describe('the path a page is canonicalised to', () => {
    it('should keep the path and drop what comes after it', () => {
      expect(pagePath('/shop/12')).toBe('/shop/12');
      expect(pagePath('/shop?q=wooden+doll')).toBe('/shop');
      expect(pagePath('/shop/12?categoryId=12#top')).toBe('/shop/12');
    });

    it('should write the root as one slash', () => {
      expect(pagePath('')).toBe('/');
      expect(pagePath('/')).toBe('/');
      expect(pagePath('//')).toBe('/');
    });

    it('should drop a trailing slash rather than canonicalise two addresses for one page', () => {
      expect(pagePath('/shop/')).toBe('/shop');
    });
  });

  describe('an address on the storefront', () => {
    it('should name the site for a path', () => {
      expect(absoluteUrl('/shop')).toBe(`${SITE_URL}/shop`);
      expect(absoluteUrl('/og-image.png')).toBe(`${SITE_URL}/og-image.png`);
    });

    it('should add the slash a path arrives without', () => {
      expect(absoluteUrl('shop')).toBe(`${SITE_URL}/shop`);
    });

    it('should leave an address that is already whole alone', () => {
      expect(absoluteUrl('https://example.com/a.png')).toBe('https://example.com/a.png');
    });
  });

  describe('the words a page is described with', () => {
    it('should take markup out and squash the whitespace', () => {
      expect(plainText('<p>Hand-turned <b>wooden</b>\n  doll</p>')).toBe('Hand-turned wooden doll');
      expect(plainText('Pots &amp; vases')).toBe('Pots & vases');
      expect(plainText(null)).toBe('');
    });

    it('should leave a description that already fits exactly as it is', () => {
      const description = 'Handcrafted pots and vases, made by hand.';

      expect(metaDescription(description)).toBe(description);
    });

    it('should cut a longer description to the length a result shows', () => {
      const description = metaDescription('word '.repeat(80));

      expect(description.length).toBeLessThanOrEqual(DESCRIPTION_LIMIT);
      expect(description.endsWith('word…')).toBeTrue();
    });

    it('should not leave a comma or a full stop in front of the ellipsis', () => {
      const description = metaDescription('Hand-turned pieces, '.repeat(20));

      expect(description).not.toContain(',…');
      expect(description).not.toContain('.…');
      expect(description.endsWith('…')).toBeTrue();
    });

    it('should still cut a description that has no words to cut at', () => {
      const description = metaDescription('x'.repeat(DESCRIPTION_LIMIT * 2));

      expect(description.length).toBeLessThanOrEqual(DESCRIPTION_LIMIT);
      expect(description.endsWith('…')).toBeTrue();
    });
  });

  describe('one product', () => {
    it('should be named by its title, and by its name when the title is blank', () => {
      expect(productName(product({}))).toBe('Channapatna Wooden Dancing Doll');
      expect(productName(product({ ProductTitle: '' }))).toBe('I10040');
    });

    it('should give every photograph an address on the image host', () => {
      const urls = productImageUrls(product({}));

      expect(urls.length).toBe(2);
      expect(urls[0]).toBe(`${environment.baseImageUrl}products/I10040_L_01.jpg`);
      expect(urls[0].startsWith('http')).toBeTrue();
    });

    it('should fall back to the promo image when there are no photographs', () => {
      const urls = productImageUrls(product({ Pictures: [] }));

      expect(urls).toEqual([`${environment.baseImageUrl}products/I10040_S_01.jpg`]);
    });

    it('should say nothing about an image the shop does not have', () => {
      expect(productImageUrls(product({ Pictures: [], PromoImage: '' }))).toEqual([]);
    });

    it('should describe its own page', () => {
      const page = productMetadata(product({}));

      expect(page.title).toBe('Channapatna Wooden Dancing Doll');
      expect(page.path).toBe('/product/10040');
      expect(page.type).toBe('product');
      expect(page.image).toBe(`${environment.baseImageUrl}products/I10040_L_01.jpg`);
      expect(page.description).toBe('A hand-turned wooden doll, painted with natural colours.');
    });

    it('should still describe its page when the shop wrote no description', () => {
      const page = productMetadata(product({ ProductDescription: '' }));

      expect(page.description).toContain('Channapatna Wooden Dancing Doll');
      expect(page.description).toContain(BRAND_NAME);
    });

    it('should declare what it is, what it costs and that it can be bought', () => {
      const data = productStructuredData(product({}));
      const offers = data['offers'] as Record<string, unknown>;

      expect(data['@type']).toBe('Product');
      expect(data['name']).toBe('Channapatna Wooden Dancing Doll');
      expect(data['url']).toBe(`${SITE_URL}/product/10040`);
      expect(data['category']).toBe('Wooden Toys');
      expect(offers['priceCurrency']).toBe('INR');
      expect(offers['price']).toBe('1499.00');
      expect(offers['availability']).toBe('https://schema.org/InStock');
    });

    it('should say a piece is out of stock rather than leaving it to be guessed', () => {
      const data = productStructuredData(product({ IsInStock: false }));
      const offers = data['offers'] as Record<string, unknown>;

      expect(offers['availability']).toBe('https://schema.org/OutOfStock');
    });

    it('should declare no offer for a piece the shop has priced at nothing', () => {
      const data = productStructuredData(product({ ListingPrice: 0 }));

      expect(data['offers']).toBeUndefined();
      expect(data['name']).toBe('Channapatna Wooden Dancing Doll');
    });
  });
});
