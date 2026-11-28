import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../core/api.service';
import { CartService } from '../core/cart.service';
import { FavoritesService } from '../core/favorites.service';
import { ImageFallbackDirective } from '../core/image-fallback.directive';
import { KmfPipe } from '../core/kmf.pipe';
import { LanguageService } from '../core/language.service';
import { LanguageSwitcherComponent } from '../core/language-switcher.component';
import { TranslatePipe } from '../core/translate.pipe';
import { TranslationParams } from '../core/translations';

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
  selector: 'app-shop',
  standalone: true,
  imports: [KmfPipe, DatePipe, FormsModule, RouterLink, ImageFallbackDirective, LanguageSwitcherComponent, TranslatePipe],
  template: `
    <header class="shop-head">
      <a class="brand" routerLink="/">Beauty & Shine</a>
      <nav class="shop-nav" [attr.aria-label]="'shop.allProducts' | translate">
        <a class="nav-link" routerLink="/favorites">{{ 'shop.favorites' | translate }} <span class="nav-badge">{{ favs.count() }}</span></a>
        <a class="nav-link" routerLink="/cart">{{ 'shop.cart' | translate }} <span class="nav-badge">{{ cart.count() }}</span></a>
        <app-language-switcher />
      </nav>
    </header>

    <main class="shop">
      <section class="shop-hero">
        <div class="hero-copy">
          <p class="eyebrow">{{ 'shop.heroEyebrow' | translate }}</p>
          <h1>{{ 'shop.heroTitle' | translate }}</h1>
          <p>{{ 'shop.heroText' | translate }}</p>
          <div class="hero-actions">
            <a class="btn btn-primary" href="#collection">{{ 'shop.explore' | translate }}</a>
            <a class="btn btn-ghost" routerLink="/favorites">{{ 'shop.viewFavorites' | translate }}</a>
          </div>
        </div>
        <div class="hero-media">
          <img [src]="api.img(heroProduct?.imageUrl)" [alt]="heroProduct?.model || ('shop.collectionAlt' | translate)" appImageFallback>
          <div class="hero-tag"><strong>40%</strong><span>{{ 'shop.depositToReserve' | translate }}</span></div>
        </div>
      </section>

      <section class="trust-strip" [attr.aria-label]="'shop.benefits' | translate">
        <div><strong>{{ 'shop.payMvola' | translate }}</strong><span>{{ 'shop.payMvolaText' | translate }}</span></div>
        <div><strong>{{ 'shop.keepReference' | translate }}</strong><span>{{ 'shop.keepReferenceText' | translate }}</span></div>
        <div><strong>{{ 'shop.emailUpdates' | translate }}</strong><span>{{ 'shop.emailUpdatesText' | translate }}</span></div>
      </section>

      <section id="collection" class="collection">
        <div class="section-head">
          <div>
            <p class="eyebrow">{{ 'shop.collectionEyebrow' | translate }}</p>
            <h2>{{ 'shop.browsePieces' | translate }}</h2>
          </div>
          <button class="btn btn-ghost filter-toggle" type="button" (click)="mobileFilters = !mobileFilters"
                  [attr.aria-expanded]="mobileFilters" aria-controls="shop-filters">
            {{ 'common.filters' | translate }} @if (activeFilterCount) { <span class="nav-badge">{{ activeFilterCount }}</span> }
          </button>
        </div>

        <section id="shop-filters" class="filters shop-filters" [class.mobile-open]="mobileFilters" [attr.aria-label]="'common.filters' | translate">
          <div class="filter grow">
            <label for="q">{{ 'shop.search' | translate }}</label>
            <input id="q" name="q" type="search" [placeholder]="'shop.searchPlaceholder' | translate" [(ngModel)]="search" (ngModelChange)="page = 1">
          </div>
          <div class="filter">
            <label for="sort">{{ 'shop.sortBy' | translate }}</label>
            <select id="sort" name="sort" [(ngModel)]="sort" (ngModelChange)="page = 1">
              <option value="new">{{ 'shop.sortNewest' | translate }}</option>
              <option value="name">{{ 'shop.sortName' | translate }}</option>
              <option value="priceAsc">{{ 'shop.sortPriceAsc' | translate }}</option>
              <option value="priceDesc">{{ 'shop.sortPriceDesc' | translate }}</option>
            </select>
          </div>
          <div class="filter">
            <span>{{ 'shop.priceKmf' | translate }}</span>
            <div class="pair">
              <input id="minPrice" name="minPrice" class="num" type="number" min="0" [placeholder]="'common.min' | translate"
                     [attr.aria-label]="'shop.minimumPrice' | translate" [(ngModel)]="minPrice" (ngModelChange)="page = 1">
              <span>{{ 'common.to' | translate }}</span>
              <input id="maxPrice" name="maxPrice" class="num" type="number" min="0" [placeholder]="'common.max' | translate"
                     [attr.aria-label]="'shop.maximumPrice' | translate" [(ngModel)]="maxPrice" (ngModelChange)="page = 1">
            </div>
          </div>

          <div class="chip-group" role="group" [attr.aria-label]="'common.genre' | translate">
            <span class="chip-label">{{ 'common.genre' | translate }}</span>
            @for (genre of genreOptions; track genre) {
              <button type="button" [class.on]="selectedGenre === genre"
                      (click)="selectGenre(genre === selectedGenre ? null : genre)">{{ genre }}</button>
            }
          </div>
          <div class="chip-group" role="group" [attr.aria-label]="'common.color' | translate">
            <span class="chip-label">{{ 'common.color' | translate }}</span>
            @for (color of colorOptions; track color) {
              <button type="button" [class.on]="selectedColor === color"
                      (click)="selectColor(color === selectedColor ? null : color)">{{ color }}</button>
            }
          </div>
          <div class="chip-group" role="group" [attr.aria-label]="'common.size' | translate">
            <span class="chip-label">{{ 'common.size' | translate }}</span>
            @for (size of sizeOptions; track size) {
              <button type="button" [class.on]="selectedSize === size"
                      (click)="selectSize(size === selectedSize ? null : size)">{{ size }}</button>
            }
          </div>
        </section>

        <div class="result-toolbar">
          <p class="muted">
            {{ visible.length }} {{ (visible.length === 1 ? 'shop.piece' : 'shop.pieces') | translate }}
            @if (hasFilters) { <span>· {{ 'shop.filteredFrom' | translate: { total: products.length } }}</span> }
          </p>
          <div class="toolbar-actions">
            @if (hasFilters) {
              <button class="btn btn-ghost" type="button" (click)="clearFilters()">{{ 'common.clearFilters' | translate }}</button>
            }
            <button class="btn btn-ghost" type="button" [class.on]="onlyFavorites" (click)="toggleFavorites()">
              ♥ {{ 'shop.favorites' | translate }}
            </button>
            <button class="btn btn-ghost" type="button" [class.on]="onlyNew" [attr.aria-pressed]="onlyNew" (click)="toggleNew()">
              {{ 'shop.newArrivals' | translate }}
            </button>
            <div class="view-switch" role="group" [attr.aria-label]="'shop.productLayout' | translate">
              <button type="button" [class.on]="view === 'grid'" [attr.aria-pressed]="view === 'grid'"
                      [attr.aria-label]="'shop.gridView' | translate" (click)="changeView('grid')">▦</button>
              <button type="button" [class.on]="view === 'list'" [attr.aria-pressed]="view === 'list'"
                      [attr.aria-label]="'shop.listView' | translate" (click)="changeView('list')">☷</button>
            </div>
          </div>
        </div>

        @if (failed) {
          <div class="state-card" role="alert">
            <h3>{{ 'shop.loadErrorTitle' | translate }}</h3>
            <p class="muted">{{ 'shop.loadErrorText' | translate }}</p>
            <button class="btn btn-primary" type="button" (click)="loadProducts()">{{ 'shop.tryAgain' | translate }}</button>
          </div>
        } @else if (loading) {
          <div class="grid skeleton-grid" [attr.aria-label]="'shop.loadingProducts' | translate">
            @for (item of skeleton; track item) {
              <div class="skeleton-card"><div class="skeleton-image"></div><div class="skeleton-line"></div><div class="skeleton-line short"></div></div>
            }
          </div>
        } @else {
          <div class="grid" [class.list-view]="view === 'list'">
            @for (product of pagedProducts; track product.id; let i = $index) {
              <article class="product animate-in" [style.animation-delay]="(i * 45) + 'ms'">
                <button type="button" class="card-img" (click)="open(product)" [attr.aria-label]="t('shop.viewProduct', { model: product.model })">
                  <img [src]="api.img(product.imageUrl)" [alt]="product.model" loading="lazy" appImageFallback>
                  @if (isNew(product)) { <span class="product-badge">{{ 'shop.new' | translate }}</span> }
                </button>
                <button type="button" class="heart" [class.on]="favs.has(product.id)" [attr.aria-pressed]="favs.has(product.id)"
                        [attr.aria-label]="t(favs.has(product.id) ? 'shop.removeFavorite' : 'shop.addFavorite', { model: product.model })"
                        (click)="favs.toggle(product.id)">♥</button>
                <div class="product-body">
                  <p class="product-kind">{{ product.genre }}</p>
                  <h3><a class="link" [routerLink]="['/product', product.id]">{{ product.model }}</a></h3>
                  <p class="muted">{{ product.color }}@if (product.size) { · {{ 'common.size' | translate }} {{ product.size }} }</p>
                  <p class="price">{{ product.price | kmf }}</p>
                  <div class="product-actions">
                    <button class="btn btn-primary" type="button" (click)="add(product)">{{ 'shop.addToCart' | translate }}</button>
                    <a class="btn btn-ghost" [routerLink]="['/product', product.id]">{{ 'shop.details' | translate }}</a>
                  </div>
                </div>
              </article>
            } @empty {
              <div class="state-card empty-state">
                <h3>{{ 'shop.noPieces' | translate }}</h3>
                @if (onlyFavorites && !favs.count()) {
                  <p class="muted">{{ 'shop.noFavoritesHint' | translate }}</p>
                } @else {
                  <p class="muted">{{ 'shop.noMatchHint' | translate }}</p>
                }
                <button class="btn btn-ghost" type="button" (click)="clearFilters()">{{ 'common.clearFilters' | translate }}</button>
              </div>
            }
          </div>

          @if (totalPages > 1) {
            <nav class="pagination" [attr.aria-label]="'shop.productPages' | translate">
              <button class="btn btn-ghost" type="button" [disabled]="page === 1" (click)="goToPage(page - 1)">{{ 'shop.previous' | translate }}</button>
              <span>{{ 'shop.pageOf' | translate: { page: page, total: totalPages } }}</span>
              <button class="btn btn-ghost" type="button" [disabled]="page === totalPages" (click)="goToPage(page + 1)">{{ 'shop.next' | translate }}</button>
            </nav>
          }
        }
      </section>

      @if (recentlyViewed.length) {
        <section class="recent-section">
          <div class="section-head">
            <div><p class="eyebrow">{{ 'shop.recentEyebrow' | translate }}</p><h2>{{ 'shop.recentTitle' | translate }}</h2></div>
            <button class="btn btn-ghost" type="button" (click)="clearRecent()">{{ 'shop.clearHistory' | translate }}</button>
          </div>
          <div class="recent-grid">
            @for (product of recentlyViewed; track product.id) {
              <button class="recent-card" type="button" (click)="open(product)">
                <img [src]="api.img(product.imageUrl)" [alt]="product.model" appImageFallback>
                <span><strong>{{ product.model }}</strong><small>{{ product.price | kmf }}</small></span>
              </button>
            }
          </div>
        </section>
      }
    </main>

    @if (selected) {
      <div class="backdrop" (click)="close()"></div>
      <div class="modal" role="dialog" aria-modal="true" [attr.aria-label]="selected.model">
        <button type="button" class="btn btn-ghost modal-close" (click)="close()">{{ 'common.close' | translate }}</button>
        <div class="modal-image">
          <img [src]="api.img(selected.imageUrl)" [alt]="selected.model" appImageFallback>
          @if (isNew(selected)) { <span class="product-badge">{{ 'shop.newArrival' | translate }}</span> }
        </div>
        <div class="modal-body">
          <p class="eyebrow">{{ selected.genre }}</p>
          <h2>{{ selected.model }}</h2>
          <p class="price">{{ selected.price | kmf }}</p>
          <dl class="facts">
            <dt>{{ 'common.color' | translate }}</dt><dd>{{ selected.color }}</dd>
            @if (selected.size) { <dt>{{ 'common.size' | translate }}</dt><dd>{{ selected.size }}</dd> }
            <dt>{{ 'shop.released' | translate }}</dt><dd>{{ selected.releaseDate | date: 'mediumDate' : locale }}</dd>
          </dl>
          <div class="qty" role="group" [attr.aria-label]="'common.quantity' | translate">
            <button type="button" [attr.aria-label]="'common.decreaseQuantity' | translate" (click)="qty = qty > 1 ? qty - 1 : 1">−</button>
            <span aria-live="polite">{{ qty }}</span>
            <button type="button" [attr.aria-label]="'common.increaseQuantity' | translate" [disabled]="qty >= cart.maxQuantity" (click)="qty = qty + 1">+</button>
          </div>
          <p>
            <button class="btn btn-primary" type="button" (click)="add(selected, qty); close()">{{ 'shop.addToCart' | translate }}</button>
            <button class="btn btn-ghost" type="button" (click)="favs.toggle(selected.id)">
              {{ (favs.has(selected.id) ? 'shop.removeFromFavorites' : 'shop.addToFavorites') | translate }}
            </button>
          </p>
          <button class="btn btn-ghost" type="button" (click)="share(selected)">{{ 'shop.sharePiece' | translate }}</button>
        </div>
      </div>
    }

    @if (toast) { <p class="toast" role="status">{{ toast }} <a routerLink="/cart">{{ 'shop.viewCart' | translate }}</a></p> }
  `
})
export class ShopComponent implements OnInit, OnDestroy {
  api = inject(ApiService);
  cart = inject(CartService);
  favs = inject(FavoritesService);
  language = inject(LanguageService);
  private route = inject(ActivatedRoute);

  products: Product[] = [];
  loading = true;
  failed = false;
  mobileFilters = false;

  search = '';
  sort = 'new';
  minPrice: number | null = null;
  maxPrice: number | null = null;
  selectedGenre: string | null = null;
  selectedColor: string | null = null;
  selectedSize: string | null = null;
  onlyFavorites = false;
  onlyNew = false;
  view: 'grid' | 'list' = this.readStorage('charm_shop_view') === 'list' ? 'list' : 'grid';
  page = 1;
  readonly pageSize = 9;
  readonly skeleton = [1, 2, 3, 4, 5, 6];

  selected: Product | null = null;
  qty = 1;
  toast = '';
  private recentIds: string[] = this.readRecent();
  private timer: any;
  private previousBodyOverflow = '';

  get locale(): string { return this.language.locale(); }
  t(key: string, params: TranslationParams = {}): string { return this.language.t(key, params); }

  ngOnInit(): void {
    this.loadProducts();
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }

  get heroProduct(): Product | undefined { return this.products[0]; }
  get genreOptions() { return this.unique(product => product.genre); }
  get colorOptions() { return this.unique(product => product.color); }
  get sizeOptions() { return this.unique(product => product.size); }

  get visible(): Product[] {
    const query = this.search.trim().toLowerCase();
    const filtered = this.products.filter(product =>
      (!query || product.model.toLowerCase().includes(query)) &&
      (this.selectedGenre === null || product.genre === this.selectedGenre) &&
      (this.selectedColor === null || product.color === this.selectedColor) &&
      (this.selectedSize === null || product.size === this.selectedSize) &&
      (!this.onlyFavorites || this.favs.has(product.id)) &&
      (!this.onlyNew || this.isNew(product)) &&
      (!this.hasValue(this.minPrice) || product.price >= Number(this.minPrice)) &&
      (!this.hasValue(this.maxPrice) || product.price <= Number(this.maxPrice)));

    switch (this.sort) {
      case 'name': return filtered.sort((a, b) => a.model.localeCompare(b.model));
      case 'priceAsc': return filtered.sort((a, b) => a.price - b.price);
      case 'priceDesc': return filtered.sort((a, b) => b.price - a.price);
      default: return filtered.sort((a, b) => String(b.releaseDate).localeCompare(String(a.releaseDate)));
    }
  }

  get pagedProducts(): Product[] {
    const totalPages = this.totalPages;
    if (this.page > totalPages) this.page = totalPages;
    const start = (this.page - 1) * this.pageSize;
    return this.visible.slice(start, start + this.pageSize);
  }

  get totalPages(): number { return Math.max(1, Math.ceil(this.visible.length / this.pageSize)); }

  get recentlyViewed(): Product[] {
    return this.recentIds
      .map(id => this.products.find(product => product.id === id))
      .filter((product): product is Product => !!product);
  }

  get activeFilterCount(): number {
    return [
      this.search.trim(), this.selectedGenre, this.selectedColor, this.selectedSize,
      this.hasValue(this.minPrice), this.hasValue(this.maxPrice), this.onlyFavorites, this.onlyNew
    ].filter(Boolean).length;
  }

  get hasFilters(): boolean {
    return this.activeFilterCount > 0 || this.sort !== 'new';
  }

  loadProducts(): void {
    this.loading = true;
    this.failed = false;
    this.api.list('products').pipe(
      finalize(() => (this.loading = false))
    ).subscribe({
      next: rows => {
        this.products = rows.map(row => this.normalize(row));
        this.favs.retain(this.products.map(product => product.id));
        const productId = this.route.snapshot.queryParamMap.get('product');
        if (productId) {
          const product = this.products.find(item => item.id === productId);
          if (product) this.open(product);
        }
      },
      error: () => (this.failed = true)
    });
  }

  selectGenre(value: string | null): void { this.selectedGenre = value; this.page = 1; }
  selectColor(value: string | null): void { this.selectedColor = value; this.page = 1; }
  selectSize(value: string | null): void { this.selectedSize = value; this.page = 1; }

  toggleFavorites(): void {
    this.onlyFavorites = !this.onlyFavorites;
    this.page = 1;
  }

  toggleNew(): void {
    this.onlyNew = !this.onlyNew;
    this.page = 1;
  }

  clearRecent(): void {
    this.recentIds = [];
    this.writeStorage('charm_recent_products', '[]');
  }

  changeView(view: 'grid' | 'list'): void {
    this.view = view;
    this.writeStorage('charm_shop_view', view);
  }

  clearFilters(): void {
    this.search = '';
    this.sort = 'new';
    this.minPrice = null;
    this.maxPrice = null;
    this.selectedGenre = null;
    this.selectedColor = null;
    this.selectedSize = null;
    this.onlyFavorites = false;
    this.onlyNew = false;
    this.page = 1;
  }

  goToPage(page: number): void {
    this.page = Math.max(1, Math.min(this.totalPages, page));
    document.getElementById('collection')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  isNew(product: Product): boolean {
    const released = Date.parse(product.releaseDate);
    const age = Date.now() - released;
    return !Number.isNaN(released) && age >= 0 && age < 90 * 24 * 60 * 60 * 1000;
  }

  add(product: Product, quantity = 1): void {
    this.cart.add({
      productId: product.id,
      model: product.model,
      price: product.price,
      imageUrl: product.imageUrl
    }, quantity);
    this.say(this.t('shop.addedToCart', { model: product.model }));
  }

  open(product: Product): void {
    if (!this.selected) {
      this.previousBodyOverflow = document.body.style.overflow;
      document.body.style.overflow = 'hidden';
    }
    this.selected = product;
    this.qty = 1;
    this.recentIds = [product.id, ...this.recentIds.filter(id => id !== product.id)].slice(0, 6);
    this.writeStorage('charm_recent_products', JSON.stringify(this.recentIds));
  }

  @HostListener('document:keydown.escape')
  close(): void {
    this.selected = null;
    this.mobileFilters = false;
    document.body.style.overflow = this.previousBodyOverflow;
  }

  async share(product: Product): Promise<void> {
    const url = new URL(`/product/${product.id}`, window.location.origin).toString();
    try {
      if (navigator.share) {
        await navigator.share({ title: product.model, text: this.t('shop.shareText', { model: product.model }), url: url.toString() });
      } else if (navigator.clipboard) {
        await navigator.clipboard.writeText(url.toString());
        this.say(this.t('shop.linkCopied'));
      } else {
        this.say(this.t('shop.shareUnavailable'));
      }
    } catch {
      this.say(this.t('shop.shareCancelled'));
    }
  }

  private normalize(product: any): Product {
    return {
      id: String(product.Id ?? product.id),
      model: product.Model ?? product.model ?? '',
      genre: product.Genre ?? product.genre ?? '',
      size: product.Size ?? product.size ?? '',
      color: product.Color ?? product.color ?? '',
      price: Number(product.Price ?? product.price ?? 0),
      releaseDate: product.ReleaseDate ?? product.releaseDate ?? '',
      imageUrl: product.ImageUrl ?? product.imageUrl ?? ''
    };
  }

  private unique(pick: (product: Product) => string): string[] {
    return [...new Set(this.products.map(pick).filter(Boolean))].sort();
  }

  private hasValue(value: unknown): boolean {
    return value !== null && value !== undefined && value !== '';
  }

  private say(message: string): void {
    this.toast = message;
    clearTimeout(this.timer);
    this.timer = setTimeout(() => (this.toast = ''), 3500);
  }

  private readStorage(key: string): string {
    try { return localStorage.getItem(key) ?? ''; } catch { return ''; }
  }

  private writeStorage(key: string, value: string): void {
    try { localStorage.setItem(key, value); } catch { /* storage unavailable */ }
  }

  private readRecent(): string[] {
    try {
      const value = JSON.parse(this.readStorage('charm_recent_products') || '[]');
      return Array.isArray(value) ? value.filter(id => typeof id === 'string').slice(0, 6) : [];
    } catch {
      return [];
    }
  }
}
