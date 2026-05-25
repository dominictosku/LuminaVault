import { Directive, ElementRef, Input, OnDestroy, inject } from '@angular/core';
import { Subscription } from 'rxjs';
import { ProtectedMediaService } from '../core/protected-media.service';

@Directive({
  selector: 'img[appProtectedSrc]',
  standalone: true,
})
export class ProtectedMediaSrcDirective implements OnDestroy {
  private media = inject(ProtectedMediaService);
  private element = inject<ElementRef<HTMLImageElement>>(ElementRef);
  private sub?: Subscription;
  private objectUrl?: string;

  @Input('appProtectedSrc')
  set protectedSrc(url: string | null | undefined) {
    this.reset();
    if (!url) return;

    this.sub = this.media.objectUrl(url).subscribe({
      next: objectUrl => {
        this.objectUrl = objectUrl;
        this.element.nativeElement.src = objectUrl;
      },
      error: () => {
        this.element.nativeElement.removeAttribute('src');
      },
    });
  }

  ngOnDestroy() {
    this.reset();
  }

  private reset() {
    this.sub?.unsubscribe();
    this.sub = undefined;
    this.element.nativeElement.removeAttribute('src');
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = undefined;
    }
  }
}
