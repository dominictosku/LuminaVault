import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FinanceApi } from '../../core/data-access/finance-api';
import { FinanceSummary } from '../../core/models';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, CurrencyPipe, DatePipe, DecimalPipe],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class DashboardComponent {
  private api = inject(FinanceApi);
  summary = signal<FinanceSummary | null>(null);
  today = new Date();

  maxFlow = computed(() => Math.max(
    1,
    ...(this.summary()?.monthlySeries.flatMap(m => [m.income, m.expenses]) ?? [1]),
  ));

  maxCategory = computed(() => Math.max(
    1,
    ...(this.summary()?.categoryBreakdown.map(c => c.amount) ?? [1]),
  ));

  constructor() {
    this.api.financeSummary().subscribe({ next: s => this.summary.set(s) });
  }

  money(value: number | null | undefined) {
    const currency = this.summary()?.baseCurrency ?? 'CHF';
    if (value == null) return new Intl.NumberFormat('de-CH', { style: 'currency', currency }).format(0);
    return new Intl.NumberFormat('de-CH', { style: 'currency', currency }).format(value);
  }

  barHeight(value: number) {
    return Math.max(3, (value / this.maxFlow()) * 100);
  }

  categoryWidth(value: number) {
    return Math.max(5, (value / this.maxCategory()) * 100);
  }

  transactionSign(kind: string) {
    return kind === 'Income' ? '+' : kind === 'Expense' ? '-' : '';
  }
}
