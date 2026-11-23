import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="login">
      <div class="login-art" aria-hidden="true">
        <p class="login-mark">Beauty & Shine</p>
        <p class="login-line">Bracelets, necklaces ,keychains and MORE!, kept in order.</p>
      </div>
      <form class="login-card" #f="ngForm" (ngSubmit)="submit()">
        <h1>Admin sign in</h1>
        <div class="field">
          <label for="username">Username</label>
          <input id="username" name="username" autocomplete="username" [(ngModel)]="username" required>
        </div>
        <div class="field">
          <label for="password">Password</label>
          <input id="password" name="password" type="password" autocomplete="current-password" [(ngModel)]="password" required>
        </div>
        @if (error) { <p class="alert" role="alert">{{ error }}</p> }
        <button class="btn btn-primary wide" type="submit" [disabled]="f.invalid || busy">{{ busy ? 'Signing in…' : 'Sign in' }}</button>
      </form>
    </section>
  `
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  username = '';
  password = '';
  busy = false;
  error = '';

  submit(): void {
    this.busy = true;
    this.error = '';
    this.auth.login(this.username, this.password).subscribe({
      next: () => this.router.navigate(['/admin/dashboard']),
      error: e => {
        this.busy = false;
        this.error = e.status === 401 || e.status === 400
          ? 'The username or password is not right. Try again.'
          : 'The server could not be reached. Check that the API is running.';
      }
    });
  }
}
