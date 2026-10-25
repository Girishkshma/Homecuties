import { Pipe, PipeTransform } from '@angular/core';
import { productImageUrl } from '../models/product-image';

/**
 * Draws a product's photograph from the folder the API keeps it in (see productImageUrl, which holds the rule and
 * says why it lives outside the pipe: the head needs the same address for a share card).
 */
@Pipe({
  name: 'productImage',
  standalone: false
})
export class ProductImagePipe implements PipeTransform {

  transform(value: string | null | undefined): string {
    return productImageUrl(value);
  }
}
