import { Injectable, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { ConfirmDialogOptions, ConfirmDialogService } from '../confirm-dialog/confirm-dialog.service';

export interface CrudFormConfig<TInput, TEntity> {
  /// POST-style create. Called when there's no `editingId`.
  create: (input: TInput) => Observable<TEntity>;
  /// PUT-style update. Called when `editingId` is set.
  update: (id: number, input: TInput) => Observable<TEntity>;
  /// DELETE-style remove. Called when `editingId` is set + user confirms.
  delete: (id: number) => Observable<unknown>;
  /// Fallback error message shown when the server's `{error: "..."}` envelope is missing.
  saveErrorFallback?: string;
  /// Called after a successful create or update — usually `() => { reset(); fetch(); }`.
  onSaved?: (entity: TEntity) => void;
  /// Called after a successful delete — usually `() => { reset(); fetch(); }`.
  onRemoved?: () => void;
}

/// Per-page controller that owns the saving/error/editingId boilerplate every CRUD form
/// duplicates: branching create-vs-update inside save(), the confirm-then-delete pattern
/// in remove(), error envelope unwrapping, and resetting state after success.
///
/// Components keep their own validation, model→input mapping, and refetch logic — those
/// vary too much to generalize. The controller just runs the universal plumbing.
///
/// Usage in a page component:
///   private crud = inject(CrudFormController<LoanInput, Loan>);
///   constructor() {
///     this.crud.configure({
///       create: input => this.api.createLoan(input),
///       update: (id, input) => this.api.updateLoan(id, input),
///       delete: id => this.api.deleteLoan(id),
///       onSaved: () => { this.reset(); this.fetch(); },
///       onRemoved: () => { this.reset(); this.fetch(); },
///     });
///   }
///   save() { if (!validate()) return; this.crud.save(buildInput()); }
///   remove() { this.crud.remove({ title: 'Delete loan?', message: '...', confirmText: 'Delete' }); }
@Injectable()
export class CrudFormController<TInput, TEntity> {
  private confirmDialog = inject(ConfirmDialogService);
  readonly editingId = signal<number | null>(null);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  private config!: CrudFormConfig<TInput, TEntity>;

  configure(config: CrudFormConfig<TInput, TEntity>) {
    this.config = config;
  }

  startEdit(id: number) {
    this.editingId.set(id);
    this.error.set(null);
  }

  cancel() {
    this.editingId.set(null);
    this.error.set(null);
    this.saving.set(false);
  }

  save(input: TInput) {
    this.saving.set(true);
    this.error.set(null);
    const id = this.editingId();
    const op = id != null ? this.config.update(id, input) : this.config.create(input);
    op.subscribe({
      next: entity => {
        this.saving.set(false);
        this.config.onSaved?.(entity);
      },
      error: e => {
        this.saving.set(false);
        this.error.set(this.extractError(e) ?? this.config.saveErrorFallback ?? 'Save failed.');
      },
    });
  }

  async remove(confirm: ConfirmDialogOptions) {
    const id = this.editingId();
    if (id == null) return;
    const confirmed = await this.confirmDialog.confirm(confirm);
    if (!confirmed) return;
    this.config.delete(id).subscribe(() => {
      this.cancel();
      this.config.onRemoved?.();
    });
  }

  private extractError(e: unknown): string | null {
    if (e && typeof e === 'object' && 'error' in e) {
      const err = (e as { error: unknown }).error;
      if (err && typeof err === 'object' && 'error' in err && typeof (err as { error: unknown }).error === 'string') {
        return (err as { error: string }).error;
      }
    }
    return null;
  }
}
