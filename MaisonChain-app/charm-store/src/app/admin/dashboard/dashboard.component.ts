import { DestroyRef, Component, OnInit, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime } from 'rxjs';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { ImageFallbackDirective } from '../../core/image-fallback.directive';
import { KmfPipe } from '../../core/kmf.pipe';
import { LanguageService } from '../../core/language.service';
import { ORDER_STATUSES } from '../../core/models';
import { RealtimeService } from '../../core/realtime.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { TranslationParams } from '../../core/translations';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [KmfPipe, DatePipe, RouterLink, ImageFallbackDirective, TranslatePipe],
  template: `
    <header class="page-head"><div><h1>{{ 'admin.dashboard' | translate }}</h1><p class="muted">{{ 'dashboard.subtitle' | translate }}</p></div></header>
    @if (errors.length) {
      <p class="alert" role="alert">{{ t('dashboard.loadFailed', { errors: errors.join(', ') }) }}</p>
    }

    <div class="stats">
      <div class="stat stat-main"><span class="stat-num">{{ revenue | kmf }}</span><span>{{ 'dashboard.revenue' | translate }}</span></div>
      <div class="stat"><span class="stat-num">{{ commands.length }}</span><span>{{ 'dashboard.ordersTotal' | translate }}</span></div>
      <div class="stat"><span class="stat-num">{{ paymentCount('Payment submitted') }}</span><span>{{ 'dashboard.paymentsToCheck' | translate }}</span></div>
      <div class="stat"><span class="stat-num">{{ products.length }}</span><span>{{ 'dashboard.products' | translate }}</span></div>
      <div class="stat"><span class="stat-num">{{ customers.length }}</span><span>{{ 'dashboard.customers' | translate }}</span></div>
    </div>

    <div class="dash-grid">
      <section class="card">
        <h2>{{ 'dashboard.ordersByStatus' | translate }}</h2>
        @for (s of statuses; track s) {
          <div class="bar-row">
            <span>{{ t('status.' + s) }}</span>
            <div class="bar"><i [style.width.%]="commands.length ? count(s) / commands.length * 100 : 0"></i></div>
            <span class="bar-n">{{ count(s) }}</span>
          </div>
        }
      </section>

      <section class="card">
        <h2>{{ 'dashboard.recentVisitors' | translate }}</h2>
        @for (c of recentVisitors; track c.id) {
          <p class="line"><span>{{ c.fullName }}</span><span class="muted">{{ c.lastVisit | date: 'medium' : locale }}</span></p>
        } @empty { <p class="muted">{{ 'dashboard.noVisits' | translate }}</p> }
      </section>
    </div>

    <section class="card">
      <div class="card-head"><h2>{{ 'dashboard.latestOrders' | translate }}</h2><a routerLink="/admin/orders">{{ 'dashboard.seeAllOrders' | translate }}</a></div>
      <div class="table-wrap flat">
        <table>
          <thead><tr>
            <th>{{ 'common.image' | translate }}</th><th>{{ 'common.reference' | translate }}</th><th>{{ 'common.customer' | translate }}</th>
            <th>{{ 'common.items' | translate }}</th><th>{{ 'common.date' | translate }}</th><th>{{ 'common.status' | translate }}</th>
            <th>{{ 'common.payment' | translate }}</th><th>{{ 'common.total' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (o of latest; track o.id) {
              <tr>
                <td><img class="thumb" [src]="api.img(o.imageUrl)" alt="" appImageFallback></td>
                <td>{{ o.referenceNumber }}</td>
                <td>{{ customerName(o.customerId) }}</td>
                <td>{{ o.model }}</td>
                <td>{{ o.date | date: 'mediumDate' : locale }}</td>
                <td><span class="badge" [class]="'badge s-' + o.status">{{ t('status.' + o.status) }}</span></td>
                <td><span class="badge" [class]="'badge s-' + ('' + o.paymentStatus).split(' ')[0]">{{ t('payment.' + o.paymentStatus) }}</span></td>
                <td>{{ o.total | kmf }}</td>
              </tr>
            } @empty { <tr><td class="empty" colspan="8">{{ 'dashboard.noOrders' | translate }}</td></tr> }
          </tbody>
        </table>
      </div>
    </section>
  `
})
export class DashboardComponent implements OnInit {
  api = inject(ApiService);
  language = inject(LanguageService);
  statuses = ORDER_STATUSES;
  commands: any[] = [];
  products: any[] = [];
  customers: any[] = [];
  errors: string[] = [];

  private realtime = inject(RealtimeService);
  private destroyRef = inject(DestroyRef);

  get locale(): string { return this.language.locale(); }
  t(key: string, params: TranslationParams = {}): string { return this.language.t(key, params); }

  ngOnInit(): void {
    this.reload();
    this.realtime.changes$.pipe(debounceTime(250), takeUntilDestroyed(this.destroyRef)).subscribe(() => this.reload());
  }

  private reload(): void {
    this.errors = [];
    this.load('commands', d => (this.commands = d));
    this.load('products', d => (this.products = d));
    this.load('customers', d => (this.customers = d));
  }

  private load(endpoint: string, set: (d: any[]) => void): void {
    this.api.list(endpoint).subscribe({
      next: d => set(Array.isArray(d) ? d : []),
      error: e => this.errors.push(`${endpoint} (status ${e.status})`)
    });
  }

  count(status: string) { return this.commands.filter(c => c.status === status).length; }
  paymentCount(status: string) { return this.commands.filter(c => c.paymentStatus === status).length; }
  get revenue() { return this.commands.filter(c => c.status !== 'Cancelled').reduce((s, c) => s + (+c.total || 0), 0); }
  get latest() {
    return [...this.commands]
      .sort((a, b) => (Date.parse(b.createdAt ?? b.date) || 0) - (Date.parse(a.createdAt ?? a.date) || 0))
      .slice(0, 6);
  }
  get recentVisitors() {
    return this.customers.filter(c => c.lastVisit)
      .sort((a, b) => String(b.lastVisit).localeCompare(String(a.lastVisit))).slice(0, 6);
  }
  customerName(id: string) { return this.customers.find(c => c.id === id)?.fullName ?? this.t('dashboard.unknownCustomer'); }
}
