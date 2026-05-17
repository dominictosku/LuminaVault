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
import { CrudFormController } from '../../shared/crud-form/crud-form.controller';

@Component({
  selector: 'app-goals',
  imports: [FormsModule, CurrencyPipe, DecimalPipe, DatePipe, NgClass],
  templateUrl: './goals.html',
  providers: [CrudFormController],
})
export class GoalsComponent {
  private api = inject(FinanceApi);
  protected crud = inject<CrudFormController<SavingsGoalInput, SavingsGoal>>(CrudFormController);
  // Pass-through signals so the template keeps reading `editingId()`, `saving()`, `error()`
  // unchanged — the controller is an implementation detail of the component.
  protected editingId = this.crud.editingId;
  protected saving = this.crud.saving;
  protected error = this.crud.error;

  goals = signal<SavingsGoal[]>([]);
  accounts = signal<FinanceAccount[]>([]);
  loading = signal(true);
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
    this.crud.configure({
      create: input => this.api.createGoal(input),
      update: (id, input) => this.api.updateGoal(id, input),
      delete: id => this.api.deleteGoal(id),
      onSaved: () => { this.reset(); this.fetch(); },
      onRemoved: () => { this.reset(); this.fetch(); },
    });
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
    this.crud.startEdit(goal.id);
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
      this.crud.error.set('Goal name is required.');
      return;
    }
    const account = this.accounts().find(a => a.id === this.model.accountId);
    const input: SavingsGoalInput = {
      ...this.model,
      name: this.model.name.trim(),
      currency: (this.model.currency || account?.currency || 'CHF').toUpperCase(),
      targetAmount: Number(this.model.targetAmount) || 0,
      currentAmount: Number(this.model.currentAmount) || 0,
      targetDate: this.targetDateValue ? new Date(this.targetDateValue).toISOString() : null,
    };
    this.crud.save(input);
  }

  remove() {
    this.crud.remove({
      title: 'Delete goal?',
      message: 'This savings goal will be removed.',
      confirmText: 'Delete',
    });
  }

  onAccountChange() {
    const account = this.accounts().find(a => a.id === this.model.accountId);
    if (account) this.model.currency = account.currency;
  }

  reset() {
    this.crud.cancel();
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
