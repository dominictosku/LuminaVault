import { CurrencyPipe, DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { FinanceApi } from '../../core/data-access/finance-api';
import {
  FinanceAccount,
  Loan,
  LoanInput,
  LoanSchedule,
  LoanStatus,
  LOAN_STATUSES,
} from '../../core/models';
import { CrudFormController } from '../../shared/crud-form/crud-form.controller';

@Component({
  selector: 'app-loans',
  imports: [FormsModule, CurrencyPipe, DecimalPipe, DatePipe, NgClass],
  templateUrl: './loans.html',
  providers: [CrudFormController],
})
export class LoansComponent {
  private api = inject(FinanceApi);
  protected crud = inject<CrudFormController<LoanInput, Loan>>(CrudFormController);
  protected editingId = this.crud.editingId;
  protected saving = this.crud.saving;
  protected error = this.crud.error;

  loans = signal<Loan[]>([]);
  accounts = signal<FinanceAccount[]>([]);
  schedule = signal<LoanSchedule | null>(null);
  loading = signal(true);
  scheduleLoading = signal(false);
  selectedScheduleId = signal<number | null>(null);
  includeClosed = signal(false);
  startDateValue = '';
  statuses = LOAN_STATUSES;
  model: LoanInput = this.defaultModel();

  activeLoans = computed(() => this.loans().filter(l => l.status === 'Active'));
  totals = computed(() => {
    const loans = this.activeLoans();
    return {
      balance: loans.reduce((sum, l) => sum + l.currentBalance, 0),
      principal: loans.reduce((sum, l) => sum + l.principal, 0),
      monthly: loans.reduce((sum, l) => sum + l.monthlyPayment, 0),
      interest: loans.reduce((sum, l) => sum + l.totalInterest, 0),
    };
  });
  baseCurrency = computed(() => this.activeLoans()[0]?.currency ?? 'CHF');

  constructor() {
    this.crud.configure({
      create: input => this.api.createLoan(input),
      update: (id, input) => this.api.updateLoan(id, input),
      delete: id => this.api.deleteLoan(id),
      toastSubject: 'Loan',
      // Loans page refreshes its open schedule panel after a save so the chart reflects
      // the new principal/rate/extra-payment values, not just the list row.
      onSaved: () => {
        const refreshId = this.selectedScheduleId();
        this.reset();
        this.fetch();
        if (refreshId) this.api.loanSchedule(refreshId).subscribe(s => this.schedule.set(s));
      },
      onRemoved: () => {
        this.reset();
        this.selectedScheduleId.set(null);
        this.schedule.set(null);
        this.fetch();
      },
    });
    this.fetch();
  }

  fetch() {
    this.loading.set(true);
    forkJoin({
      loans: this.api.listLoans(this.includeClosed()),
      accounts: this.api.listFinanceAccounts(),
    }).subscribe({
      next: r => {
        this.loans.set(r.loans);
        this.accounts.set(r.accounts);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  toggleClosed(value: boolean) {
    this.includeClosed.set(value);
    this.fetch();
  }

  edit(loan: Loan) {
    this.crud.startEdit(loan.id);
    this.startDateValue = loan.startDate.substring(0, 10);
    this.model = {
      name: loan.name,
      lender: loan.lender ?? '',
      accountId: loan.accountId ?? null,
      currency: loan.currency,
      principal: loan.principal,
      annualInterestRate: loan.annualInterestRate,
      termMonths: loan.termMonths,
      startDate: loan.startDate,
      extraMonthlyPayment: loan.extraMonthlyPayment,
      status: loan.status,
      notes: loan.notes ?? '',
    };
    this.viewSchedule(loan);
  }

  viewSchedule(loan: Loan) {
    if (this.selectedScheduleId() === loan.id) {
      this.selectedScheduleId.set(null);
      this.schedule.set(null);
      return;
    }
    this.selectedScheduleId.set(loan.id);
    this.scheduleLoading.set(true);
    this.api.loanSchedule(loan.id).subscribe({
      next: s => { this.schedule.set(s); this.scheduleLoading.set(false); },
      error: () => { this.scheduleLoading.set(false); this.schedule.set(null); },
    });
  }

  save() {
    if (!this.model.name.trim()) { this.crud.error.set('Loan name is required.'); return; }
    if ((Number(this.model.principal) || 0) <= 0) { this.crud.error.set('Principal must be greater than zero.'); return; }
    if ((Number(this.model.termMonths) || 0) <= 0) { this.crud.error.set('Term must be at least one month.'); return; }
    const account = this.accounts().find(a => a.id === this.model.accountId);
    const input: LoanInput = {
      ...this.model,
      name: this.model.name.trim(),
      lender: this.model.lender?.toString().trim() || null,
      currency: (this.model.currency || account?.currency || 'CHF').toUpperCase(),
      principal: Number(this.model.principal) || 0,
      annualInterestRate: Number(this.model.annualInterestRate) || 0,
      termMonths: Number(this.model.termMonths) || 0,
      extraMonthlyPayment: Number(this.model.extraMonthlyPayment) || 0,
      startDate: this.startDateValue ? new Date(this.startDateValue).toISOString() : new Date().toISOString(),
    };
    this.crud.save(input);
  }

  remove() {
    this.crud.remove({
      title: 'Delete loan?',
      message: 'The loan and its amortisation schedule will be removed.',
      confirmText: 'Delete',
    });
  }

  onAccountChange() {
    const account = this.accounts().find(a => a.id === this.model.accountId);
    if (account) this.model.currency = account.currency;
  }

  reset() {
    this.crud.cancel();
    this.startDateValue = '';
    this.model = this.defaultModel();
  }

  progressWidth(loan: Loan) {
    const paid = loan.principal - loan.currentBalance;
    return loan.principal <= 0 ? 0 : Math.min(100, Math.max(2, (paid / loan.principal) * 100));
  }

  paidPercent(loan: Loan) {
    return loan.principal <= 0 ? 0 : ((loan.principal - loan.currentBalance) / loan.principal) * 100;
  }

  statusClass(status: LoanStatus) {
    return {
      'text-emerald-300': status === 'PaidOff',
      'text-violet-300': status === 'Active',
      'text-slate-500': status === 'Closed',
    };
  }

  scheduleProgressBuckets(rows: LoanSchedule['rows']) {
    // Year-level buckets keep the table to a tractable size even on a 30-year mortgage.
    const buckets = new Map<number, { year: number; principal: number; interest: number; balance: number }>();
    for (const r of rows) {
      const year = new Date(r.paymentDate).getFullYear();
      const b = buckets.get(year) ?? { year, principal: 0, interest: 0, balance: r.balance };
      b.principal += r.principal + r.extraPrincipal;
      b.interest += r.interest;
      b.balance = r.balance;
      buckets.set(year, b);
    }
    return Array.from(buckets.values()).sort((a, b) => a.year - b.year);
  }

  private defaultModel(): LoanInput {
    return {
      name: '',
      lender: '',
      accountId: null,
      currency: 'CHF',
      principal: 0,
      annualInterestRate: 0,
      termMonths: 240,
      startDate: new Date().toISOString(),
      extraMonthlyPayment: 0,
      status: 'Active',
      notes: '',
    };
  }
}
