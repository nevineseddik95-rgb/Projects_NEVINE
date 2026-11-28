import { Pipe, PipeTransform, inject } from '@angular/core';
import { LanguageService } from './language.service';
import { TranslationParams } from './translations';

@Pipe({ name: 'translate', standalone: true, pure: false })
export class TranslatePipe implements PipeTransform {
  private language = inject(LanguageService);

  transform(key: string, params: TranslationParams = {}): string {
    return this.language.t(key, params);
  }
}
