import { Injectable, computed, signal } from '@angular/core';

export interface CartLine {
  productId: string;
  model: string;
  price: number;
  imageUrl: string;
  quantity: number;
}

export interface CartProduct { id: string; model: string; price: number; imageUrl: string; }

/** Turns a product from the API into the few fields the cart needs (accepts "Model" or "model" keys). */
export function toCartProduct(p: any): CartProduct {
  return {
    id: String(p.Id ?? p.id),
    model: p.Model ?? p.model ?? '',
    price: Number(p.Price ?? p.price ?? 0),
    imageUrl: p.ImageUrl ?? p.imageUrl ?? ''
  };
}

const KEY = 'charm_cart';

@Injectable({ providedIn: 'root' })
export class CartService {
  readonly maxQuantity = 10;
  readonly lines = signal<CartLine[]>(this.load());
  readonly count = computed(() => this.lines().reduce((s, l) => s + l.quantity, 0));
  readonly total = computed(() => this.lines().reduce((s, l) => s + l.price * l.quantity, 0));

  add(item: Omit<CartLine, 'quantity'>, quantity = 1): void {
    const lines = [...this.lines()];
    const i = lines.findIndex(l => l.productId === item.productId);
    if (i >= 0) lines[i] = { ...lines[i], price: item.price, quantity: Math.min(this.maxQuantity, lines[i].quantity + quantity) };
    else lines.push({ ...item, quantity: Math.min(this.maxQuantity, quantity) });
    this.set(lines);
  }

  setQuantity(productId: string, quantity: number): void {
    const q = Math.max(0, Math.min(this.maxQuantity, Math.floor(quantity)));
    this.set(q === 0
      ? this.lines().filter(l => l.productId !== productId)
      : this.lines().map(l => (l.productId === productId ? { ...l, quantity: q } : l)));
  }

  remove(productId: string): void { this.set(this.lines().filter(l => l.productId !== productId)); }
  clear(): void { this.set([]); }

  /** Refreshes names and prices from the API, and drops products that no longer exist. */
  sync(products: CartProduct[]): void {
    const byId = new Map(products.map(p => [p.id.toLowerCase(), p]));
    this.set(this.lines().flatMap(l => {
      const p = byId.get(l.productId.toLowerCase());
      return p ? [{ ...l, model: p.model, price: p.price, imageUrl: p.imageUrl }] : [];
    }));
  }

  private set(lines: CartLine[]): void {
    this.lines.set(lines);
    try { localStorage.setItem(KEY, JSON.stringify(lines)); } catch { /* storage not available */ }
  }

  private load(): CartLine[] {
    try {
      const raw = JSON.parse(localStorage.getItem(KEY) ?? '[]');
      return Array.isArray(raw)
        ? raw.filter(l => l && typeof l.productId === 'string' && l.quantity > 0)
        : [];
    } catch { return []; }
  }
}