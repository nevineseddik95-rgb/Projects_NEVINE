import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { CartService, toCartProduct } from '../core/cart.service';
import { KmfPipe } from '../core/kmf.pipe';

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
       @if (done.smsSent) { <p class="hint">A text message with this reference was sent to {{ done.phone }}.</p> }
       @if (done.emailSent) { <p class="hint">A confirmation email with this reference was sent to {{ email }}.</p> }
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
          } @else {
            <form #pf="ngForm" (ngSubmit)="sendNotice()">
              <div class="field">
                <label for="txid">Transaction ID from your payment message</label>
                <input id="txid" name="txid" [(ngModel)]="transactionId" required>
              </div>
              @if (noticeError) { <p class="alert" role="alert">{{ noticeError }}</p> }
              <button class="btn btn-primary" type="submit" [disabled]="pf.invalid || busy">I have paid</button>
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
  <p class="hint">We send your confirmation by text message, and by email too if you add an address.</p>
</div>
          <div class="field">
            <label for="phone">Phone</label>
            <input id="phone" name="phone" type="tel" autocomplete="tel" placeholder="+269 321 2345" [(ngModel)]="phone" required>
          </div>
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

  fullName = ''; email = ''; phone = ''; address = ''; method = 'MVola';
  deposit: number | null = null;
  busy = false;
  error = '';
  done: any = null;

  transactionId = '';
  noticeSent = false;
  noticeError = '';

  ngOnInit(): void {
    this.deposit = this.minDeposit;
    // use today's names and prices, and drop products that were removed from the shop
    this.api.list('products').subscribe({
      next: rows => { this.cart.sync(rows.map(toCartProduct)); this.deposit = this.minDeposit; }
    });
  }

  /** 40% of the total, rounded up to a whole franc (the API uses the same rule). */
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
    if (!this.depositOk) return;
    this.busy = true;
    this.error = '';
    this.api.create('checkout', {
      fullName: this.fullName, email: this.email, phone: this.phone,
      deliveryAddress: this.address, depositAmount: this.deposit, paymentMethod: this.method,
      items: this.cart.lines().map(l => ({ productId: l.productId, quantity: l.quantity }))
    }).subscribe({
      next: r => { this.done = r; this.busy = false; this.cart.clear(); },
      error: e => {
        this.busy = false;
        this.error = typeof e.error === 'string' && e.error
          ? e.error
          : 'The order could not be placed. Check your details and try again.';
      }
    });
  }

  sendNotice(): void {
    this.busy = true;
    this.noticeError = '';
    this.api.create(`checkout/${this.done.reference}/payment`, { email: this.email, phone: this.phone, transactionId: this.transactionId })
      .subscribe({
        next: () => { this.noticeSent = true; this.busy = false; },
        error: e => {
          this.busy = false;
          this.noticeError = typeof e.error === 'string' && e.error
            ? e.error
            : 'We could not save the transaction ID. Check it and try again.';
        }
      });
  }
}