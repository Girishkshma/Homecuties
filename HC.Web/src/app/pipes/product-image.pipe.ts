import { Pipe, PipeTransform } from '@angular/core';
import { environment } from '../../environments/environment';

/**
 * Where a product's photographs are served from.
 *
 * Every photograph of every product - what an admin uploads and what the catalogue already has - lives in the API's
 * product image folder, and that is the folder the API writes an upload's sizes into (see ProductImageService in
 * HC.Business). So a stored file name ('I10040_S_01.jpg') is drawn from `environment.baseImageUrl`: the API's own
 * host in production, and the storefront's dev server locally, which serves the same folder while the shop is being
 * developed. One folder, one host serving it - a file written to a folder nobody serves is a photograph nobody sees.
 *
 * The artwork that belongs to the storefront itself - the category tiles under '/images/channapatna' - keeps its own
 * path: it ships with the storefront and has nothing to do with the shop's products.
 */
@Pipe({
  name: 'productImage',
  standalone: false
})
export class ProductImagePipe implements PipeTransform {

  private readonly baseImageUrl = environment.baseImageUrl;
  private readonly productsPath = 'products/';

  transform(value: string | null | undefined): string {
    if (!value) {
      return '';
    }

    // A path that already names a place on the storefront's own host: drawn from there, as it stands.
    if (value.startsWith('/images/') || value.startsWith('images/')) {
      return value;
    }

    // A photograph stored as a full URL - an older stored value - is read as the file it names and drawn from the
    // folder in use, like any other. Anything else that is a URL is not the shop's own picture and is left alone.
    if (value.startsWith('http')) {
      return value.includes('/images/')
        ? this.productPath(value.substring(value.lastIndexOf('/') + 1))
        : value;
    }

    return this.productPath(value);
  }

  /** The address of one photograph: '{images base}/products/{file}', however the stored value spelled it. */
  private productPath(fileName: string): string {
    const file = fileName.startsWith(this.productsPath) ? fileName.substring(this.productsPath.length) : fileName;

    return `${this.baseImageUrl}${this.productsPath}${file}`;
  }
}
