import { Routes } from '@angular/router';
import { authGuard, ownerGuard } from './core/auth.guard'
import { PRODUCTS, GENRES, COLORS, SIZES, COMMANDS, CUSTOMERS, ADMINS } from './admin/entities';

const crud = () => import('./admin/crud/crud-page.component').then(m => m.CrudPageComponent);

export const routes: Routes = [
  { path: '', loadComponent: () => import('./shop/shop.component').then(m => m.ShopComponent) },
  { path: 'product/:id', loadComponent: () => import('./shop/product.component').then(m => m.ProductComponent) },
  { path: 'cart', loadComponent: () => import('./shop/cart.component').then(m => m.CartComponent) },
  { path: 'checkout', loadComponent: () => import('./shop/checkout.component').then(m => m.CheckoutComponent) },
  { path: 'admin/login', loadComponent: () => import('./admin/login/login.component').then(m => m.LoginComponent) },
  {
    path: 'admin',
    canActivate: [authGuard],
    loadComponent: () => import('./admin/admin-layout.component').then(m => m.AdminLayoutComponent),
    children: [
      { path: 'admins', canActivate: [ownerGuard], loadComponent: crud, data: { config: ADMINS } },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', loadComponent: () => import('./admin/dashboard/dashboard.component').then(m => m.DashboardComponent) },
      { path: 'products',  loadComponent: crud, data: { config: PRODUCTS } },
      { path: 'orders',    loadComponent: crud, data: { config: COMMANDS } },
      { path: 'commissions', loadComponent: () => import('./admin/commissions.component').then(m => m.CommissionsComponent) },
      { path: 'customers', loadComponent: crud, data: { config: CUSTOMERS } },
      { path: 'genres',    loadComponent: crud, data: { config: GENRES } },
      { path: 'colors',    loadComponent: crud, data: { config: COLORS } },
      { path: 'sizes',     loadComponent: crud, data: { config: SIZES } }
    ]
  },
  { path: 'favorites', loadComponent: () => import('./shop/favorites.component').then(m => m.FavoritesComponent) },
  { path: '**', redirectTo: '' }
];