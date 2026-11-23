import { Component, DestroyRef, HostListener, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Subject, catchError, debounceTime, filter, forkJoin, merge, of, switchMap, timer } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { RealtimeService } from '../core/realtime.service';

interface Note { key: string; kind: 'new' | 'payment'; title: string; detail: string; ref: string; }

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="admin">
      <nav class="sidebar" aria-label="Admin">
        <a class="brand" routerLink="/admin">Charm Store</a>
        <ul>
          @for (l of visibleLinks; track l.path) {
            <li>
              <a [routerLink]="l.path" routerLinkActive="active">
                <span>{{ l.label }}</span>
                @if (l.path === '/admin/orders') {
                  @if (newOrders > 0) { <span class="pill pill-new" title="New orders">{{ newOrders }} new</span> }
                  @if (toCheck > 0) { <span class="pill pill-check" title="Payments to check">{{ toCheck }} to check</span> }
                }
              </a>
            </li>
          }
        </ul>
        <div class="sidebar-foot">
          <span class="who">{{ auth.username }} · {{ auth.isOwner() ? 'owner' : 'view only' }}</span>
          <a routerLink="/" target="_blank">View storefront</a>
          <button class="btn btn-ghost" type="button" (click)="auth.logout()">Sign out</button>
        </div>
      </nav>

      <main class="content">
        <div class="topbar">
          <div class="bell-wrap">
            <button type="button" class="bell" aria-haspopup="true" [attr.aria-expanded]="open"
                    [attr.aria-label]="'Notifications, ' + total + ' waiting'" (click)="open = !open">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                   stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M18 8a6 6 0 0 0-12 0c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.7 21a2 2 0 0 1-3.4 0"/>
              </svg>
              @if (total) { <span class="bell-count">{{ total }}</span> }
            </button>

            @if (open) {
              <div class="notes" role="region" aria-label="Notifications">
                <div class="notes-head">
                  <strong>Notifications</strong>
                  <button class="btn btn-ghost" type="button" (click)="markAllRead()">Mark all as read</button>
                </div>
                @for (n of notes; track n.key) {
                  <a class="note" [class.note-pay]="n.kind === 'payment'"
                     routerLink="/admin/orders" [queryParams]="{ q: n.ref }" (click)="openNote()">
                    <strong>{{ n.title }}</strong>
                    <span>{{ n.detail }}</span>
                  </a>
                } @empty { <p class="muted notes-empty">Nothing new.</p> }
              </div>
            }
          </div>
        </div>

        <router-outlet />
      </main>
    </div>

    @if (toast) { <p class="toast" role="status">{{ toast }}</p> }
  `
})
export class AdminLayoutComponent {
  auth = inject(AuthService);
  private api = inject(ApiService);
  private router = inject(Router);
  private realtime = inject(RealtimeService);
  private destroyRef = inject(DestroyRef);
  private readonly key = 'charm_last_seen_order_time';

  notes: Note[] = [];       // the latest ten
  total = 0;                // everything waiting, shown on the bell
  newOrders = 0;            // orders you have not read yet
  toCheck = 0;              // customers who say they paid, waiting for your check
  open = false;
  toast = '';

  private orders: any[] = [];
  private names = new Map<string, string>();
  private known = new Set<string>();
  private firstLoad = true;
  private timer: any;
  private refresh$ = new Subject<void>();

  links = [
    { path: '/admin/dashboard', label: 'Dashboard', owner: false },
    { path: '/admin/orders', label: 'Orders', owner: false },
    { path: '/admin/products', label: 'Products', owner: false },
    { path: '/admin/customers', label: 'Customers', owner: false },
    { path: '/admin/genres', label: 'Genres', owner: false },
    { path: '/admin/colors', label: 'Colors', owner: false },
    { path: '/admin/sizes', label: 'Sizes', owner: false },
    { path: '/admin/admins', label: 'Admins', owner: true }
  ];

  get visibleLinks() { return this.links.filter(l => !l.owner || this.auth.isOwner()); }

  constructor() {
    this.realtime.start();
    this.destroyRef.onDestroy(() => this.realtime.stop());

    // a message from the server refreshes at once; a check every minute is the safety net
    merge(timer(0, 60000), this.refresh$.pipe(debounceTime(200))).pipe(
      switchMap(() => forkJoin({
        orders: this.api.list('commands').pipe(catchError(() => of(null))),
        customers: this.api.list('customers').pipe(catchError(() => of([] as any[])))
      })),
      takeUntilDestroyed()
    ).subscribe(({ orders, customers }) => {
      if (!orders) return;
      this.orders = orders;
      this.names = new Map(customers.map((c: any) => [String(c.id), c.fullName]));
      // the very first time on this browser, older orders are not "new"
      if (this.firstLoad && localStorage.getItem(this.key) === null) this.markSeen();
      this.rebuild(true);
    });

    this.realtime.changes$.pipe(
      filter(r => r.includes('*') || r.includes('commands') || r.includes('customers')),
      takeUntilDestroyed()
    ).subscribe(() => this.refresh$.next());

    // opening the Orders page from the menu counts as reading the orders
    this.router.events.pipe(filter(e => e instanceof NavigationEnd), takeUntilDestroyed()).subscribe(() => {
      this.rebuild(false);
    });
  }

  @HostListener('document:keydown.escape') closeMenu(): void { this.open = false; }

  openNote(): void {
    this.markSeen();
    this.rebuild(false);
    this.open = false;
  }

  markAllRead(): void { this.openNote(); }

  /** the time an order was placed, in milliseconds (0 when the API does not send createdAt) */
  private time(o: any): number { return Date.parse(o.createdAt) || 0; }

  private lastSeen(): number {
    try { return Number(localStorage.getItem(this.key)) || 0; } catch { return 0; }
  }

  private markSeen(): void {
    const newest = Math.max(0, ...this.orders.map(o => this.time(o)));
    try { localStorage.setItem(this.key, String(newest)); } catch { /* storage not available */ }
  }

  private rebuild(announce: boolean): void {
    // clicking the Orders link in the menu marks the orders as read
    const seen = this.lastSeen();

    const all: Note[] = [];
    for (const o of [...this.orders].sort((a, b) => this.time(b) - this.time(a))) {
      const who = this.names.get(String(o.customerId)) ?? 'A customer';
      const detail = `${who} · ${o.referenceNumber} · ${o.model}`;
      if (o.paymentStatus === 'Payment submitted')
        all.push({ key: `payment:${o.id}`, kind: 'payment', title: 'Payment to check', detail, ref: o.referenceNumber });
      else if (this.time(o) > seen)
        all.push({ key: `new:${o.id}`, kind: 'new', title: 'New order', detail, ref: o.referenceNumber });
    }

    this.total = all.length;
    this.notes = all.slice(0, 10);
    this.toCheck = all.filter(n => n.kind === 'payment').length;
    this.newOrders = this.orders.filter(o => this.time(o) > seen).length;

    if (announce) {
      const fresh = all.filter(n => !this.known.has(n.key));
      this.known = new Set(all.map(n => n.key));
      if (!this.firstLoad && fresh.length) this.say(`${fresh[0].title}: ${fresh[0].detail}`);
      this.firstLoad = false;
    }
  }

  private say(message: string): void {
    this.toast = message;
    clearTimeout(this.timer);
    this.timer = setTimeout(() => (this.toast = ''), 7000);
  }
}