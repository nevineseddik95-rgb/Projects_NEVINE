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
  imports: [KmfPipe, DatePipe, FormsModule, RouterLink, ImageFallbackDirective],
  template: `
    <header class="shop-head">
      <a class="brand" routerLink="/">Beauty & Shine</a>
      <nav class="shop-nav" aria-label="Shop">
        <a class="nav-link" routerLink="/favorites">Favorites <span class="nav-badge">{{ favs.count() }}</span></a>
        <a class="nav-link" routerLink="/cart">Cart <span class="nav-badge">{{ cart.count() }}</span></a>
      </nav>
    </header>

    <main class="shop">
      <section class="shop-hero">
        <div class="hero-copy">
          <p class="eyebrow">Small details. Your signature shine.</p>
          <h1>Find the piece that feels like you.</h1>
          <p>Explore bracelets, necklaces, keychains and more. Choose your favorites and reserve your order with a 40% deposit.</p>
          <div class="hero-actions">
            <a class="btn btn-primary" href="#collection">Explore the collection</a>
            <a class="btn btn-ghost" routerLink="/favorites">View favorites</a>
          </div>
        </div>
        <div class="hero-media">
          <img [src]="api.img(heroProduct?.imageUrl)" [alt]="heroProduct?.model || 'Beauty and Shine collection'" appImageFallback>
          <div class="hero-tag"><strong>40%</strong><span>deposit to reserve</span></div>
        </div>
      </section>

      <section class="trust-strip" aria-label="Shopping benefits">
        <div><strong>Pay with MVola</strong><span>Simple mobile payment</span></div>
        <div><strong>Keep your reference</strong><span>Easy order follow-up</span></div>
        <div><strong>Email updates</strong><span>When you add an email</span></div>
      </section>

      <section id="collection" class="collection">
        <div class="section-head">
          <div>
            <p class="eyebrow">The collection</p>
            <h2>Browse all pieces</h2>
          </div>
          <button class="btn btn-ghost filter-toggle" type="button" (click)="mobileFilters = !mobileFilters">
            Filters @if (activeFilterCount) { <span class="nav-badge">{{ activeFilterCount }}</span> }
          </button>
        </div>

        <section class="filters shop-filters" [class.mobile-open]="mobileFilters" aria-label="Filters">
          <div class="filter grow">
            <label for="q">Search</label>
            <input id="q" name="q" type="search" placeholder="Model or style" [(ngModel)]="search" (ngModelChange)="page = 1">
          </div>
          <div class="filter">
            <label for="sort">Sort by</label>
            <select id="sort" name="sort" [(ngModel)]="sort" (ngModelChange)="page = 1">
              <option value="new">Newest release first</option>
              <option value="name">Model, A to Z</option>
              <option value="priceAsc">Price, low to high</option>
              <option value="priceDesc">Price, high to low</option>
            </select>
          </div>
          <div class="filter">
            <span>Price (KMF)</span>
            <div class="pair">
              <input id="minPrice" name="minPrice" class="num" type="number" min="0" placeholder="Min"
                     aria-label="Minimum price" [(ngModel)]="minPrice" (ngModelChange)="page = 1">
              <span>to</span>
              <input id="maxPrice" name="maxPrice" class="num" type="number" min="0" placeholder="Max"
                     aria-label="Maximum price" [(ngModel)]="maxPrice" (ngModelChange)="page = 1">
            </div>
          </div>

          <div class="chip-group" role="group" aria-label="Genre">
            <span class="chip-label">Genre</span>
            @for (genre of genreOptions; track genre) {
              <button type="button" [class.on]="selectedGenre === genre"
                      (click)="selectGenre(genre === selectedGenre ? null : genre)">{{ genre }}</button>
            }
          </div>
          <div class="chip-group" role="group" aria-label="Color">
            <span class="chip-label">Color</span>
            @for (color of colorOptions; track color) {
              <button type="button" [class.on]="selectedColor === color"
                      (click)="selectColor(color === selectedColor ? null : color)">{{ color }}</button>
            }
          </div>
          <div class="chip-group" role="group" aria-label="Size">
            <span class="chip-label">Size</span>
            @for (size of sizeOptions; track size) {
              <button type="button" [class.on]="selectedSize === size"
                      (click)="selectSize(size === selectedSize ? null : size)">{{ size }}</button>
            }
          </div>
        </section>

        <div class="result-toolbar">
          <p class="muted">
            {{ visible.length }} {{ visible.length === 1 ? 'piece' : 'pieces' }}
            @if (hasFilters) { <span>· filtered from {{ products.length }}</span> }
          </p>
          <div class="toolbar-actions">
            @if (hasFilters) {
              <button class="btn btn-ghost" type="button" (click)="clearFilters()">Clear filters</button>
            }
            <button class="btn btn-ghost" type="button" [class.on]="onlyFavorites" (click)="toggleFavorites()">
              ♥ Favorites
            </button>
            <div class="view-switch" role="group" aria-label="Product layout">
              <button type="button" [class.on]="view === 'grid'" [attr.aria-pressed]="view === 'grid'"
                      aria-label="Grid view" (click)="changeView('grid')">▦</button>
              <button type="button" [class.on]="view === 'list'" [attr.aria-pressed]="view === 'list'"
                      aria-label="List view" (click)="changeView('list')">☷</button>
            </div>
          </div>
        </div>

        @if (failed) {
          <div class="state-card" role="alert">
            <h3>We could not load the collection</h3>
            <p class="muted">Check your connection and try again.</p>
            <button class="btn btn-primary" type="button" (click)="loadProducts()">Try again</button>
          </div>
        } @else if (loading) {
          <div class="grid skeleton-grid" aria-label="Loading products">
            @for (item of skeleton; track item) {
              <div class="skeleton-card"><div class="skeleton-image"></div><div class="skeleton-line"></div><div class="skeleton-line short"></div></div>
            }
          </div>
        } @else {
          <div class="grid" [class.list-view]="view === 'list'">
            @for (product of pagedProducts; track product.id) {
              <article class="product">
                <button type="button" class="card-img" (click)="open(product)" [attr.aria-label]="'View ' + product.model">
                  <img [src]="api.img(product.imageUrl)" [alt]="product.model" loading="lazy" appImageFallback>
                  @if (isNew(product)) { <span class="product-badge">New</span> }
                </button>
                <button type="button" class="heart" [class.on]="favs.has(product.id)" [attr.aria-pressed]="favs.has(product.id)"
                        [attr.aria-label]="(favs.has(product.id) ? 'Remove ' : 'Add ') + product.model + (favs.has(product.id) ? ' from favorites' : ' to favorites')"
                        (click)="favs.toggle(product.id)">♥</button>
                <div class="product-body">
                  <p class="product-kind">{{ product.genre }}</p>
                  <h3><button type="button" class="link" (click)="open(product)">{{ product.model }}</button></h3>
                  <p class="muted">{{ product.color }}@if (product.size) { · size {{ product.size }} }</p>
                  <p class="price">{{ product.price | kmf }}</p>
                  <div class="product-actions">
                    <button class="btn btn-primary" type="button" (click)="add(product)">Add to cart</button>
                    <button class="btn btn-ghost" type="button" (click)="open(product)">Details</button>
                  </div>
                </div>
              </article>
            } @empty {
              <div class="state-card empty-state">
                <h3>No pieces found</h3>
                @if (onlyFavorites && !favs.count()) {
                  <p class="muted">Tap the heart on a product to save your favorites here.</p>
                } @else {
                  <p class="muted">No products match these filters. Try a broader search.</p>
                }
                <button class="btn btn-ghost" type="button" (click)="clearFilters()">Clear filters</button>
              </div>
            }
          </div>

          @if (totalPages > 1) {
            <nav class="pagination" aria-label="Product pages">
              <button class="btn btn-ghost" type="button" [disabled]="page === 1" (click)="goToPage(page - 1)">Previous</button>
              <span>Page {{ page }} of {{ totalPages }}</span>
              <button class="btn btn-ghost" type="button" [disabled]="page === totalPages" (click)="goToPage(page + 1)">Next</button>
            </nav>
          }
        }
      </section>

      @if (recentlyViewed.length) {
        <section class="recent-section">
          <div class="section-head"><div><p class="eyebrow">Pick up where you left off</p><h2>Recently viewed</h2></div></div>
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
        <button type="button" class="btn btn-ghost modal-close" (click)="close()">Close</button>
        <div class="modal-image">
          <img [src]="api.img(selected.imageUrl)" [alt]="selected.model" appImageFallback>
          @if (isNew(selected)) { <span class="product-badge">New arrival</span> }
        </div>
        <div class="modal-body">
          <p class="eyebrow">{{ selected.genre }}</p>
          <h2>{{ selected.model }}</h2>
          <p class="price">{{ selected.price | kmf }}</p>
          <dl class="facts">
            <dt>Color</dt><dd>{{ selected.color }}</dd>
            @if (selected.size) { <dt>Size</dt><dd>{{ selected.size }}</dd> }
            <dt>Released</dt><dd>{{ selected.releaseDate | date: 'mediumDate' }}</dd>
          </dl>
          <div class="qty" role="group" aria-label="Quantity">
            <button type="button" aria-label="Decrease quantity" (click)="qty = qty > 1 ? qty - 1 : 1">−</button>
            <span aria-live="polite">{{ qty }}</span>
            <button type="button" aria-label="Increase quantity" [disabled]="qty >= cart.maxQuantity" (click)="qty = qty + 1">+</button>
          </div>
          <p>
            <button class="btn btn-primary" type="button" (click)="add(selected, qty); close()">Add to cart</button>
            <button class="btn btn-ghost" type="button" (click)="favs.toggle(selected.id)">
              {{ favs.has(selected.id) ? 'Remove from favorites' : 'Add to favorites' }}
            </button>
          </p>
          <button class="btn btn-ghost" type="button" (click)="share(selected)">Share this piece</button>
        </div>
      </div>
    }

    @if (toast) { <p class="toast" role="status">{{ toast }} <a routerLink="/cart">View cart</a></p> }
  `
})
export class ShopComponent implements OnInit, OnDestroy {
  api = inject(ApiService);
  cart = inject(CartService);
  favs = inject(FavoritesService);
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
  view: 'grid' | 'list' = this.readStorage('charm_shop_view') === 'list' ? 'list' : 'grid';
  page = 1;
  readonly pageSize = 9;
  readonly skeleton = [1, 2, 3, 4, 5, 6];

  selected: Product | null = null;
  qty = 1;
  toast = '';
  private recentIds: string[] = this.readRecent();
  private timer: any;

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
      this.hasValue(this.minPrice), this.hasValue(this.maxPrice), this.onlyFavorites
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
    this.say(`${product.model} added to the cart.`);
  }

  open(product: Product): void {
    this.selected = product;
    this.qty = 1;
    this.recentIds = [product.id, ...this.recentIds.filter(id => id !== product.id)].slice(0, 6);
    this.writeStorage('charm_recent_products', JSON.stringify(this.recentIds));
  }

  @HostListener('document:keydown.escape')
  close(): void {
    this.selected = null;
    this.mobileFilters = false;
  }

  async share(product: Product): Promise<void> {
    const url = new URL(window.location.origin + '/', window.location.origin);
    url.searchParams.set('product', product.id);
    try {
      if (navigator.share) {
        await navigator.share({ title: product.model, text: `Take a look at ${product.model}`, url: url.toString() });
      } else if (navigator.clipboard) {
        await navigator.clipboard.writeText(url.toString());
        this.say('Product link copied.');
      } else {
        this.say('Sharing is not available in this browser.');
      }
    } catch {
      this.say('Sharing was cancelled.');
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
