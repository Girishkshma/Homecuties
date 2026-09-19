import { Component, OnInit, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { UtilityService } from './services/utility.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  standalone: false,
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  constructor(
    private utilityService: UtilityService,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {}

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.trackAccess();
    }
  }

  private trackAccess(): void {
    this.utilityService.getIpAddress().subscribe({
      next: (ipData) => {
        const trackingData = {
          IPAddress: ipData.ip,
          Country: ipData.country_name,
          Region: ipData.region,
          City: ipData.city,
          Latitude: ipData.latitude,
          Longitude: ipData.longitude,
          UserAgent: navigator.userAgent,
          PageUrl: window.location.href,
          ReferrerUrl: document.referrer || null
        };
        this.utilityService.addAccessTracking(trackingData).subscribe();
      },
      error: () => {
        // Silently fail if IP tracking is unavailable
      }
    });
  }
}
