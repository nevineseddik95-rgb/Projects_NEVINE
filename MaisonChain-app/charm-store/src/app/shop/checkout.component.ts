import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize, timeout } from 'rxjs';
import { ApiService } from '../core/api.service';
import { CartService, toCartProduct } from '../core/cart.service';
import { KmfPipe } from '../core/kmf.pipe';
import { LanguageService } from '../core/language.service';
import { LanguageSwitcherComponent } from '../core/language-switcher.component';
import { TranslatePipe } from '../core/translate.pipe';
import { TranslationParams } from '../core/translations';

interface CheckoutResult {
  reference: string;
  items: { model: string; quantity: number; lineTotal: number }[];
  total: number;
  deposit: number;
  balance: number;
  method: string;
  instructions: string;
  emailQueued: boolean;
  smsQueued: boolean;
  smsRequested: boolean;
  phone: string;
}

@Component({
  selector: 'app-checkout',
  standalone: true,
  imports: [KmfPipe, FormsModule, RouterLink, LanguageSwitcherComponent, TranslatePipe],
  template: `
    <header class="shop-head">
      <a class="brand" routerLink="/">Beauty & Shine</a>
      <nav class="shop-nav">
        <a class="nav-link" routerLink="/cart">{{ 'checkout.backToCart' | translate }}</a>
        <app-language-switcher />
      </nav>
    </header>

    <main class="checkout">
      @if (done) {
        <h1>{{ 'checkout.received' | translate }}</h1>
        <section class="summary">
          <p class="notice">{{ 'checkout.referenceNotice' | translate: { reference: done.reference } }}</p>
          @if (done.emailQueued) {
            <p class="hint">{{ 'checkout.emailQueued' | translate }}</p>
          } @else if (email) {
            <p class="hint">{{ 'checkout.emailNotQueued' | translate }}</p>
          }
          @if (done.smsQueued) {
            <p class="hint">{{ 'checkout.smsQueued' | translate }}</p>
          }
          @for (i of done.items; track i.model) {
            <div class="row"><span>{{ i.model }} × {{ i.quantity }}</span><span>{{ i.lineTotal | kmf }}</span></div>
          }
          <div class="row"><strong>{{ 'common.total' | translate }}</strong><strong>{{ done.total | kmf }}</strong></div>
          <div class="row"><span>{{ 'checkout.depositNow' | translate }}</span><strong>{{ done.deposit | kmf }}</strong></div>
          <div class="row"><span>{{ 'checkout.balanceLater' | translate }}</span><span>{{ done.balance | kmf }}</span></div>
        </section>
        <section>
          <h2>{{ 'checkout.payWith' | translate: { method: done.method } }}</h2>
          <p>{{ done.instructions || ('checkout.instructionsFallback' | translate) }}</p>

          @if (noticeSent) {
            <p class="notice">{{ 'checkout.noticeReceived' | translate }}</p>
            @if (noticeSmsQueued) {
              <p class="hint">{{ 'checkout.noticeSms' | translate }}</p>
            }
          } @else {
            <form #pf="ngForm" (ngSubmit)="sendNotice()">
              <div class="field">
                <label for="txid">{{ 'checkout.transactionLabel' | translate }}</label>
                <input id="txid" name="txid" [(ngModel)]="transactionId" required minlength="3" maxlength="100"
                       autocomplete="off" [placeholder]="'checkout.transactionPlaceholder' | translate">
                <p class="hint">{{ 'checkout.transactionHint' | translate }}</p>
              </div>
              @if (noticeError) { <p class="alert" role="alert">{{ noticeError }}</p> }
              <button class="btn btn-primary" type="submit" [disabled]="pf.invalid || busy">
                {{ busy ? ('checkout.submittingTransaction' | translate) : ('checkout.havePaid' | translate) }}
              </button>
            </form>
          }
          <p><a class="btn btn-ghost" routerLink="/">{{ 'checkout.continueShopping' | translate }}</a></p>
        </section>
      } @else if (cart.lines().length === 0) {
        <h1>{{ 'checkout.emptyTitle' | translate }}</h1>
        <p class="muted"><a routerLink="/">{{ 'cart.browse' | translate }}</a></p>
      } @else {
        <h1>{{ 'checkout.title' | translate }}</h1>

        <section class="summary" [attr.aria-label]="'common.yourOrder' | translate">
          @for (l of cart.lines(); track l.productId) {
            <div class="row"><span>{{ l.model }} × {{ l.quantity }}</span><span>{{ l.price * l.quantity | kmf }}</span></div>
          }
          <div class="row"><strong>{{ 'common.total' | translate }}</strong><strong>{{ cart.total() | kmf }}</strong></div>
          <div class="row"><span>{{ 'common.minimumDeposit' | translate }}</span><span>{{ minDeposit | kmf }}</span></div>
          <div class="row"><span>{{ 'checkout.balanceAfterDeposit' | translate }}</span><span>{{ balance | kmf }}</span></div>
        </section>

        <form #f="ngForm" (ngSubmit)="submit()">
          <div class="field">
            <label for="fullName">{{ 'checkout.fullName' | translate }}</label>
            <input id="fullName" name="fullName" autocomplete="name" [(ngModel)]="fullName" required>
          </div>
          <div class="field">
            <label for="email">{{ 'checkout.emailOptional' | translate }}</label>
            <input id="email" name="email" type="email" autocomplete="email" [(ngModel)]="email">
            <p class="hint">{{ 'checkout.emailHint' | translate }}</p>
          </div>
          <div class="field">
            <label for="phone">{{ 'checkout.phone' | translate }}</label>
            <input id="phone" name="phone" type="tel" autocomplete="tel" placeholder="+269 321 2345" [(ngModel)]="phone" required>
          </div>
          @if (smsAvailable) {
            <label class="check">
              <input type="checkbox" name="smsUpdates" [(ngModel)]="smsUpdates">
              {{ 'checkout.smsConsent' | translate }}
            </label>
            <p class="hint">{{ 'checkout.smsHint' | translate }}</p>
          }
          <div class="field">
            <label for="address">{{ 'checkout.deliveryAddress' | translate }}</label>
            <textarea id="address" name="address" rows="3" autocomplete="street-address" [(ngModel)]="address" required></textarea>
          </div>
          <div class="field">
            <label for="deposit">{{ 'checkout.depositLabel' | translate }}</label>
            <input id="deposit" name="deposit" type="number" step="1" [min]="minDeposit" [max]="cart.total()" [(ngModel)]="deposit" required>
            <p class="hint">{{ depositHint }}</p>
          </div>
          <div class="field">
            <label for="method">{{ 'checkout.paymentMethod' | translate }}</label>
            <input id="method" name="method" [(ngModel)]="method" readonly>
            <p class="hint">{{ 'checkout.methodHint' | translate }}</p>
          </div>

          @if (error) { <p class="alert" role="alert">{{ error }}</p> }
          <button class="btn btn-primary wide" type="submit" [disabled]="f.invalid || !depositOk || busy">
            {{ busy ? ('checkout.placing' | translate) : ('checkout.placeOrder' | translate) }}
          </button>
        </form>
      }
    </main>
  `
})
export class CheckoutComponent implements OnInit {
  api = inject(ApiService);
  cart = inject(CartService);
  language = inject(LanguageService);

  fullName = '';
  email = '';
  phone = '';
  smsAvailable = false;
  smsUpdates = false;
  address = '';
  method = 'MVola';
  deposit: number | null = null;
  busy = false;
  error = '';
  done: CheckoutResult | null = null;

  transactionId = '';
  noticeSent = false;
  noticeSmsQueued = false;
  noticeError = '';

  t(key: string, params: TranslationParams = {}): string { return this.language.t(key, params); }

  ngOnInit(): void {
    this.deposit = this.minDeposit;
    this.api.options<{ smsEnabled: boolean }>('notifications/options').subscribe({
      next: options => {
        this.smsAvailable = !!options.smsEnabled;
        this.smsUpdates = this.smsAvailable;
      },
      error: () => (this.smsAvailable = false)
    });
    this.api.list('products').subscribe({
      next: rows => {
        this.cart.sync(rows.map(toCartProduct));
        this.deposit = this.minDeposit;
      }
    });
  }

  get minDeposit(): number {
    return Math.ceil((Math.round(this.cart.total() * 100) * 40) / 10000);
  }

  get balance(): number {
    return Math.max(0, this.cart.total() - (this.deposit ?? 0));
  }

  get depositHint(): string {
    const money = (value: number) => new Intl.NumberFormat(this.language.locale(), { maximumFractionDigits: 0 }).format(value) + ' KMF';
    return this.t('checkout.depositHint', { minimum: money(this.minDeposit), total: money(this.cart.total()) });
  }

  get depositOk(): boolean {
    return this.deposit != null && this.cart.total() > 0 &&
      this.deposit >= this.minDeposit && this.deposit <= this.cart.total();
  }

  submit(): void {
    if (!this.depositOk || this.busy) return;
    this.busy = true;
    this.error = '';
    this.api.create('checkout', {
      fullName: this.fullName,
      email: this.email,
      phone: this.phone,
      smsUpdates: this.smsAvailable && this.smsUpdates,
      deliveryAddress: this.address,
      depositAmount: this.deposit,
      paymentMethod: this.method,
      items: this.cart.lines().map(l => ({ productId: l.productId, quantity: l.quantity }))
    }).pipe(
      timeout(30000),
      finalize(() => (this.busy = false))
    ).subscribe({
      next: result => {
        this.done = result as CheckoutResult;
        this.cart.clear();
      },
      error: error => {
        this.error = typeof error.error === 'string' && error.error
          ? this.language.apiMessage(error.error)
          : error.name === 'TimeoutError'
            ? this.t('checkout.timeout')
            : this.t('checkout.orderFailed');
      }
    });
  }

  sendNotice(): void {
    if (!this.done || this.busy) return;
    const transactionId = this.transactionId.trim();
    if (transactionId.length < 3 || transactionId.length > 100) {
      this.noticeError = this.t('checkout.transactionRequired');
      return;
    }

    this.busy = true;
    this.noticeError = '';
    this.api.create(`checkout/${this.done.reference}/payment`, {
      email: this.email,
      phone: this.phone,
      transactionId
    }).pipe(
      timeout(30000),
      finalize(() => (this.busy = false))
    ).subscribe({
      next: result => {
        this.noticeSent = true;
        this.noticeSmsQueued = !!result.smsQueued;
      },
      error: error => {
        this.noticeError = typeof error.error === 'string' && error.error
          ? this.language.apiMessage(error.error)
          : error.name === 'TimeoutError'
            ? this.t('checkout.noticeTimeout')
            : this.t('checkout.noticeFailed');
      }
    });
  }
}
