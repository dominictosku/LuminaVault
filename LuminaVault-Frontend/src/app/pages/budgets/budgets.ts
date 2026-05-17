import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { FinanceApi } from '../../core/data-access/finance-api';
import { SettingsApi } from '../../core/data-access/settings-api';
import { FinanceBudget, FinanceBudgetInput, FinanceBudgetOverview, FinanceCategory } from '../../core/models';
import { CrudFormController } from '../../shared/crud-form/crud-form.controller';

@Component({
  selector: 'app-budgets',
  imports: [FormsModule, CurrencyPipe, DecimalPipe],
  templateUrl: './budgets.html',
  styleUrl: './budgets.scss',
  providers: [CrudFormController],
})
export class BudgetsComponent {
  private finance = inject(FinanceApi);
  private settings = inject(SettingsApi);
  protected crud = inject<CrudFormController<FinanceBudgetInput, FinanceBudget>>(CrudFormController);
  protected editingId = this.crud.editingId;
  protected saving = this.crud.saving;
  protected error = this.crud.error;

  month = signal(new Date().toISOString().substring(0, 7));
  overview = signal<FinanceBudgetOverview | null>(null);
  categories = signal<FinanceCategory[]>([]);
  loading = signal(true);
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
    this.crud.configure({
      create: input => this.finance.createBudget(input),
      update: (id, input) => this.finance.updateBudget(id, input),
      delete: id => this.finance.deleteBudget(id),
      onSaved: () => { this.reset(); this.fetch(); },
      onRemoved: () => { this.reset(); this.fetch(); },
    });
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
    this.crud.startEdit(row.id);
    this.model = {
      category: row.category,
      month: row.month,
      limitAmount: row.limitAmount,
      notes: row.notes ?? '',
    };
  }

  save() {
    if (!this.model.category) {
      this.crud.error.set('Choose a category.');
      return;
    }
    const input: FinanceBudgetInput = {
      ...this.model,
      month: this.monthStartIso(),
      limitAmount: Number(this.model.limitAmount) || 0,
    };
    this.crud.save(input);
  }

  remove() {
    this.crud.remove({
      title: 'Delete budget?',
      message: 'This category budget will be removed for the selected month.',
      confirmText: 'Delete',
    });
  }

  reset() {
    this.crud.cancel();
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
