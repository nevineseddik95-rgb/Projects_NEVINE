import { Component, DestroyRef, HostListener, inject } from '@angular/core';
import { AsyncPipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Subject, catchError, debounceTime, filter, forkJoin, merge, of, switchMap, timer } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { RealtimeService } from '../core/realtime.service';

interface Note {
  key: string;
  kind: 'new' | 'payment';
  title: string;
  detail: string;
  ref: string;
}

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [AsyncPipe, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="admin">
      <nav class="sidebar" aria-label="Admin">
        <a class="brand" routerLink="/admin">Beauty & Shine</a>
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
          @if (realtime.state$ | async; as live) {
            <span class="live-state" [class.offline]="live !== 'online'">
              <i aria-hidden="true"></i>{{ live === 'online' ? 'Live updates' : 'Reconnecting…' }}
            </span>
          }
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
                     routerLink="/admin/orders" [queryParams]="{ q: n.ref }" (click)="openNote(n)">
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
  realtime = inject(RealtimeService);
  private api = inject(ApiService);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);
  private readonly key = `charm_last_seen_order_time:${this.auth.username}`;
  private readonly paymentKey = `charm_read_payments:${this.auth.username}`;

  notes: Note[] = [];
  total = 0;
  newOrders = 0;
  toCheck = 0;
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
      if (this.firstLoad && localStorage.getItem(this.key) === null) this.markSeen();
      this.rebuild(true);
    });

    this.realtime.changes$.pipe(
      filter(resources => resources.includes('*') || resources.includes('commands') || resources.includes('customers')),
      takeUntilDestroyed()
    ).subscribe(() => this.refresh$.next());

    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      takeUntilDestroyed()
    ).subscribe(event => {
      if ((event as NavigationEnd).url.startsWith('/admin/orders')) {
        this.markSeen();
        this.markPaymentsRead(this.orders.filter(o => o.paymentStatus === 'Payment submitted').map(o => String(o.id)));
      }
      this.rebuild(false);
    });
  }

  @HostListener('document:keydown.escape') closeMenu(): void { this.open = false; }

  openNote(note: Note): void {
    if (note.kind === 'payment') this.markPaymentsRead([note.key.replace('payment:', '')]);
    this.markSeen();
    this.rebuild(false);
    this.open = false;
  }

  markAllRead(): void {
    this.markSeen();
    this.markPaymentsRead(this.orders.filter(o => o.paymentStatus === 'Payment submitted').map(o => String(o.id)));
    this.rebuild(false);
    this.open = false;
  }

  private time(order: any): number {
    return Date.parse(order.createdAt ?? order.date ?? '') || 0;
  }

  private lastSeen(): number {
    try { return Number(localStorage.getItem(this.key)) || 0; } catch { return 0; }
  }

  private markSeen(): void {
    const newest = Math.max(0, ...this.orders.map(order => this.time(order)));
    try { localStorage.setItem(this.key, String(newest)); } catch { /* storage unavailable */ }
  }

  private readPayments(): Set<string> {
    try {
      const value = JSON.parse(localStorage.getItem(this.paymentKey) ?? '[]');
      return new Set(Array.isArray(value) ? value.map(String) : []);
    } catch {
      return new Set();
    }
  }

  private markPaymentsRead(ids: string[]): void {
    const read = this.readPayments();
    ids.forEach(id => read.add(id));
    try { localStorage.setItem(this.paymentKey, JSON.stringify([...read].slice(-500))); } catch { /* storage unavailable */ }
  }

  private rebuild(announce: boolean): void {
    const seen = this.lastSeen();
    const readPayments = this.readPayments();
    const all: Note[] = [];

    for (const order of [...this.orders].sort((a, b) => this.time(b) - this.time(a))) {
      const who = this.names.get(String(order.customerId)) ?? 'A customer';
      const detail = `${who} · ${order.referenceNumber} · ${order.model}`;
      if (order.paymentStatus === 'Payment submitted' && !readPayments.has(String(order.id))) {
        all.push({ key: `payment:${order.id}`, kind: 'payment', title: 'Payment to check', detail, ref: order.referenceNumber });
      } else if (this.time(order) > seen) {
        all.push({ key: `new:${order.id}`, kind: 'new', title: 'New order', detail, ref: order.referenceNumber });
      }
    }

    this.total = all.length;
    this.notes = all.slice(0, 10);
    this.toCheck = this.orders.filter(order => order.paymentStatus === 'Payment submitted').length;
    this.newOrders = this.orders.filter(order => this.time(order) > seen).length;

    if (announce) {
      const fresh = all.filter(note => !this.known.has(note.key));
      this.known = new Set(all.map(note => note.key));
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
