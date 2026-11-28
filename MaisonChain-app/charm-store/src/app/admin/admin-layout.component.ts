import { Component, DestroyRef, ElementRef, HostListener, ViewChild, inject } from '@angular/core';
import { AsyncPipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Subject, catchError, debounceTime, filter, forkJoin, merge, of, switchMap, timer } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { LanguageService } from '../core/language.service';
import { LanguageSwitcherComponent } from '../core/language-switcher.component';
import { RealtimeService } from '../core/realtime.service';
import { TranslatePipe } from '../core/translate.pipe';
import { TranslationParams } from '../core/translations';

interface Note {
  key: string;
  kind: 'new' | 'payment';
  titleKey: string;
  detail: string;
  ref: string;
}

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [AsyncPipe, RouterOutlet, RouterLink, RouterLinkActive, LanguageSwitcherComponent, TranslatePipe],
  template: `
    <div class="admin">
      <nav id="admin-sidebar" #sidebar class="sidebar" [class.mobile-open]="menuOpen" [attr.aria-label]="'admin.navigation' | translate">
        <div class="sidebar-head">
          <a class="brand" routerLink="/admin" (click)="closeMobileMenu()">Beauty & Shine</a>
          <button class="sidebar-close" type="button" [attr.aria-label]="'admin.closeMenu' | translate" (click)="closeMobileMenu(true)">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">
              <path d="M6 6l12 12M18 6L6 18"/>
            </svg>
          </button>
        </div>
        <ul>
          @for (l of visibleLinks; track l.path) {
            <li>
              <a [routerLink]="l.path" routerLinkActive="active" (click)="closeMobileMenu()">
                <span>{{ t(l.label) }}</span>
                @if (l.path === '/admin/orders') {
                  @if (newOrders > 0) { <span class="pill pill-new" [title]="'admin.newOrdersTitle' | translate">{{ t('admin.newOrders', { count: newOrders }) }}</span> }
                  @if (toCheck > 0) { <span class="pill pill-check" [title]="'admin.toCheckTitle' | translate">{{ t('admin.toCheck', { count: toCheck }) }}</span> }
                }
              </a>
            </li>
          }
        </ul>
        <div class="sidebar-foot">
          <span class="who">{{ auth.username }} · {{ t(auth.isOwner() ? 'admin.owner' : 'admin.viewer') }}</span>
          <a routerLink="/" target="_blank" (click)="closeMobileMenu()">{{ 'admin.viewStorefront' | translate }}</a>
          <button class="btn btn-ghost" type="button" (click)="auth.logout(); closeMobileMenu()">{{ 'admin.signOut' | translate }}</button>
        </div>
      </nav>

      @if (menuOpen) {
        <button class="sidebar-backdrop" type="button" [attr.aria-label]="'admin.closeMenu' | translate" (click)="closeMobileMenu(true)"></button>
      }

      <main class="content">
        <div class="topbar">
          <button #menuToggle class="menu-toggle" type="button" aria-controls="admin-sidebar" [attr.aria-expanded]="menuOpen"
                  [attr.aria-label]="(menuOpen ? 'admin.closeMenu' : 'admin.openMenu') | translate" (click)="toggleMenu()">
            <svg width="21" height="21" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">
              <path d="M4 7h16M4 12h16M4 17h16"/>
            </svg>
          </button>
          <app-language-switcher />
          @if (realtime.state$ | async; as live) {
            <span class="live-state" [class.offline]="live !== 'online'">
              <i aria-hidden="true"></i>{{ (live === 'online' ? 'admin.live' : 'admin.reconnecting') | translate }}
            </span>
          }
          <div class="bell-wrap">
            <button type="button" class="bell" aria-haspopup="true" [attr.aria-expanded]="open"
                    [attr.aria-label]="'admin.notificationsAria' | translate: { total: total }" (click)="open = !open">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                   stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M18 8a6 6 0 0 0-12 0c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.7 21a2 2 0 0 1-3.4 0"/>
              </svg>
              @if (total) { <span class="bell-count">{{ total }}</span> }
            </button>

            @if (open) {
              <div class="notes" role="region" [attr.aria-label]="'admin.notifications' | translate">
                <div class="notes-head">
                  <strong>{{ 'admin.notifications' | translate }}</strong>
                  <button class="btn btn-ghost" type="button" (click)="markAllRead()">{{ 'admin.markAllRead' | translate }}</button>
                </div>
                @for (n of notes; track n.key) {
                  <a class="note" [class.note-pay]="n.kind === 'payment'"
                     routerLink="/admin/orders" [queryParams]="{ q: n.ref }" (click)="openNote(n)">
                    <strong>{{ t(n.titleKey) }}</strong>
                    <span>{{ n.detail }}</span>
                  </a>
                } @empty { <p class="muted notes-empty">{{ 'admin.nothingNew' | translate }}</p> }
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
  @ViewChild('sidebar') private sidebar?: ElementRef<HTMLElement>;
  @ViewChild('menuToggle') private menuToggle?: ElementRef<HTMLButtonElement>;

  auth = inject(AuthService);
  realtime = inject(RealtimeService);
  language = inject(LanguageService);
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
  menuOpen = false;
  toast = '';

  private orders: any[] = [];
  private names = new Map<string, string>();
  private known = new Set<string>();
  private firstLoad = true;
  private timer: any;
  private refresh$ = new Subject<void>();

  links = [
    { path: '/admin/dashboard', label: 'admin.dashboard', owner: false },
    { path: '/admin/orders', label: 'admin.orders', owner: false },
    { path: '/admin/commissions', label: 'admin.commissions', owner: false },
    { path: '/admin/products', label: 'admin.products', owner: false },
    { path: '/admin/customers', label: 'admin.customers', owner: false },
    { path: '/admin/genres', label: 'admin.genres', owner: false },
    { path: '/admin/colors', label: 'admin.colors', owner: false },
    { path: '/admin/sizes', label: 'admin.sizes', owner: false },
    { path: '/admin/admins', label: 'admin.admins', owner: true }
  ];

  t(key: string, params: TranslationParams = {}): string { return this.language.t(key, params); }
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
      this.menuOpen = false;
      if ((event as NavigationEnd).url.startsWith('/admin/orders')) {
        this.markSeen();
        this.markPaymentsRead(this.orders.filter(o => o.paymentStatus === 'Payment submitted').map(o => String(o.id)));
      }
      this.rebuild(false);
    });
  }

  toggleMenu(): void {
    this.open = false;
    this.menuOpen = !this.menuOpen;
    if (this.menuOpen) {
      setTimeout(() => this.sidebar?.nativeElement.querySelector<HTMLAnchorElement>('a')?.focus(), 0);
    } else {
      this.menuToggle?.nativeElement.focus();
    }
  }

  closeMobileMenu(restoreFocus = false): void {
    const wasOpen = this.menuOpen;
    this.menuOpen = false;
    if (restoreFocus && wasOpen)
      setTimeout(() => this.menuToggle?.nativeElement.focus(), 0);
  }

  @HostListener('document:keydown.escape') closeMenu(): void {
    this.open = false;
    if (this.menuOpen) this.closeMobileMenu(true);
  }

  @HostListener('window:resize') closeDesktopMenu(): void {
    if (window.innerWidth > 800) this.menuOpen = false;
  }

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
      return new Set<string>();
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
      const who = this.names.get(String(order.customerId)) ?? this.t('common.customerFallback');
      const detail = `${who} · ${order.referenceNumber} · ${order.model}`;
      if (order.paymentStatus === 'Payment submitted' && !readPayments.has(String(order.id))) {
        all.push({ key: `payment:${order.id}`, kind: 'payment', titleKey: 'admin.notificationPayment', detail, ref: order.referenceNumber });
      } else if (this.time(order) > seen) {
        all.push({ key: `new:${order.id}`, kind: 'new', titleKey: 'admin.notificationNew', detail, ref: order.referenceNumber });
      }
    }

    this.total = all.length;
    this.notes = all.slice(0, 10);
    this.toCheck = this.orders.filter(order => order.paymentStatus === 'Payment submitted').length;
    this.newOrders = this.orders.filter(order => this.time(order) > seen).length;

    if (announce) {
      const fresh = all.filter(note => !this.known.has(note.key));
      this.known = new Set(all.map(note => note.key));
      if (!this.firstLoad && fresh.length) this.say(`${this.t(fresh[0].titleKey)}: ${fresh[0].detail}`);
      this.firstLoad = false;
    }
  }

  private say(message: string): void {
    this.toast = message;
    clearTimeout(this.timer);
    this.timer = setTimeout(() => (this.toast = ''), 7000);
  }
}
