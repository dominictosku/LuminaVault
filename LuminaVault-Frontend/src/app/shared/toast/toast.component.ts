import { NgClass } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ToastKind, ToastService } from './toast.service';

@Component({
  selector: 'app-toast-outlet',
  imports: [NgClass],
  template: `
    <div class="fixed top-4 right-4 z-[60] flex flex-col gap-2 pointer-events-none max-w-[90vw] sm:max-w-md">
      @for (toast of toasts.toasts(); track toast.id) {
        <div [ngClass]="toneClass(toast.kind)"
             class="pointer-events-auto flex items-start gap-3 px-4 py-3 rounded-lg shadow-lg border backdrop-blur-md text-sm">
          <i [ngClass]="iconFor(toast.kind)" class="mt-0.5"></i>
          <div class="flex-1 break-words">{{ toast.message }}</div>
          <button type="button" (click)="toasts.dismiss(toast.id)"
                  class="opacity-60 hover:opacity-100" aria-label="Dismiss">
            <i class="pi pi-times text-xs"></i>
          </button>
        </div>
      }
    </div>
  `,
})
export class ToastOutletComponent {
  protected toasts = inject(ToastService);

  protected toneClass(kind: ToastKind) {
    return {
      'bg-emerald-950/80 border-emerald-500/40 text-emerald-100': kind === 'success',
      'bg-red-950/80 border-red-500/40 text-red-100': kind === 'error',
      'bg-amber-950/80 border-amber-500/40 text-amber-100': kind === 'warning',
      'bg-slate-900/80 border-slate-500/40 text-slate-100': kind === 'info',
    };
  }

  protected iconFor(kind: ToastKind) {
    return {
      'pi pi-check-circle text-emerald-300': kind === 'success',
      'pi pi-exclamation-circle text-red-300': kind === 'error',
      'pi pi-exclamation-triangle text-amber-300': kind === 'warning',
      'pi pi-info-circle text-slate-300': kind === 'info',
    };
  }
}
