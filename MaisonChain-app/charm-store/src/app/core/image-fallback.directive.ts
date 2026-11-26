import { Directive, ElementRef, HostListener, inject } from '@angular/core';
import { environment } from '../../environments/environment';

@Directive({
  selector: 'img[appImageFallback]',
  standalone: true
})
export class ImageFallbackDirective {
  private readonly image = inject<ElementRef<HTMLImageElement>>(ElementRef);

  @HostListener('error')
  onError(): void {
    if (this.image.nativeElement.dataset.fallback === 'true') return;
    this.image.nativeElement.dataset.fallback = 'true';
    this.image.nativeElement.src = `${environment.filesUrl}/placeholder.svg`;
  }

  @HostListener('load')
  onLoad(): void {
    delete this.image.nativeElement.dataset.fallback;
  }
}
