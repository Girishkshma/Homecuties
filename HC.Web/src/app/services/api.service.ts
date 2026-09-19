import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ApiConfigService {
  private baseServUrl = environment.baseServUrl;
  private baseImageUrl = environment.baseImageUrl;

  getBaseImageUrl(): string {
    return this.baseImageUrl;
  }

  getBaseServUrl(): string {
    return this.baseServUrl;
  }
}
