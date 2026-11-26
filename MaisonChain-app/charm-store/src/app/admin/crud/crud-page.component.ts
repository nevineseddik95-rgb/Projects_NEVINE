import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { KmfPipe } from '../../core/kmf.pipe';
import { ImageFallbackDirective } from '../../core/image-fallback.directive';
import { RealtimeService } from '../../core/realtime.service';
import { CrudConfig, Field, Option, RowAction } from '../../core/models';

/** One reusable list + create/edit/view/delete screen, driven by a CrudConfig (see entities.ts). */
@Component({
  selector: 'app-crud-page',
  standalone: true,
  imports: [FormsModule, DatePipe, KmfPipe, ImageFallbackDirective],
  templateUrl: './crud-page.component.html'
})
export class CrudPageComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);
  private realtime = inject(RealtimeService);

  cfg: CrudConfig = this.route.snapshot.data['config'];
  canEdit = inject(AuthService).isOwner();     // view-only admins cannot change anything
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

  // filters and sorting
  sel: Record<string, any> = {};
  from: Record<string, string> = {};
  to: Record<string, string> = {};
  min: Record<string, number | null> = {};
  max: Record<string, number | null> = {};
  checks: Record<number, boolean> = {};
  sortKey = '';
  sortDir: 'asc' | 'desc' = 'asc';

  ngOnInit(): void {
    this.load();
    this.clearFilters();
    this.loadLookups();
    if (this.cfg.endpoint === 'commands') {
      this.sortKey = 'createdAt';
      this.sortDir = 'desc';
    }

    // a link from the notification bell opens the list already filtered to that order
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(p => {
      const q = p.get('q');
      if (q !== null) this.search = q;
    });

    // the list refreshes by itself when something changes on the server
    this.realtime.changes$.pipe(debounceTime(250), takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      const all = res.includes('*');
      if (all || res.includes(this.cfg.endpoint)) this.load();
      if (all || this.cfg.fields.some(f => f.lookup && res.includes(f.lookup))) this.loadLookups();
    });
  }

  load(): void {
    this.api.list(this.cfg.endpoint).subscribe({
      next: r => { this.rows = r; this.error = ''; },
      error: () => (this.error = 'Could not load the list. Check that the API is running and you are signed in.')
    });
  }

  private loadLookups(): void {
    const names = new Set(this.cfg.fields.filter(f => f.lookup).map(f => f.lookup!));
    for (const name of names) {
      this.api.list(name).subscribe({
        next: d => (this.lookups[name] = d),
        error: () => (this.error = `The list "${name}" could not be loaded from the API.`)
      });
    }
  }

  get listFields(): Field[] { return this.cfg.fields.filter(f => !f.hideInList); }
  get formFields(): Field[] { return this.cfg.fields.filter(f => !f.readonly); }
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
      if (q && !JSON.stringify(r).toLowerCase().includes(q)) return false;
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
    const v = row[f.key];
    return f.type === 'select' ? this.options(f).find(o => o.value === v)?.label ?? '' : v ?? '';
  }

  openNew(): void {
    if (!this.canEdit) return;
    this.editing = {};
    for (const f of this.formFields) {
      if (f.type === 'date') this.editing[f.key] = new Date().toISOString().slice(0, 10);
      else if (f.type === 'select' && f.options) this.editing[f.key] = f.options[0].value;
      else if (f.type === 'select') this.editing[f.key] = null;
    }
    this.isNew = true;
    this.viewOnly = false;
    this.error = '';
  }

  openEdit(row: any): void {
    this.editing = { ...row };
    for (const f of this.formFields) {
      if (f.type === 'date' && this.editing[f.key]) this.editing[f.key] = String(this.editing[f.key]).slice(0, 10);
    }
    this.isNew = false;
    this.viewOnly = !this.canEdit;
    this.error = '';
  }

  close(): void { this.editing = null; this.error = ''; }

  onFile(ev: Event, f: Field): void {
    const file = (ev.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.uploading = true;
    this.error = '';
    this.api.upload(file).subscribe({
      next: r => { this.editing[f.key] = r.url; this.uploading = false; },
      error: () => { this.error = 'The image could not be uploaded. Use a JPG, PNG or WebP under 5 MB.'; this.uploading = false; }
    });
  }

  save(): void {
    if (!this.canEdit) return;
    this.saving = true;
    const req = this.isNew
      ? this.api.create(this.cfg.endpoint, this.editing)
      : this.api.update(this.cfg.endpoint, this.editing.id, this.editing);
    req.subscribe({
      next: () => { this.saving = false; this.close(); this.load(); },
      error: e => {
        this.saving = false;
        const b = e.error;
        const detail = typeof b === 'string' ? b
          : b?.errors ? JSON.stringify(b.errors)
          : b?.detail ?? b?.title ?? '';
        this.error = `The ${this.cfg.singular} could not be saved. ${detail}`;
      }
    });
  }

  /** Runs an extra row button, such as "Confirm deposit": POST /api/<endpoint>/<id>/<path> */
  runAction(action: RowAction, row: any): void {
    if (!this.canEdit || this.actionBusy) return;
    if (action.confirm && !confirm(action.confirm)) return;
    this.actionBusy = `${row.id}:${action.path}`;
    this.error = '';
    this.api.create(`${this.cfg.endpoint}/${row.id}/${action.path}`, action.body?.(row) ?? {}).pipe(
      finalize(() => (this.actionBusy = ''))
    ).subscribe({
      next: () => this.load(),
      error: error => (this.error = typeof error.error === 'string' && error.error
        ? error.error
        : 'The action failed. Try again.')
    });
  }

  remove(row: any): void {
    if (!this.canEdit) return;
    if (!confirm(`Delete this ${this.cfg.singular}? This cannot be undone.`)) return;
    this.api.remove(this.cfg.endpoint, row.id).subscribe({
      next: () => this.load(),
      error: () => (this.error = `The ${this.cfg.singular} could not be deleted. It may be used by other records.`)
    });
  }
}