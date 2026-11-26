import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { CartService, toCartProduct } from '../core/cart.service';
import { KmfPipe } from '../core/kmf.pipe';
import { ImageFallbackDirective } from '../core/image-fallback.directive';

@Component({
  selector: 'app-cart',
  standalone: true,
  imports: [KmfPipe, RouterLink, ImageFallbackDirective],
  template: `
    <header class="shop-head">
      <a class="brand" routerLink="/">Beauty & Shine</a>
      <nav class="shop-nav"><a class="nav-link" routerLink="/">Back to products</a></nav>
    </header>

    <main class="cart-page">
      <h1>Your cart</h1>

      @if (cart.lines().length === 0) {
        <section>
          <p class="muted">Your cart is empty.</p>
          <p><a class="btn btn-primary" routerLink="/">Browse products</a></p>
        </section>
      } @else {
        <ul class="cart-lines">
          @for (l of cart.lines(); track l.productId) {
            <li class="cart-line">
              <img class="thumb" [src]="api.img(l.imageUrl)" alt="" appImageFallback>
              <div class="cart-info">
                <strong>{{ l.model }}</strong>
                <span class="muted">{{ l.price | kmf }} each</span>
              </div>
              <div class="qty" role="group" [attr.aria-label]="'Quantity of ' + l.model">
                <button type="button" aria-label="Decrease quantity" (click)="cart.setQuantity(l.productId, l.quantity - 1)">−</button>
                <span aria-live="polite">{{ l.quantity }}</span>
                <button type="button" aria-label="Increase quantity" [disabled]="l.quantity >= cart.maxQuantity" (click)="cart.setQuantity(l.productId, l.quantity + 1)">+</button>
              </div>
              <strong class="line-total">{{ l.price * l.quantity | kmf }}</strong>
              <button class="btn btn-danger" type="button" (click)="cart.remove(l.productId)">Remove</button>
            </li>
          }
        </ul>

        <aside class="summary" aria-label="Order summary">
          <div class="row"><span>Items</span><span>{{ cart.count() }}</span></div>
          <div class="row"><strong>Total</strong><strong>{{ cart.total() | kmf }}</strong></div>
          <div class="row"><span>Minimum deposit (40%)</span><span>{{ minDeposit | kmf }}</span></div>
          <p class="hint">You pay the deposit now and the balance later.</p>
          <p><a class="btn btn-primary wide" routerLink="/checkout">Go to checkout</a></p>
          <p><button class="btn btn-ghost wide" type="button" (click)="cart.clear()">Empty the cart</button></p>
        </aside>
      }
    </main>
  `
})
export class CartComponent implements OnInit {
  api = inject(ApiService);
  cart = inject(CartService);

  ngOnInit(): void {
    // keep names and prices up to date, and drop products that were removed from the shop
    this.api.list('products').subscribe({ next: rows => this.cart.sync(rows.map(toCartProduct)) });
  }

  get minDeposit(): number {
    return Math.ceil((Math.round(this.cart.total() * 100) * 40) / 10000);
  }
}