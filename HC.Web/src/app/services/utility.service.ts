import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiConfigService } from './api.service';

@Injectable({
  providedIn: 'root'
})
export class UtilityService {
  constructor(private http: HttpClient, private config: ApiConfigService) { }

  addAccessTracking(data: any): Observable<any> {
    return this.http.post(this.config.getBaseServUrl() + 'Utilities/AddAccessTracking?rnd=' + Math.random(), JSON.stringify(data));
  }

  getIpAddress(): Observable<any> {
    return this.http.get('https://ipapi.co/json/');
  }

  static currencyFormat(n: number): string {
    return '₹ ' + Math.round(n) + '.00';
  }

  static sortByKeyAsc(array: any[], key: string): any[] {
    return array.sort((a, b) => {
      const x = a[key];
      const y = b[key];
      if (x < y) return -1;
      if (x > y) return 1;
      return 0;
    });
  }

  static sortByKeyDesc(array: any[], key: string): any[] {
    return array.sort((a, b) => {
      const x = a[key];
      const y = b[key];
      if (x > y) return -1;
      if (x < y) return 1;
      return 0;
    });
  }
}
