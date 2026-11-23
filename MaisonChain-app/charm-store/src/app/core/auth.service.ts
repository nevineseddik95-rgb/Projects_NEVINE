import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private readonly key = 'charm_admin_token';
  private readonly roleKey = 'charm_admin_role';
  private readonly nameKey = 'charm_admin_name';

  get token(): string | null { return localStorage.getItem(this.key); }
  get role(): string | null { return localStorage.getItem(this.roleKey); }
  get username(): string { return localStorage.getItem(this.nameKey) ?? ''; }

  /** The server enforces this too. Here it only decides what the screens show. */
  isOwner(): boolean { return this.role === 'Owner'; }

  isLoggedIn(): boolean {
    const t = this.token;
    if (!t) return false;
    try {
      const exp = JSON.parse(atob(t.split('.')[1])).exp;   // JWT expiry, in seconds
      return !exp || exp * 1000 > Date.now();
    } catch { return true; }
  }

  login(username: string, password: string) {
    return this.http
      .post<{ token: string; role: string; username: string }>(`${environment.apiUrl}/auth/login`, { username, password })
      .pipe(tap(r => {
        localStorage.setItem(this.key, r.token);
        localStorage.setItem(this.roleKey, r.role);
        localStorage.setItem(this.nameKey, r.username);
      }));
  }

  logout(): void {
    localStorage.removeItem(this.key);
    localStorage.removeItem(this.roleKey);
    localStorage.removeItem(this.nameKey);
    this.router.navigate(['/admin/login']);
  }
}