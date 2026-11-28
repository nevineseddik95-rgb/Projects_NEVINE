import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { debounceTime, Subject, takeUntil } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { LanguageService } from '../core/language.service';
import { RealtimeService } from '../core/realtime.service';
import { TranslatePipe } from '../core/translate.pipe';
import { TranslationParams } from '../core/translations';

interface CommissionRow {
  orderId: string;
  referenceNumber: string;
  adminId: string | null;
  adminName: string;
  adminPhone: string | null;
  customerName: string | null;
  orderDate: string;
  createdAt: string;
  status: string;
  paymentStatus: string;
  orderTotal: number;
  commissionAmount: number;
  paidAmount: number;
  outstandingAmount: number;
  payoutStatus: 'Unpaid' | 'PartiallyPaid' | 'Paid' | 'Excluded';
  paidAt: string | null;
  paymentNote: string | null;
  eligible: boolean;
}

@Component({
  selector: 'app-commissions',
  standalone: true,
  imports: [FormsModule, RouterLink, TranslatePipe],
  template: `
    <header class="page-head">
      <div>
        <h1>{{ 'admin.commissions' | translate }}</h1>
        <p class="muted">{{ 'commissions.subtitle' | translate }}</p>
        @if (!auth.isOwner()) { <p class="hint">{{ 'commissions.ownOnly' | translate }}</p> }
      </div>
      <div class="head-actions">
        <input class="search" type="search" name="search" [placeholder]="'common.search' | translate" [(ngModel)]="search">
        <button class="btn btn-ghost" type="button" (click)="load()">{{ 'common.refresh' | translate }}</button>
        <button class="btn btn-ghost" type="button" (click)="exportCsv()" [disabled]="!filtered.length">{{ 'common.exportCsv' | translate }}</button>
      </div>
    </header>

    @if (error) { <p class="alert" role="alert">{{ error }}</p> }

    <div class="stats commission-stats">
      <div class="stat"><span class="stat-num">{{ eligibleRows.length }}</span><span>{{ 'commissions.orders' | translate }}</span></div>
      <div class="stat"><span class="stat-num">{{ formatMoney(commissionTotal, 2) }}</span><span>{{ 'commissions.totalCommission' | translate }}</span></div>
      <div class="stat"><span class="stat-num">{{ formatMoney(commissionPaid, 2) }}</span><span>{{ 'commissions.paid' | translate }}</span></div>
      <div class="stat stat-main"><span class="stat-num">{{ formatMoney(commissionDue, 2) }}</span><span>{{ 'commissions.due' | translate }}</span></div>
    </div>

    <section class="filters commission-filters" [attr.aria-label]="'common.filters' | translate">
      <div class="filter">
        <label for="payoutStatus">{{ 'commissions.payoutStatus' | translate }}</label>
        <select id="payoutStatus" name="payoutStatus" [(ngModel)]="statusFilter">
          <option value="All">{{ 'commissions.allStatuses' | translate }}</option>
          <option value="Unpaid">{{ 'commissions.payout.Unpaid' | translate }}</option>
          <option value="PartiallyPaid">{{ 'commissions.payout.PartiallyPaid' | translate }}</option>
          <option value="Paid">{{ 'commissions.payout.Paid' | translate }}</option>
          <option value="Excluded">{{ 'commissions.payout.Excluded' | translate }}</option>
        </select>
      </div>
      <div class="filter">
        <label for="commissionFrom">{{ 'commissions.dateFrom' | translate }}</label>
        <input id="commissionFrom" name="commissionFrom" type="date" [(ngModel)]="fromDate">
      </div>
      <div class="filter">
        <label for="commissionTo">{{ 'commissions.dateTo' | translate }}</label>
        <input id="commissionTo" name="commissionTo" type="date" [(ngModel)]="toDate">
      </div>
      <button class="btn btn-ghost" type="button" (click)="clearFilters()">{{ 'common.clearFilters' | translate }}</button>
    </section>

    @if (loading) {
      <p class="muted">{{ 'common.loading' | translate }}</p>
    } @else if (!filtered.length) {
      <div class="state-card"><h3>{{ 'commissions.empty' | translate }}</h3></div>
    } @else {
      <div class="table-wrap">
        <table>
          <thead><tr>
            <th>{{ 'commissions.admin' | translate }}</th>
            <th>{{ 'commissions.reference' | translate }}</th>
            <th>{{ 'common.customer' | translate }}</th>
            <th>{{ 'commissions.orderDate' | translate }}</th>
            <th>{{ 'common.status' | translate }}</th>
            <th class="num">{{ 'commissions.orderTotalColumn' | translate }}</th>
            <th class="num">{{ 'commissions.amount' | translate }}</th>
            <th class="num">{{ 'commissions.paid' | translate }}</th>
            <th class="num">{{ 'commissions.outstanding' | translate }}</th>
            <th>{{ 'commissions.payoutStatus' | translate }}</th>
            <th>{{ 'commissions.paidAt' | translate }}</th>
            @if (auth.isOwner()) { <th class="actions-col">{{ 'common.actions' | translate }}</th> }
          </tr></thead>
          <tbody>
            @for (r of filtered; track r.orderId) {
              <tr>
                <td>{{ r.adminName }}</td>
                <td class="ref"><a routerLink="/admin/orders" [queryParams]="{ q: r.referenceNumber }">{{ r.referenceNumber }}</a></td>
                <td>{{ r.customerName || '—' }}</td>
                <td>{{ formatDate(r.orderDate) }}</td>
                <td><span class="badge" [class]="'s-' + r.status">{{ t('status.' + r.status) }}</span></td>
                <td class="num">{{ formatMoney(r.orderTotal, 2) }}</td>
                <td class="num"><strong>{{ formatMoney(r.commissionAmount, 2) }}</strong></td>
                <td class="num">{{ formatMoney(r.paidAmount, 2) }}</td>
                <td class="num"><strong>{{ formatMoney(r.outstandingAmount, 2) }}</strong></td>
                <td><span class="badge" [class]="'payout-' + r.payoutStatus">{{ t('commissions.payout.' + r.payoutStatus) }}</span></td>
                <td>{{ formatDate(r.paidAt) }}</td>
                @if (auth.isOwner()) {
                  <td class="row-actions">
                    @if (r.adminPhone) {
                      <a class="btn btn-ghost" [href]="smsHref(r)">
                        {{ 'commissions.textAdmin' | translate }}
                      </a>
                    }
                    <button class="btn btn-ghost" type="button" [disabled]="!r.eligible" (click)="openPayment(r)">
                      {{ t(r.paidAmount > 0 ? 'commissions.updatePayment' : 'commissions.recordPayment') }}
                    </button>
                    <button class="btn btn-danger" type="button" [disabled]="!!deleteBusy" (click)="deleteCommission(r)">
                      {{ 'commissions.deleteEntry' | translate }}
                    </button>
                  </td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    @if (paymentRow) {
      <div class="backdrop" (click)="closePayment()"></div>
      <section class="panel" role="dialog" aria-modal="true" aria-labelledby="commission-payment-title">
        <div class="panel-head">
          <div>
            <p class="eyebrow">{{ paymentRow.referenceNumber }}</p>
            <h2 id="commission-payment-title">{{ 'commissions.paymentTitle' | translate }}</h2>
          </div>
          <button class="btn btn-ghost" type="button" [disabled]="paymentBusy" (click)="closePayment()">{{ 'common.close' | translate }}</button>
        </div>
        <div class="customer-summary">
          <span>{{ 'commissions.admin' | translate }}</span>
          <strong>{{ paymentRow.adminName }}</strong>
          <small>{{ paymentRow.customerName || '—' }} · {{ formatDate(paymentRow.orderDate) }}</small>
        </div>
        <dl class="detail-list">
          <div><dt>{{ 'commissions.amount' | translate }}</dt><dd>{{ formatMoney(paymentRow.commissionAmount, 2) }}</dd></div>
          <div><dt>{{ 'commissions.paid' | translate }}</dt><dd>{{ formatMoney(paymentRow.paidAmount, 2) }}</dd></div>
          <div><dt>{{ 'commissions.outstanding' | translate }}</dt><dd>{{ formatMoney(paymentRow.outstandingAmount, 2) }}</dd></div>
        </dl>
        @if (error) { <p class="alert" role="alert">{{ error }}</p> }
        <div class="field">
          <label for="payoutAmount">{{ 'commissions.paidAmount' | translate }}</label>
          <input id="payoutAmount" name="payoutAmount" type="number" min="0" step="0.01" [max]="paymentRow.commissionAmount" [(ngModel)]="paymentAmount">
          <small class="muted">{{ 'commissions.paymentHelp' | translate }}</small>
        </div>
        <div class="field">
          <label for="payoutNote">{{ 'commissions.paymentNote' | translate }}</label>
          <textarea id="payoutNote" name="payoutNote" rows="3" maxlength="1000" [(ngModel)]="paymentNote"
                    [placeholder]="'commissions.paymentNotePlaceholder' | translate"></textarea>
        </div>
        <div class="panel-actions">
          <button class="btn btn-ghost" type="button" [disabled]="paymentBusy" (click)="closePayment()">{{ 'common.cancel' | translate }}</button>
          <button class="btn btn-primary" type="button" [disabled]="paymentBusy" (click)="savePayment()">
            {{ (paymentBusy ? 'common.saving' : 'common.save') | translate }}
          </button>
        </div>
      </section>
    }
  `,
  host: { class: 'commission-page' }
})
export class CommissionsComponent implements OnInit, OnDestroy {
  api = inject(ApiService);
  auth = inject(AuthService);
  language = inject(LanguageService);
  private realtime = inject(RealtimeService);
  private readonly destroyed$ = new Subject<void>();

  rows: CommissionRow[] = [];
  search = '';
  statusFilter = 'All';
  fromDate = '';
  toDate = '';
  loading = true;
  error = '';
  paymentRow: CommissionRow | null = null;
  paymentAmount: number | null = null;
  paymentNote = '';
  paymentBusy = false;
  deleteBusy = '';
  private previousBodyOverflow = '';

  get locale(): string { return this.language.locale(); }
  t(key: string, params: TranslationParams = {}): string { return this.language.t(key, params); }
  formatDate(value: string | null): string {
    if (!value) return '—';
    const date = new Date(value);
    return Number.isNaN(date.getTime())
      ? '—'
      : new Intl.DateTimeFormat(this.locale, { dateStyle: 'medium', timeZone: 'UTC' }).format(date);
  }
  formatMoney(value: number, maximumFractionDigits = 0): string {
    const amount = Number(value);
    return Number.isFinite(amount) ? new Intl.NumberFormat(this.locale, { maximumFractionDigits }).format(amount) + ' KMF' : '';
  }
  get eligibleRows(): CommissionRow[] { return this.rows.filter(r => r.eligible); }
  get commissionTotal(): number { return this.eligibleRows.reduce((sum, r) => sum + Number(r.commissionAmount || 0), 0); }
  get commissionPaid(): number { return this.eligibleRows.reduce((sum, r) => sum + Number(r.paidAmount || 0), 0); }
  get commissionDue(): number { return this.eligibleRows.reduce((sum, r) => sum + Number(r.outstandingAmount || 0), 0); }
  get filtered(): CommissionRow[] {
    const q = this.search.trim().toLowerCase();
    return this.rows.filter(r => {
      const matchesSearch = !q || [r.adminName, r.referenceNumber, r.customerName, r.status, r.paymentStatus, r.paymentNote]
        .join(' ').toLowerCase().includes(q);
      const matchesStatus = this.statusFilter === 'All' || r.payoutStatus === this.statusFilter;
      const date = String(r.orderDate).slice(0, 10);
      const matchesFrom = !this.fromDate || date >= this.fromDate;
      const matchesTo = !this.toDate || date <= this.toDate;
      return matchesSearch && matchesStatus && matchesFrom && matchesTo;
    });
  }

  constructor() {
    this.realtime.changes$.pipe(debounceTime(250), takeUntil(this.destroyed$)).subscribe(resources => {
      if (resources.includes('*') || resources.includes('commissions') || resources.includes('commands')) this.load();
    });
  }

  ngOnInit(): void {
    this.load();
  }

  ngOnDestroy(): void {
    this.destroyed$.next();
    this.destroyed$.complete();
  }

  load(): void {
    this.loading = true;
    this.api.list<CommissionRow>('commissions').subscribe({
      next: rows => { this.rows = rows; this.error = ''; this.loading = false; },
      error: () => { this.error = this.t('common.loadFailed'); this.loading = false; }
    });
  }

  clearFilters(): void {
    this.search = '';
    this.statusFilter = 'All';
    this.fromDate = '';
    this.toDate = '';
  }

  openPayment(row: CommissionRow): void {
    if (!this.auth.isOwner() || !row.eligible) return;
    this.paymentRow = row;
    this.paymentAmount = row.commissionAmount;
    this.paymentNote = row.paymentNote ?? '';
    this.error = '';
    this.previousBodyOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
  }

  @HostListener('document:keydown.escape')
  closePayment(): void {
    if (this.paymentBusy) return;
    this.paymentRow = null;
    this.paymentAmount = null;
    this.paymentNote = '';
    this.paymentBusy = false;
    this.error = '';
    document.body.style.overflow = this.previousBodyOverflow;
  }

  savePayment(): void {
    if (!this.auth.isOwner() || !this.paymentRow || this.paymentBusy || this.paymentAmount === null) return;
    const amount = Number(this.paymentAmount);
    if (!Number.isFinite(amount) || amount < 0 || amount > this.paymentRow.commissionAmount) {
      this.error = this.t('commissions.paymentRange');
      return;
    }
    this.paymentBusy = true;
    this.error = '';
    this.api.create(`commissions/${this.paymentRow.orderId}/payment`, { amount, note: this.paymentNote }).subscribe({
      next: (updated: CommissionRow) => {
        this.rows = this.rows.map(row => row.orderId === updated.orderId ? updated : row);
        this.paymentBusy = false;
        this.closePayment();
      },
      error: error => {
        this.paymentBusy = false;
        const body = error.error;
        const raw = typeof body === 'string' ? body : body?.detail ?? body?.title ?? '';
        this.error = this.language.apiMessage(raw) || this.t('commissions.paymentFailed');
      }
    });
  }

  smsHref(row: CommissionRow): string | null {
    const phone = String(row.adminPhone ?? '').replace(/[^\d+]/g, '');
    if (!phone) return null;
    const lines = [
      'Beauty & Shine',
      this.t('commissions.smsMessage', {
        name: row.adminName,
        reference: row.referenceNumber,
        amount: this.formatMoney(row.paidAmount, 2)
      })
    ];
    if (row.paymentNote) lines.push(`${this.t('commissions.paymentNote')}: ${row.paymentNote}`);
    return `sms:${phone}?body=${encodeURIComponent(lines.join('\n'))}`;
  }

  deleteCommission(row: CommissionRow): void {
    if (!this.auth.isOwner() || this.deleteBusy) return;
    if (!confirm(this.t('commissions.deleteConfirm'))) return;
    this.deleteBusy = row.orderId;
    this.error = '';
    this.api.remove('commissions', row.orderId).subscribe({
      next: () => {
        this.rows = this.rows.filter(item => item.orderId !== row.orderId);
        if (this.paymentRow?.orderId === row.orderId) this.closePayment();
        this.deleteBusy = '';
      },
      error: error => {
        this.deleteBusy = '';
        const body = error.error;
        const raw = typeof body === 'string' ? body : body?.detail ?? body?.title ?? '';
        this.error = this.language.apiMessage(raw) || this.t('commissions.deleteFailed');
      }
    });
  }

  exportCsv(): void {
    if (!this.filtered.length) return;
    const cell = (value: any) => {
      let text = String(value ?? '');
      if (/^[=+\-@]/.test(text)) text = `'${text}`;
      return `"${text.replace(/"/g, '""')}"`;
    };
    const headers = [
      this.t('commissions.admin'), this.t('commissions.reference'), this.t('common.customer'),
      this.t('commissions.orderDate'), this.t('common.status'), this.t('commissions.orderTotalColumn'),
      this.t('commissions.amount'), this.t('commissions.paid'),
      this.t('commissions.outstanding'), this.t('commissions.payoutStatus'), this.t('commissions.paidAt'),
      this.t('commissions.paymentNote')
    ];
    const lines = [headers.map(cell).join(',')];
    for (const r of this.filtered) {
      lines.push([
        r.adminName, r.referenceNumber, r.customerName, r.orderDate, this.t(`status.${r.status}`),
        r.orderTotal, r.commissionAmount, r.paidAmount,
        r.outstandingAmount, this.t(`commissions.payout.${r.payoutStatus}`), r.paidAt?.slice(0, 10) ?? '', r.paymentNote
      ].map(cell).join(','));
    }
    const url = URL.createObjectURL(new Blob([lines.join('\r\n')], { type: 'text/csv;charset=utf-8;' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = `commissions-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }
}
