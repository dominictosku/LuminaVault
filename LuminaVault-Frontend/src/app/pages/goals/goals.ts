import { CurrencyPipe, DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { FinanceApi } from '../../core/data-access/finance-api';
import {
  FinanceAccount,
  SAVINGS_GOAL_STATUSES,
  SavingsGoal,
  SavingsGoalInput,
  SavingsGoalStatus,
} from '../../core/models';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';

@Component({
  selector: 'app-goals',
  imports: [FormsModule, CurrencyPipe, DecimalPipe, DatePipe, NgClass],
  templateUrl: './goals.html',
})
export class GoalsComponent {
  private api = inject(FinanceApi);
  private confirmDialog = inject(ConfirmDialogService);

  goals = signal<SavingsGoal[]>([]);
  accounts = signal<FinanceAccount[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  editingId = signal<number | null>(null);
  includeInactive = signal(false);
  targetDateValue = '';
  statuses = SAVINGS_GOAL_STATUSES;
  model: SavingsGoalInput = this.defaultModel();

  activeGoals = computed(() => this.goals().filter(g => g.status === 'Active'));
  totals = computed(() => {
    const goals = this.activeGoals();
    const target = goals.reduce((sum, g) => sum + g.targetAmount, 0);
    const current = goals.reduce((sum, g) => sum + g.currentAmount, 0);
    return {
      target,
      current,
      remaining: Math.max(0, target - current),
      progress: target <= 0 ? 0 : (current / target) * 100,
    };
  });

  constructor() {
    this.fetch();
  }

  fetch() {
    this.loading.set(true);
    forkJoin({
      goals: this.api.listGoals(this.includeInactive()),
      accounts: this.api.listFinanceAccounts(),
    }).subscribe({
      next: r => {
        this.goals.set(r.goals);
        this.accounts.set(r.accounts);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  toggleInactive(value: boolean) {
    this.includeInactive.set(value);
    this.fetch();
  }

  edit(goal: SavingsGoal) {
    this.editingId.set(goal.id);
    this.error.set(null);
    this.targetDateValue = goal.targetDate ? goal.targetDate.substring(0, 10) : '';
    this.model = {
      name: goal.name,
      accountId: goal.accountId ?? null,
      currency: goal.currency,
      targetAmount: goal.targetAmount,
      currentAmount: goal.currentAmount,
      targetDate: goal.targetDate,
      status: goal.status,
      notes: goal.notes ?? '',
    };
  }

  save() {
    if (!this.model.name.trim()) {
      this.error.set('Goal name is required.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const account = this.accounts().find(a => a.id === this.model.accountId);
    const input: SavingsGoalInput = {
      ...this.model,
      name: this.model.name.trim(),
      currency: (this.model.currency || account?.currency || 'CHF').toUpperCase(),
      targetAmount: Number(this.model.targetAmount) || 0,
      currentAmount: Number(this.model.currentAmount) || 0,
      targetDate: this.targetDateValue ? new Date(this.targetDateValue).toISOString() : null,
    };
    const op = this.editingId()
      ? this.api.updateGoal(this.editingId()!, input)
      : this.api.createGoal(input);
    op.subscribe({
      next: () => { this.saving.set(false); this.reset(); this.fetch(); },
      error: e => { this.saving.set(false); this.error.set(e?.error?.error ?? 'Save failed.'); },
    });
  }

  async remove() {
    if (!this.editingId()) return;
    const confirmed = await this.confirmDialog.confirm({
      title: 'Delete goal?',
      message: 'This savings goal will be removed.',
      confirmText: 'Delete',
    });
    if (!confirmed) return;
    this.api.deleteGoal(this.editingId()!).subscribe(() => {
      this.reset();
      this.fetch();
    });
  }

  onAccountChange() {
    const account = this.accounts().find(a => a.id === this.model.accountId);
    if (account) this.model.currency = account.currency;
  }

  reset() {
    this.editingId.set(null);
    this.error.set(null);
    this.targetDateValue = '';
    this.model = this.defaultModel();
  }

  progressWidth(goal: SavingsGoal) {
    return Math.min(100, Math.max(3, goal.progressPercent));
  }

  statusClass(status: SavingsGoalStatus) {
    return {
      'text-emerald-300': status === 'Active' || status === 'Achieved',
      'text-amber-200': status === 'Paused',
      'text-slate-500': status === 'Cancelled',
    };
  }

  private defaultModel(): SavingsGoalInput {
    return {
      name: '',
      accountId: null,
      currency: 'CHF',
      targetAmount: 0,
      currentAmount: 0,
      targetDate: null,
      status: 'Active',
      notes: '',
    };
  }
}
