import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../../core/api';
import {
  AssetCategory,
  AssetCategoryInput,
  FinanceCategory,
  FinanceCategoryInput,
} from '../../core/models';

type SettingsTab = 'assets' | 'finance';
type EditableCategory = AssetCategory | FinanceCategory;
type CategoryInput = AssetCategoryInput | FinanceCategoryInput;

@Component({
  selector: 'app-settings',
  imports: [FormsModule],
  templateUrl: './settings.html',
  styleUrl: './settings.scss'
})
export class SettingsComponent {
  private api = inject(Api);
  tab = signal<SettingsTab>('assets');
  assetCategories = signal<AssetCategory[]>([]);
  financeCategories = signal<FinanceCategory[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  editingId = signal<number | null>(null);
  colors = ['#7c3aed', '#ec4899', '#06b6d4', '#22c55e', '#f59e0b', '#ef4444', '#94a3b8'];

  title = computed(() => this.tab() === 'assets'
    ? 'Asset categories'
    : 'Transaction & subscription categories');

  currentCategories = computed<EditableCategory[]>(() =>
    this.tab() === 'assets' ? this.assetCategories() : this.financeCategories());

  model: CategoryInput = this.defaultModel();

  constructor() {
    this.fetch();
  }

  setTab(tab: SettingsTab) {
    this.tab.set(tab);
    this.reset();
  }

  fetch() {
    this.loading.set(true);
    const asset$ = this.api.listAssetCategories();
    const finance$ = this.api.listFinanceCategories();
    asset$.subscribe({
      next: categories => {
        this.assetCategories.set(categories);
        finance$.subscribe({
          next: financeCategories => {
            this.financeCategories.set(financeCategories);
            this.loading.set(false);
          },
          error: () => this.loading.set(false),
        });
      },
      error: () => this.loading.set(false),
    });
  }

  newCategory() {
    this.reset();
  }

  editCategory(category: EditableCategory) {
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
    const op = this.tab() === 'assets'
      ? (this.editingId()
        ? this.api.updateAssetCategory(this.editingId()!, input)
        : this.api.createAssetCategory(input))
      : (this.editingId()
        ? this.api.updateFinanceCategory(this.editingId()!, input)
        : this.api.createFinanceCategory(input));
    op.subscribe({
      next: () => { this.saving.set(false); this.reset(); this.fetch(); },
      error: e => { this.saving.set(false); this.error.set(e?.error?.error ?? 'Save failed.'); },
    });
  }

  remove() {
    if (!this.editingId()) return;
    const label = this.tab() === 'assets' ? 'asset' : 'finance';
    if (!confirm(`Delete this ${label} category? Existing records keep their category text.`)) return;
    const op = this.tab() === 'assets'
      ? this.api.deleteAssetCategory(this.editingId()!)
      : this.api.deleteFinanceCategory(this.editingId()!);
    op.subscribe(() => {
      this.reset();
      this.fetch();
    });
  }

  reset() {
    this.editingId.set(null);
    this.error.set(null);
    this.model = this.defaultModel();
  }

  private defaultModel(): CategoryInput {
    return {
      name: '',
      color: '#7c3aed',
      sortOrder: this.tab() === 'assets' ? this.assetCategories().length : this.financeCategories().length,
    };
  }
}
