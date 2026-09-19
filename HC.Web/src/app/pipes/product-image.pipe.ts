import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'productImage',
  standalone: false
})
export class ProductImagePipe implements PipeTransform {

  private readonly baseImageUrl = 'https://homecuties.com/images/';
  private readonly localImagesPath = '/images/';

  transform(value: string | null | undefined): string {
    if (!value) {
      return '';
    }

    // If it's already a local path, return as-is
    if (value.startsWith('/images/') || value.startsWith('images/')) {
      return value;
    }

    // If it starts with the base image URL, map it to local images
    if (value.startsWith(this.baseImageUrl)) {
      return value.replace(this.baseImageUrl, this.localImagesPath);
    }

    // If it's a relative path like "products/I10002_P_01.JPG", map it
    if (value.startsWith('products/')) {
      return `${this.localImagesPath}${value}`;
    }

    // If it's just a filename, assume it's in the products folder
    if (!value.startsWith('http') && !value.startsWith('/')) {
      return `${this.localImagesPath}products/${value}`;
    }

    // Return as-is for any other URL (external images, etc.)
    return value;
  }
}
