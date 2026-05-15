import { ErrorHandler, Injectable, inject } from '@angular/core';
import { ToastService } from '../shared/toast/toast.service';

/// Catches uncaught errors from anywhere in the app (template bindings,
/// signal computeds, async tasks that reject). Without this, Angular zoneless
/// silently logs to the console and the user sees a blank/half-rendered page.
///
/// We re-log to the console so the dev tools surface the real stack, then surface
/// a generic toast so the user knows something went wrong rather than seeing
/// inconsistent UI state.
@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private toasts = inject(ToastService);

  handleError(error: unknown): void {
    console.error('Uncaught error:', error);
    // Skip HTTP errors here — auth.interceptor already shows toasts for those.
    if (this.looksLikeHttpError(error)) return;
    this.toasts.error('Something went wrong. The page may not be in sync — try refreshing.');
  }

  private looksLikeHttpError(error: unknown): boolean {
    return typeof error === 'object'
      && error !== null
      && 'status' in error
      && 'url' in error;
  }
}
