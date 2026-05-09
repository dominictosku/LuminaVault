import { CurrencyPipe, DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FinanceApi } from '../../core/data-access/finance-api';
import { FinanceStatistics } from '../../core/models';

type AmountRow = { category: string; amount: number; count: number; average?: number };
type SubscriptionRow = { category: string; monthlyAmount: number; annualAmount: number; count: number };

@Component({
  selector: 'app-statistics',
  imports: [CurrencyPipe, DatePipe, DecimalPipe, NgTemplateOutlet],
  templateUrl: './statistics.html',
  styleUrl: './statistics.scss'
})
export class StatisticsComponent {
  private api = inject(FinanceApi);
  stats = signal<FinanceStatistics | null>(null);

  maxFlow = computed(() => Math.max(
    1,
    ...(this.stats()?.monthlySeries.flatMap(m => [m.income, m.expenses, Math.abs(m.net)]) ?? [1]),
  ));

  maxAccount = computed(() => Math.max(
    1,
    ...(this.stats()?.accountBalances.map(a => Math.abs(a.balance)) ?? [1]),
  ));

  constructor() {
    this.api.financeStatistics().subscribe({ next: stats => this.stats.set(stats) });
  }

  assetTotal() {
    return this.stats()?.assetCategoryBreakdown.reduce((sum, row) => sum + row.amount, 0) ?? 0;
  }

  transactionExpenseTotal() {
    return this.stats()?.transactionExpenseBreakdown.reduce((sum, row) => sum + row.amount, 0) ?? 0;
  }

  transactionIncomeTotal() {
    return this.stats()?.transactionIncomeBreakdown.reduce((sum, row) => sum + row.amount, 0) ?? 0;
  }

  subscriptionWidth(row: SubscriptionRow) {
    const max = Math.max(...(this.stats()?.subscriptionCategoryBreakdown.map(r => r.monthlyAmount) ?? [1]), 1);
    return Math.max(3, (row.monthlyAmount / max) * 100);
  }

  accountWidth(balance: number) {
    return Math.max(3, (Math.abs(balance) / this.maxAccount()) * 100);
  }

  share(amount: number, total: number) {
    return total <= 0 ? 0 : Math.max(2, (amount / total) * 100);
  }

  monthX(index: number) {
    const count = Math.max(this.stats()?.monthlySeries.length ?? 1, 1);
    return 56 + index * (620 / Math.max(count - 1, 1));
  }

  chartY(tick: number) {
    return 230 - tick * 190;
  }

  barHeight(value: number) {
    return Math.max(2, (value / this.maxFlow()) * 180);
  }

  barY(value: number) {
    return 230 - this.barHeight(value);
  }

  netY(value: number) {
    const normalized = (value / this.maxFlow()) * 90;
    return 140 - normalized;
  }

  shortMonth(month: string) {
    return month.split(' ')[0];
  }
}
