import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'kmf', standalone: true })
export class KmfPipe implements PipeTransform {
  transform(value: number | string | null | undefined): string {
    if (value === null || value === undefined || value === '') return '';
    const n = Number(value);
    if (isNaN(n)) return '';
    return new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 0 }).format(n) + ' KMF';
  }
}