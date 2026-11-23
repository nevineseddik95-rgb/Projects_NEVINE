import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { CartService } from '../core/cart.service';
import { FavoritesService } from '../core/favorites.service';
import { KmfPipe } from '../core/kmf.pipe';

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
  imports: [KmfPipe, RouterLink],
  template: `
    <header class="shop-head">
      <a class="brand" routerLink="/">Beauty & Shine</a>
      <nav class="shop-nav" aria-label="Shop">
        <a class="nav-link" routerLink="/">All products</a>
        <a class="nav-link" routerLink="/cart">Cart <span class="nav-badge">{{ cart.count() }}</span></a>
      </nav>
    </header>

    <main class="shop">
      <h1>Your favorites</h1>

      @if (failed) { <p class="alert" role="alert">The products could not be loaded. Check that the API is running.</p> }

      @if (!loaded) {
        <p class="muted">Loading…</p>
      } @else if (items.length === 0) {
        <p class="muted">You have no favorites yet. Tap the heart on a product to save it here.</p>
        <p><a class="btn btn-primary" routerLink="/">Browse products</a></p>
      } @else {
        <div class="grid">
          @for (p of items; track p.id) {
            <article class="product">
              <img [src]="api.img(p.imageUrl)" [alt]="p.model" loading="lazy">
              <button type="button" class="heart on" aria-pressed="true"
                      [attr.aria-label]="'Remove ' + p.model + ' from favorites'" (click)="favs.toggle(p.id)">♥</button>
              <h2>{{ p.model }}</h2>
              <p class="muted">{{ p.genre }}, {{ p.color }}@if (p.size) {, size {{ p.size }} }</p>
              <p class="price">{{ p.price | kmf }}</p>
              <button class="btn btn-primary" type="button" (click)="add(p)">Add to cart</button>
              <button class="btn btn-ghost" type="button" (click)="favs.toggle(p.id)">Remove</button>
            </article>
          }
        </div>
      }
    </main>

    @if (toast) { <p class="toast" role="status">{{ toast }} <a routerLink="/cart">View cart</a></p> }
  `
})
export class FavoritesComponent implements OnInit {
  api = inject(ApiService);
  cart = inject(CartService);
  favs = inject(FavoritesService);

  products: Product[] = [];
  loaded = false;
  failed = false;
  toast = '';
  private timer: any;

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
        this.favs.retain(this.products.map(p => p.id));   // forget products that no longer exist
        this.loaded = true;
      },
      error: () => { this.failed = true; this.loaded = true; }
    });
  }

  /** only the products you marked, so removing one makes it disappear at once */
  get items(): Product[] {
    return this.products.filter(p => this.favs.has(p.id));
  }

  add(p: Product): void {
    this.cart.add({ productId: p.id, model: p.model, price: p.price, imageUrl: p.imageUrl });
    this.toast = `${p.model} added to the cart.`;
    clearTimeout(this.timer);
    this.timer = setTimeout(() => (this.toast = ''), 3500);
  }
}