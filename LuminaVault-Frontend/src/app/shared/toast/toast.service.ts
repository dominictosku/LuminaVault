import { Injectable, signal } from '@angular/core';

export type ToastKind = 'success' | 'error' | 'warning' | 'info';

export interface Toast {
  id: number;
  kind: ToastKind;
  message: string;
}

/// Tiny signal-driven toast bus. The shell mounts a single outlet that subscribes
/// to `toasts()`; anyone with an injection context can `show()` from anywhere
/// (HTTP interceptors, the global ErrorHandler, page components).
///
/// Deliberately not wrapped in PrimeNG's MessageService — we have one consumer
/// pattern (transient notifications), no need for the full feature surface.
@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  readonly toasts = signal<Toast[]>([]);

  show(kind: ToastKind, message: string, ttlMs = 5000) {
    const id = this.nextId++;
    this.toasts.update(t => [...t, { id, kind, message }]);
    if (ttlMs > 0) setTimeout(() => this.dismiss(id), ttlMs);
  }

  success(message: string) { this.show('success', message, 3000); }
  error(message: string) { this.show('error', message); }
  warning(message: string) { this.show('warning', message); }
  info(message: string) { this.show('info', message); }

  dismiss(id: number) {
    this.toasts.update(t => t.filter(x => x.id !== id));
  }
}
