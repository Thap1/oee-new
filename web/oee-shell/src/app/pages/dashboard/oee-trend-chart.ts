import { Component, computed, input, signal } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { UIChart } from 'primeng/chart';
import { OeeTrendPointDto } from '../reports/oee-report.service';

/**
 * Deliberately its own family, distinct from both DESIGN.md's machine-status tokens and the loss
 * chart's fixed palette (UX-DR8) — a trend series is neither a machine status nor a loss category,
 * and reusing either set here would make two unrelated things read as the same thing.
 */
const SERIES_COLORS = {
  oee: '#0284c7',
  availability: '#38bdf8',
  performance: '#2dd4bf',
  quality: '#818cf8',
} as const;

type SeriesKey = keyof typeof SERIES_COLORS;

/** Daily OEE trend line chart. Presentational — the Dashboard owns the fetch so the KPI row and this chart share one request. */
@Component({
  selector: 'app-oee-trend-chart',
  standalone: true,
  imports: [TranslatePipe, UIChart],
  template: `
    <div class="trend-chart" data-testid="oee-trend-chart">
      <div class="trend-chart__header">
        <div>
          <h3>{{ 'dashboard.trend.title' | translate }}</h3>
          <p>{{ 'dashboard.trend.subtitle' | translate: { days: points().length } }}</p>
        </div>
        <div class="trend-chart__legend">
          @for (key of seriesKeys; track key) {
            <button
              type="button"
              class="trend-chart__legend-item"
              [class.trend-chart__legend-item--off]="hidden().has(key)"
              (click)="toggle(key)"
              [attr.data-testid]="'oee-trend-legend-' + key"
            >
              <span class="trend-chart__swatch" [style.background]="colorFor(key)"></span>
              {{ 'dashboard.trend.series.' + key | translate }}
            </button>
          }
        </div>
      </div>
      @if (points().length === 0) {
        <div class="trend-chart__empty" data-testid="oee-trend-chart-empty">
          <i class="pi pi-chart-line" aria-hidden="true"></i>
          {{ 'dashboard.trend.empty' | translate }}
        </div>
      } @else {
        <div class="trend-chart__canvas-wrap">
          <p-chart type="line" [data]="chartData()" [options]="chartOptions" height="280px" data-testid="oee-trend-chart-canvas" />
        </div>
      }
    </div>
  `,
  styles: [
    `
      .trend-chart {
        background: var(--p-surface-0, #fff);
        border: 1px solid var(--p-surface-200, #e5e7eb);
        border-radius: var(--app-panel-radius, 16px);
        box-shadow: var(--app-shadow-sm);
        padding: 1.35rem 1.5rem 1.5rem;
      }

      .trend-chart__header {
        display: flex;
        flex-wrap: wrap;
        align-items: flex-start;
        justify-content: space-between;
        gap: 0.75rem;
        margin-bottom: 1.1rem;

        h3 {
          margin: 0;
          font-size: 1.05rem;
          font-weight: 700;
          color: var(--p-surface-900, #0f172a);
        }

        p {
          margin: 0.2rem 0 0;
          font-size: 0.82rem;
          color: var(--p-surface-500, #64748b);
        }
      }

      .trend-chart__legend {
        display: flex;
        flex-wrap: wrap;
        gap: 0.35rem;
      }

      .trend-chart__legend-item {
        display: inline-flex;
        align-items: center;
        gap: 0.4rem;
        border: 1px solid var(--p-surface-200, #e5e7eb);
        background: var(--p-surface-0, #fff);
        border-radius: 999px;
        padding: 0.28rem 0.7rem;
        font-size: 0.78rem;
        font-weight: 600;
        color: var(--p-surface-700, #334155);
        cursor: pointer;
      }

      .trend-chart__legend-item--off {
        opacity: 0.45;
      }

      .trend-chart__swatch {
        width: 0.6rem;
        height: 0.6rem;
        border-radius: 3px;
      }

      .trend-chart__empty {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 0.5rem;
        padding: 3rem 1rem;
        color: var(--p-surface-500, #64748b);

        i {
          font-size: 2rem;
          color: var(--p-surface-300, #cbd5e1);
        }
      }
    `,
  ],
})
export class OeeTrendChart {
  readonly points = input.required<OeeTrendPointDto[]>();

  readonly seriesKeys = Object.keys(SERIES_COLORS) as SeriesKey[];
  private readonly hiddenSignal = signal<ReadonlySet<SeriesKey>>(new Set(['availability', 'performance', 'quality'] as SeriesKey[]));
  readonly hidden = this.hiddenSignal.asReadonly();

  readonly chartOptions = {
    maintainAspectRatio: false,
    interaction: { mode: 'index', intersect: false },
    plugins: { legend: { display: false } },
    scales: {
      y: { min: 0, max: 100, ticks: { callback: (value: number) => `${value}%` }, grid: { color: 'rgba(148,163,184,0.18)' } },
      x: { grid: { display: false } },
    },
  };

  constructor(private readonly translate: TranslateService) {}

  colorFor(key: SeriesKey): string {
    return SERIES_COLORS[key];
  }

  toggle(key: SeriesKey): void {
    const next = new Set(this.hiddenSignal());
    if (!next.delete(key)) {
      next.add(key);
    }
    this.hiddenSignal.set(next);
  }

  readonly chartData = computed(() => {
    const hidden = this.hiddenSignal();
    const points = this.points();
    const visible = this.seriesKeys.filter((key) => !hidden.has(key));

    return {
      labels: points.map((p) => p.date.slice(5)),
      datasets: visible.map((key) => ({
        label: this.translate.instant(`dashboard.trend.series.${key}`),
        data: points.map((p) => Number((p[`${key}Percent`] * 100).toFixed(1))),
        borderColor: SERIES_COLORS[key],
        backgroundColor: SERIES_COLORS[key],
        pointBackgroundColor: SERIES_COLORS[key],
        pointRadius: 3,
        borderWidth: 2,
        tension: 0.35,
        fill: false,
      })),
    };
  });
}
