import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize, timeout } from 'rxjs';
import { ApiService } from '../core/api.service';
import { CartService, toCartProduct } from '../core/cart.service';
import { KmfPipe } from '../core/kmf.pipe';

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
  imports: [KmfPipe, FormsModule, RouterLink],
  template: `
    <header class="shop-head">
      <a class="brand" routerLink="/">Beauty & Shine</a>
      <nav class="shop-nav"><a class="nav-link" routerLink="/cart">Back to cart</a></nav>
    </header>

    <main class="checkout">
      @if (done) {
        <h1>Order received</h1>
        <section class="summary">
          <p class="notice">Your order reference is <strong>{{ done.reference }}</strong>. Keep it.</p>
          @if (done.emailQueued) {
            <p class="hint">Your confirmation email is queued and will be sent shortly.</p>
          } @else if (email) {
            <p class="hint">Your order is saved, but the confirmation email could not be queued.</p>
          }
          @if (done.smsQueued) {
            <p class="hint">Your confirmation SMS is queued with your reference and order details.</p>
          }
          @for (i of done.items; track i.model) {
            <div class="row"><span>{{ i.model }} × {{ i.quantity }}</span><span>{{ i.lineTotal | kmf }}</span></div>
          }
          <div class="row"><strong>Total</strong><strong>{{ done.total | kmf }}</strong></div>
          <div class="row"><span>Deposit to pay now</span><strong>{{ done.deposit | kmf }}</strong></div>
          <div class="row"><span>Balance later</span><span>{{ done.balance | kmf }}</span></div>
        </section>
        <section>
          <h2>Pay your deposit with {{ done.method }}</h2>
          <p>{{ done.instructions || 'We will contact you with the payment details.' }}</p>

          @if (noticeSent) {
            <p class="notice">Thank you. We received your transaction ID and will confirm your order once we check the payment.</p>
            @if (noticeSmsQueued) {
              <p class="hint">Your payment-update SMS is queued.</p>
            }
          } @else {
            <form #pf="ngForm" (ngSubmit)="sendNotice()">
              <div class="field">
                <label for="txid">Transaction ID from your payment message</label>
                <input id="txid" name="txid" [(ngModel)]="transactionId" required minlength="3" maxlength="100"
                       autocomplete="off" placeholder="Example: MP2406.1234.ABCD">
                <p class="hint">Copy the full reference from your MVola confirmation message.</p>
              </div>
              @if (noticeError) { <p class="alert" role="alert">{{ noticeError }}</p> }
              <button class="btn btn-primary" type="submit" [disabled]="pf.invalid || busy">
                {{ busy ? 'Submitting transaction…' : 'I have paid' }}
              </button>
            </form>
          }
          <p><a class="btn btn-ghost" routerLink="/">Continue shopping</a></p>
        </section>
      } @else if (cart.lines().length === 0) {
        <h1>Your cart is empty</h1>
        <p class="muted"><a routerLink="/">Browse products</a></p>
      } @else {
        <h1>Checkout</h1>

        <section class="summary" aria-label="Your order">
          @for (l of cart.lines(); track l.productId) {
            <div class="row"><span>{{ l.model }} × {{ l.quantity }}</span><span>{{ l.price * l.quantity | kmf }}</span></div>
          }
          <div class="row"><strong>Total</strong><strong>{{ cart.total() | kmf }}</strong></div>
          <div class="row"><span>Minimum deposit (40%)</span><span>{{ minDeposit | kmf }}</span></div>
          <div class="row"><span>Balance after your deposit</span><span>{{ balance | kmf }}</span></div>
        </section>

        <form #f="ngForm" (ngSubmit)="submit()">
          <div class="field">
            <label for="fullName">Full name</label>
            <input id="fullName" name="fullName" autocomplete="name" [(ngModel)]="fullName" required>
          </div>
          <div class="field">
            <label for="email">Email (optional)</label>
            <input id="email" name="email" type="email" autocomplete="email" [(ngModel)]="email">
            <p class="hint">Add an email to receive your order confirmation and status updates.</p>
          </div>
          <div class="field">
            <label for="phone">Phone</label>
            <input id="phone" name="phone" type="tel" autocomplete="tel" placeholder="+269 321 2345" [(ngModel)]="phone" required>
          </div>
          @if (smsAvailable) {
            <label class="check">
              <input type="checkbox" name="smsUpdates" [(ngModel)]="smsUpdates">
              Text me order updates by SMS
            </label>
            <p class="hint">Includes your reference, items, payment confirmation, and delivery status.</p>
          }
          <div class="field">
            <label for="address">Delivery address</label>
            <textarea id="address" name="address" rows="3" autocomplete="street-address" [(ngModel)]="address" required></textarea>
          </div>
          <div class="field">
            <label for="deposit">Deposit to pay now (KMF)</label>
            <input id="deposit" name="deposit" type="number" step="1" [min]="minDeposit" [max]="cart.total()" [(ngModel)]="deposit" required>
            <p class="hint">At least {{ minDeposit | kmf }} and no more than {{ cart.total() | kmf }}.</p>
          </div>
          <div class="field">
            <label for="method">Payment method</label>
            <input id="method" name="method" [(ngModel)]="method" readonly>
            <p class="hint">You will see the MVola payment instructions after you place the order.</p>
          </div>

          @if (error) { <p class="alert" role="alert">{{ error }}</p> }
          <button class="btn btn-primary wide" type="submit" [disabled]="f.invalid || !depositOk || busy">
            {{ busy ? 'Placing your order…' : 'Place order' }}
          </button>
        </form>
      }
    </main>
  `
})
export class CheckoutComponent implements OnInit {
  api = inject(ApiService);
  cart = inject(CartService);

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
          ? error.error
          : error.name === 'TimeoutError'
            ? 'The server took too long to respond. Your order may already be saved; check your connection before trying again.'
            : 'The order could not be placed. Check your details and try again.';
      }
    });
  }

  sendNotice(): void {
    if (!this.done || this.busy) return;
    const transactionId = this.transactionId.trim();
    if (transactionId.length < 3 || transactionId.length > 100) {
      this.noticeError = 'Enter the complete transaction ID from your payment message.';
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
          ? error.error
          : error.name === 'TimeoutError'
            ? 'The request timed out. Wait a moment, then submit the same transaction ID again; duplicate submissions are safe.'
            : 'We could not save the transaction ID. Check it and try again.';
      }
    });
  }
}
