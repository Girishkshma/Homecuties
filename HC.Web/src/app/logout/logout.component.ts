import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { CartService } from '../services/cart.service';

@Component({
  selector: 'app-logout',
  templateUrl: './logout.component.html',
  standalone: false,
  styleUrl: './logout.component.scss'
})
export class LogoutComponent implements OnInit {
  constructor(
    private router: Router,
    private authService: AuthService,
    private cartService: CartService
  ) {}

  ngOnInit(): void {
    this.authService.clearSession();
    this.cartService.clearLocalCart();
    setTimeout(() => {
      this.router.navigate(['/']);
    }, 2000);
  }
}
