import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../core/api.service';
import { CartService } from '../core/cart.service';
import { FavoritesService } from '../core/favorites.service';
import { ImageFallbackDirective } from '../core/image-fallback.directive';
import { KmfPipe } from '../core/kmf.pipe';
import { LanguageService } from '../core/language.service';
import { LanguageSwitcherComponent } from '../core/language-switcher.component';
import { SeoService } from '../core/seo.service';
import { TranslatePipe } from '../core/translate.pipe';

interface Product {
  id: string;
  model: string;
  genre: string;
  size: string;
  color: string;
  price: number;
  releaseDate: string;
  imageUrl: string;
}

@Component({
  selector: 'app-product',
  standalone: true,
  imports: [RouterLink, ImageFallbackDirective, KmfPipe, LanguageSwitcherComponent, TranslatePipe],
  template: `
    <header class="shop-head">
      <a class="brand" routerLink="/">Beauty &amp; Shine</a>
      <nav class="shop-nav" [attr.aria-label]="'shop.allProducts' | translate">
        <a class="nav-link" routerLink="/favorites">{{ 'shop.favorites' | translate }} <span class="nav-badge">{{ favs.count() }}</span></a>
        <a class="nav-link" routerLink="/cart">{{ 'shop.cart' | translate }} <span class="nav-badge">{{ cart.count() }}</span></a>
        <app-language-switcher />
      </nav>
    </header>

    <main class="shop product-page">
      <nav class="breadcrumbs" [attr.aria-label]="'product.breadcrumb' | translate">
        <a routerLink="/">{{ 'product.home' | translate }}</a>
        <span aria-hidden="true">/</span>
        <span>{{ 'product.collection' | translate }}</span>
        @if (item) { <span aria-hidden="true">/</span><span>{{ item.model }}</span> }
      </nav>

      @if (loading) {
        <div class="state-card"><h2>{{ 'common.loading' | translate }}</h2></div>
      } @else if (!item) {
        <div class="state-card" role="alert">
          <h1>{{ 'product.notFoundTitle' | translate }}</h1>
          <p class="muted">{{ 'product.notFoundText' | translate }}</p>
          <a class="btn btn-primary" routerLink="/">{{ 'product.back' | translate }}</a>
        </div>
      } @else {
        <article class="product-detail">
          <div class="product-detail-media">
            <img [src]="api.img(item.imageUrl)" [alt]="item.model" appImageFallback>
          </div>
          <div class="product-detail-body">
            <p class="eyebrow">{{ item.genre }}</p>
            <h1>{{ item.model }}</h1>
            <p class="price product-detail-price">{{ item.price | kmf }}</p>
            <p class="product-description">{{ description() }}</p>
            <dl class="facts">
              <dt>{{ 'common.color' | translate }}</dt><dd>{{ item.color || '—' }}</dd>
              @if (item.size) { <dt>{{ 'common.size' | translate }}</dt><dd>{{ item.size }}</dd> }
              <dt>{{ 'shop.released' | translate }}</dt><dd>{{ formatDate(item.releaseDate) }}</dd>
            </dl>
            <div class="qty" role="group" [attr.aria-label]="'common.quantity' | translate">
              <button type="button" [attr.aria-label]="'common.decreaseQuantity' | translate" (click)="qty = qty > 1 ? qty - 1 : 1">−</button>
              <span aria-live="polite">{{ qty }}</span>
              <button type="button" [attr.aria-label]="'common.increaseQuantity' | translate" [disabled]="qty >= cart.maxQuantity" (click)="qty = qty + 1">+</button>
            </div>
            <div class="product-detail-actions">
              <button class="btn btn-primary" type="button" (click)="add()">{{ 'shop.addToCart' | translate }}</button>
              <button class="btn btn-ghost" type="button" (click)="favs.toggle(item.id)">
                {{ (favs.has(item.id) ? 'shop.removeFromFavorites' : 'shop.addToFavorites') | translate }}
              </button>
              <button class="btn btn-ghost" type="button" (click)="share()">{{ 'shop.sharePiece' | translate }}</button>
            </div>
            <div class="reserve-note">
              <strong>40%</strong>
              <div><span>{{ 'shop.depositToReserve' | translate }}</span><small>{{ 'product.reserveText' | translate }}</small></div>
            </div>
            <a class="btn btn-ghost" routerLink="/cart">{{ 'shop.viewCart' | translate }}</a>
          </div>
        </article>
      }

      @if (toast) { <p class="toast" role="status">{{ toast }} <a routerLink="/cart">{{ 'shop.viewCart' | translate }}</a></p> }
    </main>
  `
})
export class ProductComponent implements OnInit, OnDestroy {
  api = inject(ApiService);
  cart = inject(CartService);
  favs = inject(FavoritesService);
  language = inject(LanguageService);
  private route = inject(ActivatedRoute);
  private seo = inject(SeoService);
  private timer: any;

  item: Product | null = null;
  loading = true;
  qty = 1;
  toast = '';

  get locale(): string { return this.language.locale(); }
  t(key: string): string { return this.language.t(key); }

  ngOnInit(): void {
    this.load();
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }

  load(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.loading = false;
      this.seo.setNotFound();
      return;
    }
    this.loading = true;
    this.api.get<any>('products', id).pipe(finalize(() => (this.loading = false))).subscribe({
      next: row => {
        this.item = this.normalize(row);
        this.seo.setProduct(this.item);
      },
      error: () => {
        this.item = null;
        this.seo.setNotFound();
      }
    });
  }

  formatDate(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? '—' : new Intl.DateTimeFormat(this.locale, { dateStyle: 'medium', timeZone: 'UTC' }).format(date);
  }

  description(): string {
    if (!this.item) return '';
    const details = `${this.item.color}${this.item.size ? `${this.language.language() === 'fr' ? ', taille ' : ', size '}${this.item.size}` : ''}`;
    return this.language.language() === 'fr'
      ? `Découvrez ${this.item.model}, ${this.item.genre || 'un bijou'} Beauty & Shine. ${details}. Réservez avec 40 % d’acompte et payez par MVola.`
      : `Discover ${this.item.model}, a ${this.item.genre || 'jewelry'} piece from Beauty & Shine. ${details}. Reserve with a 40% deposit and pay by MVola.`;
  }

  add(): void {
    if (!this.item) return;
    this.cart.add({
      productId: this.item.id,
      model: this.item.model,
      price: this.item.price,
      imageUrl: this.item.imageUrl
    }, this.qty);
    this.say(this.language.t('shop.addedToCart', { model: this.item.model }));
  }

  async share(): Promise<void> {
    if (!this.item) return;
    const url = `${window.location.origin}/product/${this.item.id}`;
    try {
      if (navigator.share) {
        await navigator.share({ title: this.item.model, text: this.language.t('shop.shareText', { model: this.item.model }), url });
      } else if (navigator.clipboard) {
        await navigator.clipboard.writeText(url);
        this.say(this.t('shop.linkCopied'));
      } else {
        this.say(this.t('shop.shareUnavailable'));
      }
    } catch {
      this.say(this.t('shop.shareCancelled'));
    }
  }

  private normalize(row: any): Product {
    return {
      id: String(row.Id ?? row.id),
      model: row.Model ?? row.model ?? '',
      genre: row.Genre ?? row.genre ?? '',
      size: row.Size ?? row.size ?? '',
      color: row.Color ?? row.color ?? '',
      price: Number(row.Price ?? row.price ?? 0),
      releaseDate: row.ReleaseDate ?? row.releaseDate ?? '',
      imageUrl: row.ImageUrl ?? row.imageUrl ?? ''
    };
  }

  private say(message: string): void {
    this.toast = message;
    clearTimeout(this.timer);
    this.timer = setTimeout(() => (this.toast = ''), 3500);
  }
}
