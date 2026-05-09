import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../../core/api';
import { AssetCategory, AssetCategoryInput } from '../../core/models';

@Component({
  selector: 'app-settings',
  imports: [FormsModule],
  template: `
    <div class="p-6 xl:p-8 fade-in max-w-5xl">
      <div class="mb-6">
        <h1 class="text-2xl font-semibold tracking-tight">Settings</h1>
        <p class="text-slate-400 text-sm mt-1">Configure shared lists and app defaults.</p>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-[1fr_360px] gap-4">
        <section class="surface p-5">
          <div class="flex items-center justify-between mb-4">
            <h2 class="font-medium flex items-center gap-2">
              <i class="pi pi-tags text-violet-300"></i> Asset categories
            </h2>
            <button class="btn btn-primary" (click)="newCategory()">
              <i class="pi pi-plus"></i> Category
            </button>
          </div>

          @if (loading()) {
            <div class="space-y-2">
              @for (_ of [1,2,3,4]; track _) { <div class="surface-muted h-12 animate-pulse"></div> }
            </div>
          } @else if (categories().length === 0) {
            <div class="text-sm text-slate-500">No categories yet.</div>
          } @else {
            <div class="divide-y divide-white/5">
              @for (category of categories(); track category.id) {
                <button type="button" class="w-full py-3 flex items-center gap-3 text-left hover:bg-white/5 px-2 rounded-lg transition"
                        (click)="editCategory(category)">
                  <span class="w-3 h-3 rounded-sm shrink-0" [style.background]="category.color"></span>
                  <span class="font-medium flex-1">{{ category.name }}</span>
                  <span class="text-xs text-slate-500">#{{ category.sortOrder }}</span>
                </button>
              }
            </div>
          }
        </section>

        <aside class="surface p-5 h-fit">
          <h2 class="font-medium flex items-center gap-2 mb-4">
            <i class="pi pi-pen-to-square text-violet-300"></i>
            {{ editingId() ? 'Edit category' : 'New category' }}
          </h2>

          <form (ngSubmit)="save()" class="space-y-4">
            <div>
              <label class="label">Name</label>
              <input class="input" name="name" [(ngModel)]="model.name" required />
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="label">Color</label>
                <input class="input" name="color" [(ngModel)]="model.color" />
              </div>
              <div>
                <label class="label">Sort order</label>
                <input class="input" type="number" name="sortOrder" [(ngModel)]="model.sortOrder" />
              </div>
            </div>
            <div class="flex gap-2 flex-wrap">
              @for (color of colors; track color) {
                <button type="button" class="w-8 h-8 rounded-lg border border-white/10"
                        [style.background]="color"
                        [class.ring-2]="model.color === color"
                        [class.ring-violet-300]="model.color === color"
                        (click)="model.color = color">
                </button>
              }
            </div>
            @if (error()) {
              <div class="text-red-300 text-sm bg-red-500/10 border border-red-500/30 rounded-lg px-3 py-2">
                {{ error() }}
              </div>
            }
            <div class="flex gap-2">
              @if (editingId()) {
                <button type="button" class="btn btn-danger" (click)="remove()" [disabled]="saving()">
                  <i class="pi pi-trash"></i>
                </button>
              }
              <button type="button" class="btn btn-ghost flex-1 justify-center" (click)="reset()">Cancel</button>
              <button class="btn btn-primary flex-1 justify-center" type="submit" [disabled]="saving()">
                @if (saving()) { <i class="pi pi-spin pi-spinner"></i> } @else { <i class="pi pi-check"></i> }
                Save
              </button>
            </div>
          </form>
        </aside>
      </div>
    </div>
  `,
})
export class SettingsComponent {
  private api = inject(Api);
  categories = signal<AssetCategory[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  editingId = signal<number | null>(null);
  colors = ['#7c3aed', '#ec4899', '#06b6d4', '#22c55e', '#f59e0b', '#ef4444', '#94a3b8'];
  model: AssetCategoryInput = this.defaultModel();

  constructor() {
    this.fetch();
  }

  fetch() {
    this.loading.set(true);
    this.api.listAssetCategories().subscribe({
      next: categories => { this.categories.set(categories); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  newCategory() {
    this.reset();
  }

  editCategory(category: AssetCategory) {
    this.editingId.set(category.id);
    this.error.set(null);
    this.model = { name: category.name, color: category.color, sortOrder: category.sortOrder };
  }

  save() {
    if (!this.model.name.trim()) {
      this.error.set('Category name is required.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const input = {
      ...this.model,
      name: this.model.name.trim(),
      color: this.model.color || '#7c3aed',
      sortOrder: Number(this.model.sortOrder) || 0,
    };
    const op = this.editingId()
      ? this.api.updateAssetCategory(this.editingId()!, input)
      : this.api.createAssetCategory(input);
    op.subscribe({
      next: () => { this.saving.set(false); this.reset(); this.fetch(); },
      error: e => { this.saving.set(false); this.error.set(e?.error?.error ?? 'Save failed.'); },
    });
  }

  remove() {
    if (!this.editingId()) return;
    if (!confirm('Delete this asset category? Existing assets keep their category text.')) return;
    this.api.deleteAssetCategory(this.editingId()!).subscribe(() => {
      this.reset();
      this.fetch();
    });
  }

  reset() {
    this.editingId.set(null);
    this.error.set(null);
    this.model = this.defaultModel();
  }

  private defaultModel(): AssetCategoryInput {
    return {
      name: '',
      color: '#7c3aed',
      sortOrder: this.categories().length,
    };
  }
}
