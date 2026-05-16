import { CurrencyPipe, DatePipe, NgClass } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FinanceApi } from '../../core/data-access/finance-api';
import { CashFlowForecast, FinanceAccount, ForecastEvent } from '../../core/models';

const CHART_WIDTH = 1000;
const CHART_HEIGHT = 240;
const PAD_X = 16;
const PAD_Y = 16;

@Component({
  selector: 'app-forecast',
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe, NgClass],
  templateUrl: './forecast.html',
})
export class ForecastComponent {
  private api = inject(FinanceApi);

  forecast = signal<CashFlowForecast | null>(null);
  accounts = signal<FinanceAccount[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  rangeDays = signal<number>(30);
  accountFilter = signal<number | null>(null);

  readonly rangeOptions = [
    { label: '14 days', days: 14 },
    { label: '30 days', days: 30 },
    { label: '60 days', days: 60 },
    { label: '90 days', days: 90 },
    { label: '180 days', days: 180 },
  ];

  readonly chartWidth = CHART_WIDTH;
  readonly chartHeight = CHART_HEIGHT;

  bounds = computed(() => {
    const points = this.forecast()?.daily ?? [];
    if (points.length === 0) return { min: 0, max: 1 };
    const values = points.map(p => p.balance);
    const min = Math.min(...values, 0);
    const max = Math.max(...values, 0);
    if (min === max) return { min: min - 1, max: max + 1 };
    const pad = (max - min) * 0.08;
    return { min: min - pad, max: max + pad };
  });

  zeroLineY = computed(() => {
    const { min, max } = this.bounds();
    if (min >= 0 || max <= 0) return null;
    const range = max - min;
    const spanY = CHART_HEIGHT - PAD_Y * 2;
    return PAD_Y + spanY - ((0 - min) / range) * spanY;
  });

  linePath = computed(() => this.buildPath(false));
  areaPath = computed(() => this.buildPath(true));

  upcomingEvents = computed<ForecastEvent[]>(() => (this.forecast()?.events ?? []).slice(0, 50));

  hasNegativeProjection = computed(() => (this.forecast()?.lowestBalance ?? 0) < 0);

  constructor() {
    this.api.listFinanceAccounts().subscribe({ next: a => this.accounts.set(a) });
    this.fetch();
  }

  fetch() {
    this.loading.set(true);
    this.error.set(null);
    const accountId = this.accountFilter() ?? undefined;
    this.api.cashFlowForecast({ days: this.rangeDays(), accountId }).subscribe({
      next: f => {
        this.forecast.set(f);
        this.loading.set(false);
      },
      error: e => {
        this.error.set(e?.error?.error ?? 'Could not load forecast.');
        this.loading.set(false);
      },
    });
  }

  setRange(days: number) {
    this.rangeDays.set(days);
    this.fetch();
  }

  setAccountFilter(value: number | null) {
    this.accountFilter.set(value);
    this.fetch();
  }

  money(value: number | null | undefined) {
    const currency = this.forecast()?.baseCurrency ?? 'CHF';
    if (value == null) return new Intl.NumberFormat('de-CH', { style: 'currency', currency }).format(0);
    return new Intl.NumberFormat('de-CH', { style: 'currency', currency }).format(value);
  }

  eventToneClass(amount: number): Record<string, boolean> {
    return {
      'text-emerald-300': amount > 0,
      'text-red-300': amount < 0,
      'text-slate-400': amount === 0,
    };
  }

  sourceBadgeClass(source: ForecastEvent['source']): string {
    switch (source) {
      case 'Subscription': return 'bg-amber-500/15 text-amber-200 border-amber-500/30';
      case 'Pending': return 'bg-sky-500/15 text-sky-200 border-sky-500/30';
      case 'Transfer': return 'bg-violet-500/15 text-violet-200 border-violet-500/30';
    }
  }

  private buildPath(closeForArea: boolean): string {
    const points = this.forecast()?.daily ?? [];
    if (points.length === 0) return '';
    const { min, max } = this.bounds();
    const spanX = CHART_WIDTH - PAD_X * 2;
    const spanY = CHART_HEIGHT - PAD_Y * 2;
    const range = max - min || 1;
    const n = points.length;
    const xs = points.map((_, i) => PAD_X + (n === 1 ? spanX / 2 : (i / (n - 1)) * spanX));
    const ys = points.map(p => PAD_Y + spanY - ((p.balance - min) / range) * spanY);
    const segments = xs.map((x, i) => `${i === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${ys[i].toFixed(1)}`);
    if (!closeForArea) return segments.join(' ');
    const baselineY = PAD_Y + spanY;
    return [
      ...segments,
      `L ${xs[xs.length - 1].toFixed(1)} ${baselineY.toFixed(1)}`,
      `L ${xs[0].toFixed(1)} ${baselineY.toFixed(1)}`,
      'Z',
    ].join(' ');
  }
}
