import { Component, HostListener, OnInit, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { CartService } from '../core/cart.service';
import { FavoritesService } from '../core/favorites.service';
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
  imports: [KmfPipe, DatePipe, FormsModule, RouterLink],
  template: `
    <header class="shop-head">
      <a class="brand" routerLink="/">Beauty & Shine</a>
      <nav class="shop-nav" aria-label="Shop">
        <a class="nav-link" routerLink="/favorites">Favorites <span class="nav-badge">{{ favs.count() }}</span></a>
        <a class="nav-link" routerLink="/cart">Cart <span class="nav-badge">{{ cart.count() }}</span></a>
        <a class="nav-link" routerLink="/admin">Admin</a>
      </nav>
    </header>

    <main class="shop">
      <h1>Bracelets, necklaces ,keychains and MORE!</h1>

      <section class="filters shop-filters" aria-label="Filters">
        <div class="filter grow">
          <label for="q">Search</label>
          <input id="q" name="q" type="search" placeholder="Model" [(ngModel)]="q">
        </div>
        <div class="filter">
          <label for="sort">Sort by</label>
          <select id="sort" name="sort" [(ngModel)]="sort">
            <option value="new">Newest release first</option>
            <option value="name">Model, A to Z</option>
            <option value="priceAsc">Price, low to high</option>
            <option value="priceDesc">Price, high to low</option>
          </select>
        </div>
        <div class="filter">
          <span>Price (KMF)</span>
          <div class="pair">
            <input id="minPrice" name="minPrice" class="num" type="number" min="0" placeholder="Min" aria-label="Minimum price" [(ngModel)]="minPrice">
            <span>to</span>
            <input id="maxPrice" name="maxPrice" class="num" type="number" min="0" placeholder="Max" aria-label="Maximum price" [(ngModel)]="maxPrice">
          </div>
        </div>

        <div class="chip-group" role="group" aria-label="Genre">
          <span class="chip-label">Genre</span>
          @for (g of genreOptions; track g) {
            <button type="button" [class.on]="genre === g" (click)="genre = genre === g ? null : g">{{ g }}</button>
          }
        </div>
        <div class="chip-group" role="group" aria-label="Color">
          <span class="chip-label">Color</span>
          @for (c of colorOptions; track c) {
            <button type="button" [class.on]="color === c" (click)="color = color === c ? null : c">{{ c }}</button>
          }
        </div>
        <div class="chip-group" role="group" aria-label="Size">
          <span class="chip-label">Size</span>
          @for (s of sizeOptions; track s) {
            <button type="button" [class.on]="size === s" (click)="size = size === s ? null : s">{{ s }}</button>
          }
        </div>
      </section>

      <p class="muted result-line">
        {{ visible.length }} of {{ products.length }} products
        @if (hasFilters) { <button class="btn btn-ghost" type="button" (click)="clear()">Clear filters</button> }
      </p>

      @if (failed) { <p class="alert" role="alert">The products could not be loaded. Check that the API is running.</p> }

      <div class="grid">
        @for (p of visible; track p.id) {
          <article class="product">
            <button type="button" class="card-img" (click)="open(p)" [attr.aria-label]="'View ' + p.model">
              <img [src]="api.img(p.imageUrl)" [alt]="p.model" loading="lazy">
            </button>
            <button type="button" class="heart" [class.on]="favs.has(p.id)" [attr.aria-pressed]="favs.has(p.id)"
                    [attr.aria-label]="(favs.has(p.id) ? 'Remove ' : 'Add ') + p.model + (favs.has(p.id) ? ' from favorites' : ' to favorites')"
                    (click)="favs.toggle(p.id)">♥</button>
            <h2><button type="button" class="link" (click)="open(p)">{{ p.model }}</button></h2>
            <p class="muted">{{ p.genre }}, {{ p.color }}@if (p.size) {, size {{ p.size }} }</p>
            <p class="price">{{ p.price | kmf }}</p>
            <button class="btn btn-primary" type="button" (click)="add(p)">Add to cart</button>
          </article>
        } @empty {
          <p class="muted">
            @if (onlyFavs && !favs.count()) { You have no favorites yet. Tap the heart on a product to save it. }
            @else { No products match these filters. }
          </p>
        }
      </div>
    </main>

    @if (selected) {
      <div class="backdrop" (click)="close()"></div>
      <div class="modal" role="dialog" aria-modal="true" [attr.aria-label]="selected.model">
        <button type="button" class="btn btn-ghost modal-close" (click)="close()">Close</button>
        <img [src]="api.img(selected.imageUrl)" [alt]="selected.model">
        <div class="modal-body">
          <h2>{{ selected.model }}</h2>
          <p class="price">{{ selected.price | kmf }}</p>
          <dl class="facts">
            <dt>Genre</dt><dd>{{ selected.genre }}</dd>
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
        </div>
      </div>
    }

    @if (toast) { <p class="toast" role="status">{{ toast }} <a routerLink="/cart">View cart</a></p> }
  `
})
export class ShopComponent implements OnInit {
  api = inject(ApiService);
  cart = inject(CartService);
  favs = inject(FavoritesService);

  products: Product[] = [];
  failed = false;

  q = '';
  sort = 'new';
  minPrice: number | null = null;
  maxPrice: number | null = null;
  genre: string | null = null;
  color: string | null = null;
  size: string | null = null;
  onlyFavs = false;

  selected: Product | null = null;
  qty = 1;
  toast = '';
  private timer: any;

  ngOnInit(): void {
    this.api.list('products').subscribe({
      next: rows => (this.products = rows.map(r => this.normalize(r))),
      error: () => (this.failed = true)
    });
  }

  /** Accepts both "Model" and "model" style keys from the API. */
  private normalize(p: any): Product {
    return {
      id: String(p.Id ?? p.id),
      model: p.Model ?? p.model ?? '',
      genre: p.Genre ?? p.genre ?? '',
      size: p.Size ?? p.size ?? '',
      color: p.Color ?? p.color ?? '',
      price: Number(p.Price ?? p.price ?? 0),
      releaseDate: p.ReleaseDate ?? p.releaseDate ?? '',
      imageUrl: p.ImageUrl ?? p.imageUrl ?? ''
    };
  }

  add(p: Product, quantity = 1): void {
    this.cart.add({ productId: p.id, model: p.model, price: p.price, imageUrl: p.imageUrl }, quantity);
    this.say(`${p.model} added to the cart.`);
  }

  private say(message: string): void {
    this.toast = message;
    clearTimeout(this.timer);
    this.timer = setTimeout(() => (this.toast = ''), 3500);
  }

  open(p: Product): void { this.selected = p; this.qty = 1; }
  @HostListener('document:keydown.escape') close(): void { this.selected = null; }

  private uniq(pick: (p: Product) => string): string[] {
    return [...new Set(this.products.map(pick).filter(Boolean))].sort();
  }
  get genreOptions() { return this.uniq(p => p.genre); }
  get colorOptions() { return this.uniq(p => p.color); }
  get sizeOptions()  { return this.uniq(p => p.size); }

  /** true when a filter box holds a real value (an empty box does not count) */
  private has(v: unknown): boolean { return v !== null && v !== undefined && v !== ''; }

  get visible(): Product[] {
    const q = this.q.trim().toLowerCase();
    const out = this.products.filter(p =>
      (!q || p.model.toLowerCase().includes(q)) &&
      (this.genre === null || p.genre === this.genre) &&
      (this.color === null || p.color === this.color) &&
      (this.size === null || p.size === this.size) &&
      (!this.onlyFavs || this.favs.has(p.id)) &&
      (!this.has(this.minPrice) || p.price >= Number(this.minPrice)) &&
      (!this.has(this.maxPrice) || p.price <= Number(this.maxPrice)));

    switch (this.sort) {
      case 'name':      return out.sort((a, b) => a.model.localeCompare(b.model));
      case 'priceAsc':  return out.sort((a, b) => a.price - b.price);
      case 'priceDesc': return out.sort((a, b) => b.price - a.price);
      default:          return out.sort((a, b) => String(b.releaseDate).localeCompare(String(a.releaseDate)));
    }
  }

  get hasFilters(): boolean {
    return !!this.q.trim() || this.sort !== 'new' || this.has(this.minPrice) || this.has(this.maxPrice) ||
      this.genre !== null || this.color !== null || this.size !== null || this.onlyFavs;
  }

  clear(): void {
    this.q = ''; this.sort = 'new'; this.minPrice = null; this.maxPrice = null;
    this.genre = null; this.color = null; this.size = null; this.onlyFavs = false;
  }
}