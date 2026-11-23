import { Injectable, computed, signal } from '@angular/core';

const KEY = 'charm_favorites';

@Injectable({ providedIn: 'root' })
export class FavoritesService {
  readonly ids = signal<string[]>(this.load());
  readonly count = computed(() => this.ids().length);

  has(id: string): boolean { return this.ids().includes(id); }

  toggle(id: string): void {
    this.set(this.has(id) ? this.ids().filter(x => x !== id) : [...this.ids(), id]);
  }

  /** forget favorites whose product no longer exists */
  retain(validIds: string[]): void {
    const valid = new Set(validIds);
    const next = this.ids().filter(id => valid.has(id));
    if (next.length !== this.ids().length) this.set(next);
  }

  private set(ids: string[]): void {
    this.ids.set(ids);
    try { localStorage.setItem(KEY, JSON.stringify(ids)); } catch { /* storage not available */ }
  }

  private load(): string[] {
    try {
      const raw = JSON.parse(localStorage.getItem(KEY) ?? '[]');
      return Array.isArray(raw) ? raw.filter(x => typeof x === 'string') : [];
    } catch { return []; }
  }
}