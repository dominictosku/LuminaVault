import { Injectable, signal } from '@angular/core';

export interface ConfirmDialogOptions {
  title?: string;
  message: string;
  detail?: string;
  confirmText?: string;
  cancelText?: string;
  tone?: 'danger' | 'default';
}

interface ConfirmDialogRequest extends Required<Omit<ConfirmDialogOptions, 'detail'>> {
  detail?: string;
}

@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  private resolver: ((confirmed: boolean) => void) | null = null;
  readonly request = signal<ConfirmDialogRequest | null>(null);

  confirm(options: ConfirmDialogOptions) {
    this.resolver?.(false);
    this.request.set({
      title: options.title ?? 'Confirm action',
      message: options.message,
      detail: options.detail,
      confirmText: options.confirmText ?? 'Delete',
      cancelText: options.cancelText ?? 'Cancel',
      tone: options.tone ?? 'danger',
    });

    return new Promise<boolean>(resolve => {
      this.resolver = resolve;
    });
  }

  close(confirmed: boolean) {
    this.request.set(null);
    this.resolver?.(confirmed);
    this.resolver = null;
  }
}
