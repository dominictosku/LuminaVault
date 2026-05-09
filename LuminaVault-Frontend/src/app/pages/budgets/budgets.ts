import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { FinanceApi } from '../../core/data-access/finance-api';
import { SettingsApi } from '../../core/data-access/settings-api';
import { FinanceBudget, FinanceBudgetInput, FinanceBudgetOverview, FinanceCategory } from '../../core/models';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';

@Component({
  selector: 'app-budgets',
  imports: [FormsModule, CurrencyPipe, DecimalPipe],
  templateUrl: './budgets.html',
  styleUrl: './budgets.scss',
})
export class BudgetsComponent {
  private finance = inject(FinanceApi);
  private settings = inject(SettingsApi);
  private confirmDialog = inject(ConfirmDialogService);

  month = signal(new Date().toISOString().substring(0, 7));
  overview = signal<FinanceBudgetOverview | null>(null);
  categories = signal<FinanceCategory[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  editingId = signal<number | null>(null);
  model: FinanceBudgetInput = this.defaultModel();

  categoryNames = computed(() => {
    return Array.from(new Set([
      ...this.categories().map(c => c.name),
      ...(this.overview()?.rows.map(r => r.category) ?? []),
      this.model.category,
      'Bank summaries',
    ].filter(Boolean))).sort();
  });

  constructor() {
    this.fetch();
  }

  fetch() {
    this.loading.set(true);
    forkJoin({
      overview: this.finance.budgetOverview(this.monthStartIso()),
      categories: this.settings.listFinanceCategories(),
    }).subscribe({
      next: r => {
        this.overview.set(r.overview);
        this.categories.set(r.categories);
        if (!this.model.category && r.categories[0]) this.model.category = r.categories[0].name;
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onMonthChange(value: string) {
    this.month.set(value);
    this.reset();
    this.fetch();
  }

  edit(row: FinanceBudget) {
    this.editingId.set(row.id);
    this.error.set(null);
    this.model = {
      category: row.category,
      month: row.month,
      limitAmount: row.limitAmount,
      notes: row.notes ?? '',
    };
  }

  save() {
    if (!this.model.category) {
      this.error.set('Choose a category.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const input: FinanceBudgetInput = {
      ...this.model,
      month: this.monthStartIso(),
      limitAmount: Number(this.model.limitAmount) || 0,
    };
    const op = this.editingId()
      ? this.finance.updateBudget(this.editingId()!, input)
      : this.finance.createBudget(input);
    op.subscribe({
      next: () => { this.saving.set(false); this.reset(); this.fetch(); },
      error: e => { this.saving.set(false); this.error.set(e?.error?.error ?? 'Save failed.'); },
    });
  }

  async remove() {
    if (!this.editingId()) return;
    const confirmed = await this.confirmDialog.confirm({
      title: 'Delete budget?',
      message: 'This category budget will be removed for the selected month.',
      confirmText: 'Delete',
    });
    if (!confirmed) return;
    this.finance.deleteBudget(this.editingId()!).subscribe(() => {
      this.reset();
      this.fetch();
    });
  }

  reset() {
    this.editingId.set(null);
    this.error.set(null);
    this.model = this.defaultModel();
    if (this.categories()[0]) this.model.category = this.categories()[0].name;
  }

  usageWidth(row: FinanceBudget) {
    return Math.min(100, Math.max(3, row.usedPercent));
  }

  private monthStartIso() {
    const [year, month] = this.month().split('-').map(Number);
    return new Date(Date.UTC(year, month - 1, 1)).toISOString();
  }

  private defaultModel(): FinanceBudgetInput {
    return {
      category: '',
      month: this.monthStartIso(),
      limitAmount: 0,
      notes: '',
    };
  }
}
