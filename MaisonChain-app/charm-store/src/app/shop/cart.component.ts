import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { CartService, toCartProduct } from '../core/cart.service';
import { KmfPipe } from '../core/kmf.pipe';
import { ImageFallbackDirective } from '../core/image-fallback.directive';
import { LanguageSwitcherComponent } from '../core/language-switcher.component';
import { TranslatePipe } from '../core/translate.pipe';

@Component({
  selector: 'app-cart',
  standalone: true,
  imports: [KmfPipe, RouterLink, ImageFallbackDirective, LanguageSwitcherComponent, TranslatePipe],
  template: `
    <header class="shop-head">
      <a class="brand" routerLink="/">Beauty & Shine</a>
      <nav class="shop-nav">
        <a class="nav-link" routerLink="/">{{ 'shop.backToProducts' | translate }}</a>
        <app-language-switcher />
      </nav>
    </header>

    <main class="cart-page">
      <h1>{{ 'cart.title' | translate }}</h1>

      @if (cart.lines().length === 0) {
        <section>
          <p class="muted">{{ 'cart.empty' | translate }}</p>
          <p><a class="btn btn-primary" routerLink="/">{{ 'cart.browse' | translate }}</a></p>
        </section>
      } @else {
        <ul class="cart-lines">
          @for (l of cart.lines(); track l.productId) {
            <li class="cart-line">
              <img class="thumb" [src]="api.img(l.imageUrl)" alt="" appImageFallback>
              <div class="cart-info">
                <strong>{{ l.model }}</strong>
                <span class="muted">{{ l.price | kmf }} {{ 'common.each' | translate }}</span>
              </div>
              <div class="qty" role="group" [attr.aria-label]="'cart.quantityOf' | translate: { model: l.model }">
                <button type="button" [attr.aria-label]="'common.decreaseQuantity' | translate" (click)="cart.setQuantity(l.productId, l.quantity - 1)">−</button>
                <span aria-live="polite">{{ l.quantity }}</span>
                <button type="button" [attr.aria-label]="'common.increaseQuantity' | translate" [disabled]="l.quantity >= cart.maxQuantity" (click)="cart.setQuantity(l.productId, l.quantity + 1)">+</button>
              </div>
              <strong class="line-total">{{ l.price * l.quantity | kmf }}</strong>
              <button class="btn btn-danger" type="button" (click)="cart.remove(l.productId)">{{ 'cart.remove' | translate }}</button>
            </li>
          }
        </ul>

        <aside class="summary" [attr.aria-label]="'common.orderSummary' | translate">
          <div class="row"><span>{{ 'cart.items' | translate }}</span><span>{{ cart.count() }}</span></div>
          <div class="row"><strong>{{ 'common.total' | translate }}</strong><strong>{{ cart.total() | kmf }}</strong></div>
          <div class="row"><span>{{ 'common.minimumDeposit' | translate }}</span><span>{{ minDeposit | kmf }}</span></div>
          <p class="hint">{{ 'cart.depositHint' | translate }}</p>
          <p><a class="btn btn-primary wide" routerLink="/checkout">{{ 'cart.checkout' | translate }}</a></p>
          <p><button class="btn btn-ghost wide" type="button" (click)="cart.clear()">{{ 'cart.emptyCart' | translate }}</button></p>
        </aside>
      }
    </main>
  `
})
export class CartComponent implements OnInit {
  api = inject(ApiService);
  cart = inject(CartService);

  ngOnInit(): void {
    this.api.list('products').subscribe({ next: rows => this.cart.sync(rows.map(toCartProduct)) });
  }

  get minDeposit(): number {
    return Math.ceil((Math.round(this.cart.total() * 100) * 40) / 10000);
  }
}
