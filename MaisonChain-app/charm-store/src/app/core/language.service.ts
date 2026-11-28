import { DOCUMENT } from '@angular/common';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { Language, TranslationParams, initialLanguage, translate, translateApiMessage } from './translations';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private document = inject(DOCUMENT);
  readonly language = signal<Language>(initialLanguage());
  readonly locale = computed(() => this.language() === 'fr' ? 'fr-FR' : 'en-US');

  constructor() {
    effect(() => {
      const language = this.language();
      this.document.documentElement.lang = language;
      try {
        localStorage.setItem('charm_language', language);
      } catch {
        return;
      }
    });
  }

  setLanguage(language: Language): void {
    this.language.set(language);
  }

  toggle(): void {
    this.setLanguage(this.language() === 'en' ? 'fr' : 'en');
  }

  t(key: string, params: TranslationParams = {}): string {
    return translate(this.language(), key, params);
  }

  apiMessage(message: string): string {
    return translateApiMessage(this.language(), message);
  }
}
