import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { CartService } from '../core/cart.service';
import { FavoritesService } from '../core/favorites.service';
import { KmfPipe } from '../core/kmf.pipe';
import { ImageFallbackDirective } from '../core/image-fallback.directive';
import { LanguageService } from '../core/language.service';
import { LanguageSwitcherComponent } from '../core/language-switcher.component';
import { TranslatePipe } from '../core/translate.pipe';
import { TranslationParams } from '../core/translations';

interface Product {
  id: string;
  model: string;
  genre: string;
  color: string;
  size: string;
  price: number;
  imageUrl: string;
}

@Component({
  selector: 'app-favorites',
  standalone: true,
  imports: [KmfPipe, RouterLink, ImageFallbackDirective, LanguageSwitcherComponent, TranslatePipe],
  template: `
    <header class="shop-head">
      <a class="brand" routerLink="/">Beauty & Shine</a>
      <nav class="shop-nav" [attr.aria-label]="'shop.allProducts' | translate">
        <a class="nav-link" routerLink="/">{{ 'shop.allProducts' | translate }}</a>
        <a class="nav-link" routerLink="/cart">{{ 'shop.cart' | translate }} <span class="nav-badge">{{ cart.count() }}</span></a>
        <app-language-switcher />
      </nav>
    </header>

    <main class="shop">
      <h1>{{ 'favorites.title' | translate }}</h1>

      @if (failed) { <p class="alert" role="alert">{{ 'favorites.loadFailed' | translate }}</p> }

      @if (!loaded) {
        <p class="muted">{{ 'common.loading' | translate }}</p>
      } @else if (items.length === 0) {
        <p class="muted">{{ 'favorites.empty' | translate }}</p>
        <p><a class="btn btn-primary" routerLink="/">{{ 'cart.browse' | translate }}</a></p>
      } @else {
        <div class="grid">
          @for (p of items; track p.id) {
            <article class="product">
              <a class="card-img" [routerLink]="['/product', p.id]" [attr.aria-label]="t('shop.viewProduct', { model: p.model })">
                <img [src]="api.img(p.imageUrl)" [alt]="p.model" loading="lazy" appImageFallback>
              </a>
              <button type="button" class="heart on" aria-pressed="true"
                      [attr.aria-label]="t('shop.removeFavorite', { model: p.model })" (click)="favs.toggle(p.id)">♥</button>
              <div class="product-body">
                <p class="product-kind">{{ p.genre }}</p>
                <h2><a [routerLink]="['/product', p.id]">{{ p.model }}</a></h2>
                <p class="muted">{{ p.color }}@if (p.size) { · {{ 'common.size' | translate }} {{ p.size }} }</p>
                <p class="price">{{ p.price | kmf }}</p>
                <div class="product-actions">
                  <button class="btn btn-primary" type="button" (click)="add(p)">{{ 'shop.addToCart' | translate }}</button>
                  <a class="btn btn-ghost" [routerLink]="['/product', p.id]">{{ 'shop.details' | translate }}</a>
                </div>
              </div>
            </article>
          }
        </div>
      }
    </main>

    @if (toast) { <p class="toast" role="status">{{ toast }} <a routerLink="/cart">{{ 'shop.viewCart' | translate }}</a></p> }
  `
})
export class FavoritesComponent implements OnInit {
  api = inject(ApiService);
  cart = inject(CartService);
  favs = inject(FavoritesService);
  language = inject(LanguageService);

  products: Product[] = [];
  loaded = false;
  failed = false;
  toast = '';
  private timer: any;

  t(key: string, params: TranslationParams = {}): string { return this.language.t(key, params); }

  ngOnInit(): void {
    this.api.list('products').subscribe({
      next: rows => {
        this.products = rows.map(r => ({
          id: String(r.Id ?? r.id),
          model: r.Model ?? r.model ?? '',
          genre: r.Genre ?? r.genre ?? '',
          color: r.Color ?? r.color ?? '',
          size: r.Size ?? r.size ?? '',
          price: Number(r.Price ?? r.price ?? 0),
          imageUrl: r.ImageUrl ?? r.imageUrl ?? ''
        }));
        this.favs.retain(this.products.map(p => p.id));
        this.loaded = true;
      },
      error: () => { this.failed = true; this.loaded = true; }
    });
  }

  get items(): Product[] {
    return this.products.filter(p => this.favs.has(p.id));
  }

  add(p: Product): void {
    this.cart.add({ productId: p.id, model: p.model, price: p.price, imageUrl: p.imageUrl });
    this.toast = this.t('shop.addedToCart', { model: p.model });
    clearTimeout(this.timer);
    this.timer = setTimeout(() => (this.toast = ''), 3500);
  }
}
