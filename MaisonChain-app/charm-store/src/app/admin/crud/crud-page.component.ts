import { Component, DestroyRef, HostListener, OnInit, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ImageFallbackDirective } from '../../core/image-fallback.directive';
import { KmfPipe } from '../../core/kmf.pipe';
import { LanguageService } from '../../core/language.service';
import { RealtimeService } from '../../core/realtime.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { CrudConfig, Field, Option, RowAction } from '../../core/models';
import { TranslationParams } from '../../core/translations';

@Component({
  selector: 'app-crud-page',
  standalone: true,
  imports: [FormsModule, DatePipe, KmfPipe, ImageFallbackDirective, TranslatePipe],
  templateUrl: './crud-page.component.html'
})
export class CrudPageComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);
  private realtime = inject(RealtimeService);
  private auth = inject(AuthService);
  language = inject(LanguageService);

  cfg: CrudConfig = this.route.snapshot.data['config'];
  canEdit = this.auth.isOwner();
  canCreate = this.cfg.canCreate ?? this.canEdit;
  viewOnly = false;

  rows: any[] = [];
  lookups: Record<string, any[]> = {};
  search = '';
  editing: any = null;
  isNew = false;
  saving = false;
  uploading = false;
  actionBusy = '';
  error = '';
  filtersOpen = false;

  viewMode: 'table' | 'cards' = 'table';
  density: 'comfortable' | 'compact' = 'comfortable';
  visibleKeys: string[] = [];

  sel: Record<string, any> = {};
  from: Record<string, string> = {};
  to: Record<string, string> = {};
  min: Record<string, number | null> = {};
  max: Record<string, number | null> = {};
  checks: Record<number, boolean> = {};
  sortKey = '';
  sortDir: 'asc' | 'desc' = 'asc';
  private previousBodyOverflow = '';

  get locale(): string { return this.language.locale(); }
  get singular(): string { return this.t(this.cfg.singular); }
  get title(): string { return this.t(this.cfg.title); }
  get editLabel(): string { return this.t(this.cfg.editLabel || 'common.edit'); }

  t(key: string, params: TranslationParams = {}): string { return this.language.t(key, params); }

  ngOnInit(): void {
    this.load();
    this.clearFilters();
    this.loadLookups();
    this.loadViewPreferences();
    if (this.cfg.endpoint === 'commands') {
      this.sortKey = 'createdAt';
      this.sortDir = 'desc';
    }

    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(p => {
      const q = p.get('q');
      if (q !== null) this.search = q;
    });

    this.realtime.changes$.pipe(debounceTime(250), takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      const all = res.includes('*');
      if (all || res.includes(this.cfg.endpoint)) this.load();
      if (all || this.cfg.fields.some(f => f.lookup && res.includes(f.lookup))) this.loadLookups();
    });
  }

  load(): void {
    this.api.list(this.cfg.endpoint).subscribe({
      next: r => { this.rows = r; this.error = ''; },
      error: () => (this.error = this.t('common.loadFailed'))
    });
  }

  private loadLookups(): void {
    const names = new Set(this.cfg.fields.filter(f => f.lookup).map(f => f.lookup!));
    for (const name of names) {
      this.api.list(name).subscribe({
        next: d => (this.lookups[name] = d),
        error: () => (this.error = this.t('common.lookupFailed', { name }))
      });
    }
  }

  get listFields(): Field[] { return this.cfg.fields.filter(f => !f.hideInList); }
  get visibleListFields(): Field[] {
    const visible = new Set(this.visibleKeys);
    return this.listFields.filter(f => visible.has(f.key));
  }
  get formFields(): Field[] { return this.cfg.fields.filter(f => !f.readonly && f.showInDetail !== false); }
  get readonlyFields(): Field[] { return this.cfg.fields.filter(f => f.readonly && f.showInDetail !== false); }
  get detailFields(): Field[] { return this.cfg.fields.filter(f => f.showInDetail !== false); }
  get filterFields(): Field[] {
    return this.cfg.fields.filter(f => ['select', 'date', 'datetime', 'number', 'money'].includes(f.type));
  }

  get hasFilters(): boolean {
    return !!this.search.trim() ||
      Object.values(this.sel).some(v => v !== null && v !== undefined) ||
      Object.values(this.from).some(v => !!v) || Object.values(this.to).some(v => !!v) ||
      Object.values(this.min).some(v => v !== null && v !== undefined) ||
      Object.values(this.max).some(v => v !== null && v !== undefined) ||
      Object.values(this.checks).some(v => !!v);
  }

  clearFilters(): void {
    this.search = '';
    this.sel = {}; this.from = {}; this.to = {}; this.min = {}; this.max = {}; this.checks = {};
    for (const f of this.filterFields) {
      if (f.type === 'select') this.sel[f.key] = null;
      if (f.type === 'number' || f.type === 'money') { this.min[f.key] = null; this.max[f.key] = null; }
    }
  }

  sortBy(f: Field): void {
    if (this.sortKey === f.key) this.sortDir = this.sortDir === 'asc' ? 'desc' : 'asc';
    else { this.sortKey = f.key; this.sortDir = 'asc'; }
  }

  private sortVal(row: any, f: Field): number | string {
    const v = row[f.key];
    if (f.type === 'number' || f.type === 'money') return Number(v) || 0;
    if (f.type === 'select') return this.label(row, f).toLowerCase();
    return String(v ?? '').toLowerCase();
  }

  get filtered(): any[] {
    const q = this.search.trim().toLowerCase();
    let out = this.rows.filter(r => {
      const searchable = [JSON.stringify(r), ...this.cfg.fields.map(f => this.label(r, f))].join(' ').toLowerCase();
      if (q && !searchable.includes(q)) return false;
      for (const f of this.filterFields) {
        const v = r[f.key];
        if (f.type === 'select') {
          const s = this.sel[f.key];
          if (s !== null && s !== undefined && v !== s) return false;
        } else if (f.type === 'date' || f.type === 'datetime') {
          const d = v ? String(v).slice(0, 10) : '';
          const a = this.from[f.key], b = this.to[f.key];
          if ((a || b) && !d) return false;
          if (a && d < a) return false;
          if (b && d > b) return false;
        } else {
          const n = v === null || v === undefined ? NaN : Number(v);
          const lo = this.min[f.key], hi = this.max[f.key];
          if ((lo != null || hi != null) && isNaN(n)) return false;
          if (lo != null && n < lo) return false;
          if (hi != null && n > hi) return false;
        }
      }
      return (this.cfg.quickChecks ?? []).every((c, i) => !this.checks[i] || c.test(r));
    });
    if (this.sortKey) {
      const f = this.cfg.fields.find(x => x.key === this.sortKey)!;
      const dir = this.sortDir === 'asc' ? 1 : -1;
      out = [...out].sort((a, b) => {
        const x = this.sortVal(a, f), y = this.sortVal(b, f);
        return (x < y ? -1 : x > y ? 1 : 0) * dir;
      });
    }
    return out;
  }

  img(p?: string | null) { return this.api.img(p); }

  options(f: Field): Option[] {
    if (f.key === 'status' || f.key === 'paymentStatus') {
      const values = f.key === 'status'
        ? ['Pending', 'Processing', 'Shipped', 'Delivered', 'Cancelled']
        : ['Unpaid', 'Payment submitted', 'Deposit paid', 'Paid in full'];
      return values.map(value => ({ value, label: this.t(`${f.key === 'status' ? 'status' : 'payment'}.${value}`) }));
    }
    if (f.options) return f.options;
    const wanted = (f.lookupLabel ?? 'name').split(',').map(k => k.trim());
    const get = (x: any, k: string) => x[k] ?? x[k.charAt(0).toUpperCase() + k.slice(1)];
    return (this.lookups[f.lookup!] ?? []).map(x => {
      const id = x.id ?? x.Id;
      let label = wanted.map(k => get(x, k)).filter(Boolean).join(' · ');
      if (!label) {
        label = ['name', 'label', 'title', 'model', 'fullName'].map(k => get(x, k)).find(Boolean)
          ?? (Object.values(x).find(v => typeof v === 'string') as string)
          ?? String(id);
      }
      return { value: id, label };
    });
  }

  label(row: any, f: Field): string {
    if (f.displayKey && row[f.displayKey]) return String(row[f.displayKey]);
    const v = row[f.key];
    if (f.key === 'status' && v) return this.t(`status.${v}`);
    if (f.key === 'paymentStatus' && v) return this.t(`payment.${v}`);
    return f.type === 'select' ? this.options(f).find(o => o.value === v)?.label ?? '' : v ?? '';
  }

  customerName(row: any): string {
    const field = this.cfg.fields.find(f => f.key === 'customerId');
    return String(row.customerName || (field ? this.label(row, field) : '') || this.t('common.unassignedCustomer'));
  }

  canEditRow(row: any): boolean {
    return this.canEdit && (!this.cfg.canEditRow || this.cfg.canEditRow(row));
  }

  canDeleteRow(row: any): boolean {
    return this.canEdit && (!this.cfg.canDeleteRow || this.cfg.canDeleteRow(row));
  }

  setView(view: 'table' | 'cards'): void {
    this.viewMode = view;
    this.saveViewPreferences();
  }

  setDensity(density: 'comfortable' | 'compact'): void {
    this.density = density;
    this.saveViewPreferences();
  }

  toggleColumn(key: string): void {
    this.visibleKeys = this.visibleKeys.includes(key)
      ? this.visibleKeys.filter(k => k !== key)
      : [...this.visibleKeys, key];
    this.saveViewPreferences();
  }

  resetColumns(): void {
    this.visibleKeys = this.listFields.map(f => f.key);
    this.saveViewPreferences();
  }

  exportCsv(): void {
    if (!this.filtered.length) return;
    const fields = this.visibleListFields;
    const lines = [fields.map(f => this.csvCell(this.t(f.label))).join(',')];
    for (const row of this.filtered) {
      lines.push(fields.map(f => this.csvCell(this.label(row, f))).join(','));
    }
    const blob = new Blob([lines.join('\r\n')], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${this.cfg.endpoint}-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  openNew(): void {
    if (!this.canCreate) return;
    this.editing = {};
    for (const f of this.formFields) {
      if (f.type === 'date') this.editing[f.key] = new Date().toISOString().slice(0, 10);
      else if (f.type === 'select' && f.options) this.editing[f.key] = f.options[0].value;
      else if (f.type === 'select') this.editing[f.key] = null;
    }
    this.isNew = true;
    this.viewOnly = false;
    this.error = '';
    this.lockBody();
  }

  openView(row: any): void {
    this.editing = { ...row };
    this.isNew = false;
    this.viewOnly = true;
    this.error = '';
    this.lockBody();
  }

  openEdit(row: any): void {
    if (!this.canEditRow(row)) return;
    this.editing = { ...row };
    for (const f of this.formFields) {
      if (f.type === 'date' && this.editing[f.key]) this.editing[f.key] = String(this.editing[f.key]).slice(0, 10);
    }
    this.isNew = false;
    this.viewOnly = false;
    this.error = '';
    this.lockBody();
  }

  @HostListener('document:keydown.escape')
  close(): void {
    this.editing = null;
    this.error = '';
    this.filtersOpen = false;
    this.unlockBody();
  }

  onFile(ev: Event, f: Field): void {
    const file = (ev.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.uploading = true;
    this.error = '';
    this.api.upload(file).subscribe({
      next: r => { this.editing[f.key] = r.url; this.uploading = false; },
      error: () => { this.error = this.t('common.uploadFailed'); this.uploading = false; }
    });
  }

  save(): void {
    if (this.viewOnly || (this.isNew ? !this.canCreate : !this.canEditRow(this.editing))) return;
    this.saving = true;
    const payload: any = {};
    for (const f of this.formFields) payload[f.key] = this.editing[f.key];
    const req = this.isNew
      ? this.api.create(this.cfg.endpoint, payload)
      : this.api.update(this.cfg.endpoint, this.editing.id, payload);
    req.subscribe({
      next: () => { this.saving = false; this.close(); this.load(); },
      error: e => {
        this.saving = false;
        const b = e.error;
        const rawDetail = typeof b === 'string' ? b
          : b?.errors ? JSON.stringify(b.errors)
          : b?.detail ?? b?.title ?? '';
        const detail = this.language.apiMessage(rawDetail);
        this.error = this.t('common.saveFailed', { item: this.singular, detail });
      }
    });
  }

  runAction(action: RowAction, row: any): void {
    if (!this.canEdit || this.actionBusy) return;
    if (action.confirm && !confirm(this.t(action.confirm))) return;
    this.actionBusy = `${row.id}:${action.path}`;
    this.error = '';
    this.api.create(`${this.cfg.endpoint}/${row.id}/${action.path}`, action.body?.(row) ?? {}).pipe(
      finalize(() => (this.actionBusy = ''))
    ).subscribe({
      next: () => this.load(),
      error: error => (this.error = typeof error.error === 'string' && error.error
        ? this.language.apiMessage(error.error)
        : this.t('common.actionFailed'))
    });
  }

  openLink(href: string): void {
    window.location.href = href;
  }

  remove(row: any): void {
    if (!this.canDeleteRow(row)) return;
    if (!confirm(this.t('common.deleteConfirm', { item: this.singular }))) return;
    this.api.remove(this.cfg.endpoint, row.id).subscribe({
      next: () => this.load(),
      error: e => (this.error = typeof e.error === 'string' && e.error
        ? this.language.apiMessage(e.error)
        : this.t('common.deleteFailed', { item: this.singular }))
    });
  }

  private get storageKey(): string {
    return `charm_admin_view_${this.auth.username || 'default'}_${this.cfg.endpoint}`;
  }

  private loadViewPreferences(): void {
    const defaults = this.listFields.map(f => f.key);
    this.visibleKeys = defaults;
    try {
      const saved = JSON.parse(localStorage.getItem(this.storageKey) || '{}');
      const valid = Array.isArray(saved.visibleKeys)
        ? saved.visibleKeys.filter((key: string) => defaults.includes(key))
        : [];
      this.visibleKeys = valid.length ? valid : defaults;
      this.viewMode = saved.viewMode === 'cards' ? 'cards' : 'table';
      this.density = saved.density === 'compact' ? 'compact' : 'comfortable';
    } catch {
      this.viewMode = 'table';
      this.density = 'comfortable';
    }
  }

  private saveViewPreferences(): void {
    try {
      localStorage.setItem(this.storageKey, JSON.stringify({
        visibleKeys: this.visibleKeys,
        viewMode: this.viewMode,
        density: this.density
      }));
    } catch {
      return;
    }
  }

  private csvCell(value: any): string {
    let text = String(value ?? '');
    if (/^[=+\-@]/.test(text)) text = `'${text}`;
    return `"${text.replace(/"/g, '""')}"`;
  }

  private lockBody(): void {
    this.previousBodyOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
  }

  private unlockBody(): void {
    document.body.style.overflow = this.previousBodyOverflow;
  }
}
