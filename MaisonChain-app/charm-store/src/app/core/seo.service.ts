import { DOCUMENT } from '@angular/common';
import { DestroyRef, Injectable, effect, inject } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { environment } from '../../environments/environment';
import { LanguageService } from './language.service';

export interface SeoProduct {
  id: string;
  model: string;
  genre: string;
  color: string;
  size: string;
  price: number;
  imageUrl: string;
}

@Injectable({ providedIn: 'root' })
export class SeoService {
  private document = inject(DOCUMENT);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);
  private language = inject(LanguageService);

  private path = '/';
  private product: SeoProduct | null = null;
  private productPath = '';
  private notFound = false;

  constructor() {
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(event => {
      this.path = event.urlAfterRedirects.split('?')[0] || '/';
      this.notFound = false;
      if (this.path !== this.productPath) {
        this.product = null;
        this.productPath = '';
      }
      this.apply();
    });

    effect(() => {
      this.language.language();
      this.apply();
    });
  }

  setProduct(product: SeoProduct): void {
    this.product = product;
    this.productPath = this.path;
    this.notFound = false;
    this.apply();
  }

  setNotFound(): void {
    this.product = null;
    this.productPath = '';
    this.notFound = true;
    this.apply();
  }

  private apply(): void {
    const french = this.language.language() === 'fr';
    const privatePage = this.path.startsWith('/admin') || ['/cart', '/checkout', '/favorites'].includes(this.path);
    const canonical = new URL(this.path, environment.siteUrl).toString();
    const defaultImage = new URL('/placeholder.svg', environment.siteUrl).toString();
    let title = french ? 'Beauty & Shine | Bijoux et accessoires au Cameroun' : 'Beauty & Shine | Jewelry & Accessories in Cameroon';
    let description = french
      ? 'Découvrez bracelets, colliers, porte-clés et accessoires Beauty & Shine au Cameroun. Réservez avec 40 % d’acompte et payez par MVola.'
      : 'Shop bracelets, necklaces, keychains and accessories at Beauty & Shine in Cameroon. Reserve your piece with a 40% deposit and pay by MVola.';
    let image = defaultImage;
    let type = 'website';
    let robots = privatePage || this.notFound ? 'noindex, nofollow' : 'index, follow, max-image-preview:large';

    if (privatePage) {
      title = french ? 'Beauty & Shine | Administration' : 'Beauty & Shine | Admin';
      description = french ? 'Espace d’administration Beauty & Shine.' : 'Beauty & Shine administration area.';
    } else if (this.path.startsWith('/product/')) {
      type = 'product';
      if (this.product) {
        title = `${this.product.model} | Beauty & Shine`;
        description = french
          ? `Découvrez ${this.product.model}, ${this.product.genre || 'un bijou'} Beauty & Shine. ${this.product.color}${this.product.size ? `, taille ${this.product.size}` : ''}. Prix : ${this.product.price} KMF.`
          : `Discover ${this.product.model}, a ${this.product.genre || 'jewelry'} piece from Beauty & Shine. ${this.product.color}${this.product.size ? `, size ${this.product.size}` : ''}. Price: ${this.product.price} KMF.`;
        image = this.absoluteImage(this.product.imageUrl);
      } else {
        title = french ? 'Produit | Beauty & Shine' : 'Product | Beauty & Shine';
        description = french ? 'Découvrez ce bijou Beauty & Shine.' : 'Discover this Beauty & Shine jewelry piece.';
      }
    } else if (this.path !== '/') {
      robots = 'noindex, nofollow';
    }

    this.setTitle(title);
    this.setMeta('name', 'description', description);
    this.setMeta('name', 'robots', robots);
    this.setMeta('property', 'og:site_name', 'Beauty & Shine');
    this.setMeta('property', 'og:type', type);
    this.setMeta('property', 'og:title', title);
    this.setMeta('property', 'og:description', description);
    this.setMeta('property', 'og:url', canonical);
    this.setMeta('property', 'og:image', image);
    this.setMeta('property', 'og:locale', french ? 'fr_FR' : 'en_US');
    this.setMeta('name', 'twitter:card', 'summary_large_image');
    this.setMeta('name', 'twitter:title', title);
    this.setMeta('name', 'twitter:description', description);
    this.setMeta('name', 'twitter:image', image);
    this.setCanonical(canonical);
    this.setProductStructuredData();
  }

  private setTitle(value: string): void {
    this.document.title = value;
  }

  private setMeta(attribute: 'name' | 'property', name: string, content: string): void {
    const selector = `meta[${attribute}="${name}"]`;
    let element = this.document.querySelector<HTMLMetaElement>(selector);
    if (!element) {
      element = this.document.createElement('meta');
      element.setAttribute(attribute, name);
      this.document.head.appendChild(element);
    }
    element.content = content;
  }

  private setCanonical(value: string): void {
    let element = this.document.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!element) {
      element = this.document.createElement('link');
      element.rel = 'canonical';
      this.document.head.appendChild(element);
    }
    element.href = value;
  }

  private setProductStructuredData(): void {
    const element = this.document.getElementById('product-structured-data');
    if (!element) return;
    if (!this.product || !this.path.startsWith('/product/')) {
      element.textContent = '{}';
      return;
    }
    const data = {
      '@context': 'https://schema.org',
      '@type': 'Product',
      name: this.product.model,
      description: this.document.querySelector<HTMLMetaElement>('meta[name="description"]')?.content ?? '',
      image: this.absoluteImage(this.product.imageUrl),
      sku: this.product.id,
      brand: { '@type': 'Brand', name: 'Beauty & Shine' },
      offers: {
        '@type': 'Offer',
        priceCurrency: 'KMF',
        price: this.product.price,
        availability: 'https://schema.org/InStock',
        url: new URL(this.path, environment.siteUrl).toString()
      }
    };
    element.textContent = JSON.stringify(data).replace(/</g, '\\u003c');
  }

  private absoluteImage(path: string): string {
    if (!path) return new URL('/placeholder.svg', environment.siteUrl).toString();
    return /^https?:\/\//i.test(path) ? path : new URL(path.startsWith('/') ? path : `/${path}`, environment.siteUrl).toString();
  }
}
