import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FinanceSummaryApi } from '../../core/data-access/finance-summary-api';
import { NetWorthApi } from '../../core/data-access/net-worth-api';
import { FinanceSummary, NetWorthSnapshot } from '../../core/models';
import { ToastService } from '../../shared/toast/toast.service';

const CHART_WIDTH = 1000;
const CHART_HEIGHT = 220;
const CHART_PAD_X = 16;
const CHART_PAD_Y = 12;

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, CurrencyPipe, DatePipe, DecimalPipe],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class DashboardComponent {
  private summaryApi = inject(FinanceSummaryApi);
  private netWorthApi = inject(NetWorthApi);
  private toast = inject(ToastService);
  summary = signal<FinanceSummary | null>(null);
  netWorthHistory = signal<NetWorthSnapshot[]>([]);
  netWorthLoading = signal(true);
  capturingSnapshot = signal(false);
  today = new Date();

  // Chart geometry constants are exposed for the template to keep viewBox in sync.
  readonly chartWidth = CHART_WIDTH;
  readonly chartHeight = CHART_HEIGHT;

  maxFlow = computed(() => Math.max(
    1,
    ...(this.summary()?.monthlySeries.flatMap(m => [m.income, m.expenses]) ?? [1]),
  ));

  maxCategory = computed(() => Math.max(
    1,
    ...(this.summary()?.categoryBreakdown.map(c => c.amount) ?? [1]),
  ));

  netWorthBounds = computed(() => {
    const points = this.netWorthHistory();
    if (points.length === 0) return { min: 0, max: 1 };
    const values = points.map(p => p.netWorth);
    const min = Math.min(...values);
    const max = Math.max(...values);
    if (min === max) return { min: min - 1, max: max + 1 };
    const pad = (max - min) * 0.08;
    return { min: min - pad, max: max + pad };
  });

  // SVG path for the line. Single point returns a tiny horizontal segment so the line is visible.
  netWorthLinePath = computed(() => this.buildPath(false));
  netWorthAreaPath = computed(() => this.buildPath(true));

  netWorthDelta = computed(() => {
    const points = this.netWorthHistory();
    if (points.length < 2) return null;
    const first = points[0].netWorth;
    const last = points[points.length - 1].netWorth;
    const abs = last - first;
    const pct = first === 0 ? null : (abs / Math.abs(first)) * 100;
    return { abs, pct };
  });

  constructor() {
    this.summaryApi.financeSummary().subscribe({ next: s => this.summary.set(s) });
    this.loadHistory();
  }

  loadHistory() {
    this.netWorthLoading.set(true);
    this.netWorthApi.netWorthHistory().subscribe({
      next: rows => {
        this.netWorthHistory.set(rows);
        this.netWorthLoading.set(false);
      },
      error: () => this.netWorthLoading.set(false),
    });
  }

  captureSnapshot() {
    this.capturingSnapshot.set(true);
    this.netWorthApi.captureNetWorthSnapshot().subscribe({
      next: snapshot => {
        this.netWorthHistory.update(list => {
          const without = list.filter(s => s.snapshotDate.substring(0, 10) !== snapshot.snapshotDate.substring(0, 10));
          return [...without, snapshot].sort((a, b) => a.snapshotDate.localeCompare(b.snapshotDate));
        });
        this.capturingSnapshot.set(false);
        this.toast.success('Net worth snapshot captured.');
      },
      error: () => this.capturingSnapshot.set(false),
    });
  }

  private buildPath(closeForArea: boolean): string {
    const points = this.netWorthHistory();
    if (points.length === 0) return '';
    const { min, max } = this.netWorthBounds();
    const spanX = CHART_WIDTH - CHART_PAD_X * 2;
    const spanY = CHART_HEIGHT - CHART_PAD_Y * 2;
    const range = max - min || 1;
    const n = points.length;
    const xs = points.map((_, i) => CHART_PAD_X + (n === 1 ? spanX / 2 : (i / (n - 1)) * spanX));
    const ys = points.map(p => CHART_PAD_Y + spanY - ((p.netWorth - min) / range) * spanY);

    const segments = xs.map((x, i) => `${i === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${ys[i].toFixed(1)}`);
    if (!closeForArea) return segments.join(' ');

    const baselineY = CHART_PAD_Y + spanY;
    return [
      ...segments,
      `L ${xs[xs.length - 1].toFixed(1)} ${baselineY.toFixed(1)}`,
      `L ${xs[0].toFixed(1)} ${baselineY.toFixed(1)}`,
      'Z',
    ].join(' ');
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
