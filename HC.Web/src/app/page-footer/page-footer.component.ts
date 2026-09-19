import { Component } from '@angular/core';

@Component({
  selector: 'app-page-footer',
  templateUrl: './page-footer.component.html',
  standalone: false,
  styleUrl: './page-footer.component.scss'
})
export class PageFooterComponent {
  currentYear = new Date().getFullYear();
}
