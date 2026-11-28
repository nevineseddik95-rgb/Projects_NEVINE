import { Pipe, PipeTransform, inject } from '@angular/core';
import { LanguageService } from './language.service';

@Pipe({ name: 'kmf', standalone: true, pure: false })
export class KmfPipe implements PipeTransform {
  private language = inject(LanguageService);

  transform(value: number | string | null | undefined): string {
    if (value === null || value === undefined || value === '') return '';
    const n = Number(value);
    if (isNaN(n)) return '';
    return new Intl.NumberFormat(this.language.locale(), { maximumFractionDigits: 0 }).format(n) + ' KMF';
  }
}