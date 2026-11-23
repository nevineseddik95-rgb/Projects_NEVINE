import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

export const authGuard: CanActivateFn = () => {
  if (!environment.requireLogin) return true;
  const auth = inject(AuthService);
  return auth.isLoggedIn() || inject(Router).createUrlTree(['/admin/login']);
};

/** only the owner may open the Admins screen */
export const ownerGuard: CanActivateFn = () =>
  inject(AuthService).isOwner() || inject(Router).createUrlTree(['/admin/dashboard']);