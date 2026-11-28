import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { LanguageService } from '../../core/language.service';
import { LanguageSwitcherComponent } from '../../core/language-switcher.component';
import { TranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, LanguageSwitcherComponent, TranslatePipe],
  template: `
    <section class="login">
      <div class="login-art" aria-hidden="true">
        <p class="login-mark">Beauty & Shine</p>
        <p class="login-line">{{ 'login.tagline' | translate }}</p>
      </div>
      <form class="login-card" #f="ngForm" (ngSubmit)="submit()">
        <app-language-switcher />
        <h1>{{ 'login.title' | translate }}</h1>
        <div class="field">
          <label for="username">{{ 'login.username' | translate }}</label>
          <input id="username" name="username" autocomplete="username" [(ngModel)]="username" required>
        </div>
        <div class="field">
          <label for="password">{{ 'login.password' | translate }}</label>
          <input id="password" name="password" type="password" autocomplete="current-password" [(ngModel)]="password" required>
        </div>
        @if (error) { <p class="alert" role="alert">{{ error }}</p> }
        <button class="btn btn-primary wide" type="submit" [disabled]="f.invalid || busy">{{ busy ? ('login.signingIn' | translate) : ('login.signIn' | translate) }}</button>
      </form>
    </section>
  `
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  private language = inject(LanguageService);
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
          ? this.language.t('login.invalid')
          : this.language.t('login.serverError');
      }
    });
  }
}
