import { Component, inject } from '@angular/core';
import { LanguageService } from './language.service';
import { TranslatePipe } from './translate.pipe';

@Component({
  selector: 'app-language-switcher',
  standalone: true,
  imports: [TranslatePipe],
  template: `
    <div class="language-switch" role="group" [attr.aria-label]="'language.label' | translate">
      <button type="button" [class.on]="language.language() === 'en'"
              [attr.aria-pressed]="language.language() === 'en'"
              [attr.aria-label]="'language.english' | translate"
              (click)="language.setLanguage('en')">EN</button>
      <button type="button" [class.on]="language.language() === 'fr'"
              [attr.aria-pressed]="language.language() === 'fr'"
              [attr.aria-label]="'language.french' | translate"
              (click)="language.setLanguage('fr')">FR</button>
    </div>
  `
})
export class LanguageSwitcherComponent {
  language = inject(LanguageService);
}
