import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { sankey, sankeyJustify, sankeyLinkHorizontal } from 'd3-sankey';

export interface FinanceSankeyRow {
  category: string;
  amount: number;
}

interface SankeyNodeData {
  id: string;
  label: string;
  kind: 'income' | 'hub' | 'expense' | 'surplus' | 'shortfall';
  amount: number;
}

interface SankeyLinkData {
  source: string;
  target: string;
  value: number;
}

interface RenderedNode extends SankeyNodeData {
  x0: number;
  x1: number;
  y0: number;
  y1: number;
}

interface RenderedLink {
  source: string;
  target: string;
  value: number;
  path: string;
  width: number;
  color: string;
}

/// Money-flow Sankey: income categories on the left, a "Net" hub in the middle, expense
/// categories on the right, plus a synthetic Surplus or Shortfall node so the diagram
/// always balances visually. Layout is computed by d3-sankey; we render plain SVG so
/// styling stays consistent with the rest of the app.
@Component({
  selector: 'app-finance-sankey',
  standalone: true,
  imports: [CurrencyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (totalIncome() <= 0 && totalExpenses() <= 0) {
      <div class="text-center text-slate-400 text-sm py-10">
        <i class="pi pi-share-alt text-3xl text-slate-500"></i>
        <div class="mt-2">No income or expense transactions in this window.</div>
      </div>
    } @else {
      <div class="relative">
        <svg [attr.viewBox]="'0 0 ' + width + ' ' + height()" class="w-full overflow-visible"
             [style.height.px]="height()">
          @for (link of links(); track link.source + '->' + link.target) {
            <path [attr.d]="link.path"
                  [attr.stroke]="link.color"
                  [attr.stroke-width]="link.width"
                  fill="none"
                  [attr.opacity]="linkOpacity(link)"
                  class="transition-opacity"
                  (mouseenter)="hovered.set(link.source + '|' + link.target)"
                  (mouseleave)="hovered.set(null)" />
          }
          @for (node of nodes(); track node.id) {
            <g (mouseenter)="hovered.set(node.id)" (mouseleave)="hovered.set(null)">
              <rect [attr.x]="node.x0"
                    [attr.y]="node.y0"
                    [attr.width]="node.x1 - node.x0"
                    [attr.height]="Math.max(1, node.y1 - node.y0)"
                    [attr.fill]="nodeColor(node.kind)"
                    [attr.opacity]="nodeOpacity(node)"
                    class="transition-opacity">
                <title>{{ node.label }}: {{ node.amount | currency:currency():'symbol-narrow':'1.0-0' }} ({{ sharePercent(node) }}%)</title>
              </rect>
              <text [attr.x]="labelX(node)"
                    [attr.y]="(node.y0 + node.y1) / 2"
                    [attr.text-anchor]="labelAnchor(node)"
                    dy="0.35em"
                    class="fill-slate-300 text-[11px] pointer-events-none">
                {{ truncate(node.label) }}
              </text>
              <text [attr.x]="labelX(node)"
                    [attr.y]="(node.y0 + node.y1) / 2 + 13"
                    [attr.text-anchor]="labelAnchor(node)"
                    dy="0.35em"
                    class="fill-slate-500 text-[10px] pointer-events-none">
                {{ node.amount | currency:currency():'symbol-narrow':'1.0-0' }}
              </text>
            </g>
          }
        </svg>
      </div>
    }
  `,
})
export class FinanceSankeyComponent {
  // Make Math available in the template — Angular's template language doesn't expose it
  // and we need Math.max above to clamp degenerate zero-height rects.
  protected readonly Math = Math;

  income = input.required<FinanceSankeyRow[]>();
  expenses = input.required<FinanceSankeyRow[]>();
  currency = input<string>('CHF');
  /// Render width in SVG user-units. The viewBox scales to the container; this just
  /// fixes the aspect of the layout that d3-sankey produces.
  readonly width = 760;
  /// Caps the rendered node count per side so a runaway category list can't blow
  /// the diagram up to thousands of pixels tall.
  readonly maxNodesPerSide = 12;

  protected hovered = signal<string | null>(null);

  protected totalIncome = computed(() => sumPositive(this.income()));
  protected totalExpenses = computed(() => sumPositive(this.expenses()));

  protected height = computed(() => {
    const sides = Math.max(this.consolidate(this.income()).length, this.consolidate(this.expenses()).length, 1);
    return Math.max(280, 56 + sides * 38);
  });

  private graph = computed(() => {
    const income = this.consolidate(this.income());
    const expenses = this.consolidate(this.expenses());
    const totalIn = sumPositive(income);
    const totalOut = sumPositive(expenses);
    if (totalIn <= 0 && totalOut <= 0) return { nodes: [], links: [] as RenderedLink[], rendered: [] as RenderedNode[] };

    const nodes: SankeyNodeData[] = [];
    const links: SankeyLinkData[] = [];
    // The hub anchors the diagram even when one side is empty (e.g. expenses-only) — the
    // d3-sankey layout requires a connected graph, and the hub gives every node a path.
    nodes.push({ id: 'hub', label: 'Net', kind: 'hub', amount: Math.max(totalIn, totalOut) });

    for (const row of income) {
      const id = `in:${row.category}`;
      nodes.push({ id, label: row.category, kind: 'income', amount: row.amount });
      links.push({ source: id, target: 'hub', value: row.amount });
    }
    for (const row of expenses) {
      const id = `out:${row.category}`;
      nodes.push({ id, label: row.category, kind: 'expense', amount: row.amount });
      links.push({ source: 'hub', target: id, value: row.amount });
    }

    // Balance the two sides so the Sankey reads correctly. If you earned more than you
    // spent, the leftover goes to a green "Surplus" bucket; if you spent more, a red
    // "Shortfall" bucket plugs the income side so total inflow == total outflow.
    const diff = totalIn - totalOut;
    if (diff > 0.01) {
      nodes.push({ id: 'surplus', label: 'Surplus', kind: 'surplus', amount: diff });
      links.push({ source: 'hub', target: 'surplus', value: diff });
    } else if (diff < -0.01) {
      const shortfall = -diff;
      nodes.push({ id: 'shortfall', label: 'Shortfall', kind: 'shortfall', amount: shortfall });
      links.push({ source: 'shortfall', target: 'hub', value: shortfall });
    }

    // d3-sankey resolves string source/target IDs via nodeId() — no need to pre-index.
    const layout = sankey<SankeyNodeData, SankeyLinkData>()
      .nodeId(n => n.id)
      .nodeAlign(sankeyJustify)
      .nodeWidth(12)
      .nodePadding(16)
      .extent([[8, 8], [this.width - 8, this.height() - 8]]);
    const graph = layout({
      nodes: nodes.map(n => ({ ...n })),
      links: links.map(l => ({ ...l })),
    });

    const pathFn = sankeyLinkHorizontal();
    const renderedLinks: RenderedLink[] = graph.links.map(l => {
      const sourceNode = l.source as RenderedNode;
      const targetNode = l.target as RenderedNode;
      return {
        source: sourceNode.id,
        target: targetNode.id,
        value: l.value as number,
        path: pathFn(l as Parameters<typeof pathFn>[0]) ?? '',
        width: Math.max(1, (l as { width: number }).width ?? 1),
        color: this.linkColor(sourceNode.kind, targetNode.kind),
      };
    });
    const renderedNodes = graph.nodes as RenderedNode[];

    return { nodes: renderedNodes, rendered: renderedNodes, links: renderedLinks };
  });

  protected nodes = computed(() => this.graph().nodes);
  protected links = computed(() => this.graph().links);

  protected nodeColor(kind: SankeyNodeData['kind']) {
    return ({
      income: '#34d399',
      expense: '#fb7185',
      hub: '#a78bfa',
      surplus: '#10b981',
      shortfall: '#f43f5e',
    } as const)[kind];
  }

  protected linkColor(sourceKind: SankeyNodeData['kind'], targetKind: SankeyNodeData['kind']) {
    if (sourceKind === 'shortfall' || targetKind === 'shortfall') return '#f43f5e80';
    if (targetKind === 'surplus') return '#10b98180';
    if (sourceKind === 'income') return '#34d39966';
    return '#fb718566';
  }

  protected labelAnchor(node: RenderedNode) {
    if (node.kind === 'hub') return 'middle';
    if (node.kind === 'income' || node.kind === 'shortfall') return 'start';
    return 'end';
  }

  protected labelX(node: RenderedNode) {
    if (node.kind === 'hub') return (node.x0 + node.x1) / 2;
    if (node.kind === 'income' || node.kind === 'shortfall') return node.x1 + 6;
    return node.x0 - 6;
  }

  protected truncate(label: string) {
    return label.length > 18 ? label.slice(0, 17) + '…' : label;
  }

  protected sharePercent(node: SankeyNodeData) {
    const denom = node.kind === 'income' || node.kind === 'shortfall'
      ? Math.max(this.totalIncome(), this.totalExpenses())
      : Math.max(this.totalExpenses(), this.totalIncome());
    return denom <= 0 ? 0 : Math.round((node.amount / denom) * 1000) / 10;
  }

  protected nodeOpacity(node: RenderedNode) {
    const h = this.hovered();
    if (!h) return 0.95;
    return h === node.id || this.isLinked(node.id, h) ? 1 : 0.35;
  }

  protected linkOpacity(link: RenderedLink) {
    const h = this.hovered();
    if (!h) return 0.55;
    const key = link.source + '|' + link.target;
    return h === key || h === link.source || h === link.target ? 0.9 : 0.15;
  }

  private isLinked(nodeId: string, hovered: string) {
    if (hovered.includes('|')) {
      const [s, t] = hovered.split('|');
      return s === nodeId || t === nodeId;
    }
    return this.links().some(l =>
      (l.source === nodeId && l.target === hovered) || (l.source === hovered && l.target === nodeId));
  }

  /// Cap rows to `maxNodesPerSide` and fold the long tail into an "Other" bucket so
  /// users with many categories still get a readable diagram.
  private consolidate(rows: FinanceSankeyRow[]): FinanceSankeyRow[] {
    const positive = rows.filter(r => r.amount > 0)
      .sort((a, b) => b.amount - a.amount);
    if (positive.length <= this.maxNodesPerSide) return positive;
    const top = positive.slice(0, this.maxNodesPerSide - 1);
    const other = positive.slice(this.maxNodesPerSide - 1)
      .reduce((sum, r) => sum + r.amount, 0);
    return [...top, { category: 'Other', amount: other }];
  }
}

function sumPositive(rows: FinanceSankeyRow[]) {
  return rows.reduce((sum, r) => sum + Math.max(0, r.amount), 0);
}
