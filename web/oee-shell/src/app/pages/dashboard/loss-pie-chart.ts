import { Component, Input, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DatePicker } from 'primeng/datepicker';
import { UIChart } from 'primeng/chart';
import { SelectModule } from 'primeng/select';
import { LossCategoryValue } from '../master-data/master-data.service';
import {
  LossAnalyticsService,
  LossAreaDto,
  LossBreakdownDto,
  LossBreakdownTargetType,
  ReasonBreakdownDto,
} from './loss-analytics.service';

/** DESIGN.md §Colors — fixed across light/dark themes (UX-DR8), never reused for machine-status colors. */
const LOSS_COLORS = {
  availability: '#EF4444',
  performance: '#F59E0B',
  quality: '#8B5CF6',
} as const;

/** Dataset point index (Chart.js `element.index` from `UIChart.onDataSelect`, Story 3.2) → LossCategory — must match `chartData()`'s `labels`/`data` order below. */
const CATEGORY_BY_SLICE_INDEX: LossCategoryValue[] = ['AvailabilityLoss', 'PerformanceLoss', 'QualityLoss'];

export interface EquipmentOption {
  machineId: string;
  machineName: string;
}

/** Minimal shape of `UIChart.onDataSelect`'s emitted event — see `primeng-chart.mjs`'s `onCanvasClick`. */
export interface ChartSliceSelectedEvent {
  element: { index: number };
}

/**
 * Loss pie chart (Story 3.1, FR-019/020/021). Self-contained: manages its own target-type/target
 * selection, doesn't share state with `DashboardPage`'s machine grid — see Dev Notes on not touching
 * Story 2.2/2.4's existing code. Equipment options come from the Dashboard's already-scoped machine
 * list (Story 2.2); Area options are lazily fetched only once the user switches to "by Area" (AC #4).
 */
@Component({
  selector: 'app-loss-pie-chart',
  standalone: true,
  imports: [FormsModule, TranslatePipe, SelectModule, DatePicker, UIChart],
  template: `
    <div class="loss-pie-chart" data-testid="loss-pie-chart">
      <div class="loss-pie-chart__header">
        <i class="pi pi-chart-pie" aria-hidden="true"></i>
        <h3>{{ 'lossChart.title' | translate }}</h3>
      </div>
      <div class="loss-pie-chart__controls">
        <p-select
          [options]="targetTypeOptions()"
          optionLabel="label"
          optionValue="value"
          [ngModel]="targetType()"
          (ngModelChange)="onTargetTypeChange($event)"
          data-testid="loss-pie-chart-target-type"
        />
        <p-select
          [options]="targetOptions()"
          optionLabel="label"
          optionValue="value"
          [ngModel]="selectedTargetId()"
          (ngModelChange)="onTargetChange($event)"
          [placeholder]="'lossChart.selectTarget' | translate"
          data-testid="loss-pie-chart-target"
        />
        <p-datepicker
          [ngModel]="selectedDate()"
          (ngModelChange)="onDateChange($event)"
          [placeholder]="'lossChart.selectDate' | translate"
          [showClear]="true"
          dateFormat="yy-mm-dd"
          data-testid="loss-pie-chart-date"
        />
      </div>
      @if (error()) {
        <div class="loss-pie-chart__error" data-testid="loss-pie-chart-error">
          <i class="pi pi-exclamation-triangle" aria-hidden="true"></i>
          {{ 'lossChart.loadError' | translate }}
        </div>
      } @else if (isEmpty()) {
        <div class="loss-pie-chart__empty" data-testid="loss-pie-chart-empty">
          <i class="pi pi-inbox" aria-hidden="true"></i>
          {{ 'lossChart.emptyState' | translate }}
        </div>
      } @else if (chartData()) {
        <div class="loss-pie-chart__canvas-wrap">
          <p-chart
            type="pie"
            [data]="chartData()"
            [options]="chartOptions"
            (onDataSelect)="onSliceSelected($event)"
            data-testid="loss-pie-chart-canvas"
          />
        </div>
        @if (breakdownCaption()) {
          <p class="loss-pie-chart__caption" data-testid="loss-pie-chart-caption">{{ breakdownCaption() }}</p>
        }
      }
      @if (reasonBreakdownCategory()) {
        <div class="loss-pie-chart__reason-breakdown" data-testid="loss-pie-chart-reason-breakdown">
          <h4>{{ 'lossChart.reasonBreakdown.title' | translate: { category: reasonBreakdownCategoryLabel() } }}</h4>
          @if (reasonBreakdown().length === 0) {
            <p>{{ 'lossChart.reasonBreakdown.empty' | translate }}</p>
          } @else {
            <ul>
              @for (item of reasonBreakdown(); track item.reasonCodeId) {
                <li data-testid="loss-pie-chart-reason-item">{{ item.reasonCodeName }}: {{ item.durationSeconds }}s</li>
              }
            </ul>
          }
        </div>
      }
    </div>
  `,
  styles: [
    `
      .loss-pie-chart {
        background: var(--p-surface-0, #fff);
        border: 1px solid var(--p-surface-200, #e5e7eb);
        border-radius: var(--app-panel-radius, 16px);
        box-shadow: var(--app-shadow-sm);
        padding: 1.5rem 1.75rem 1.75rem;
      }

      .loss-pie-chart__header {
        display: flex;
        align-items: center;
        gap: 0.6rem;
        margin-bottom: 1.25rem;

        i {
          font-size: 1.1rem;
          color: var(--p-primary-color, #10b981);
        }

        h3 {
          margin: 0;
          font-size: 1.1rem;
          font-weight: 700;
          color: var(--p-surface-900, #0f172a);
        }
      }

      .loss-pie-chart__controls {
        display: flex;
        flex-wrap: wrap;
        gap: 0.75rem;
        margin-bottom: 1.5rem;
        padding-bottom: 1.25rem;
        border-bottom: 1px solid var(--p-surface-100, #f1f5f9);
      }

      .loss-pie-chart__canvas-wrap {
        max-width: 360px;
        margin: 0 auto;
      }

      .loss-pie-chart__empty,
      .loss-pie-chart__error {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 0.5rem;
        text-align: center;
        padding: 2.5rem 1rem;
        color: var(--p-surface-500, #64748b);

        i {
          font-size: 2rem;
          color: var(--p-surface-300, #cbd5e1);
        }
      }

      .loss-pie-chart__error i {
        color: var(--status-stopped, #ef4444);
      }

      .loss-pie-chart__caption {
        text-align: center;
        opacity: 0.75;
        font-size: 0.9rem;
        margin-top: 1rem;
      }

      .loss-pie-chart__reason-breakdown {
        margin-top: 1.25rem;
        padding-top: 1.25rem;
        border-top: 1px solid var(--p-surface-100, #f1f5f9);

        h4 {
          margin: 0 0 0.5rem;
          font-size: 0.95rem;
          font-weight: 700;
        }

        ul {
          margin: 0;
          padding-left: 1.25rem;
        }
      }
    `,
  ],
})
export class LossPieChart {
  @Input({ required: true }) equipmentOptions: EquipmentOption[] = [];

  private readonly targetTypeSignal = signal<LossBreakdownTargetType>('Equipment');
  private readonly selectedTargetIdSignal = signal<string | null>(null);
  private readonly selectedDateSignal = signal<Date | null>(null);
  private readonly areasSignal = signal<LossAreaDto[]>([]);
  private readonly breakdownSignal = signal<LossBreakdownDto | null>(null);
  private readonly reasonBreakdownCategorySignal = signal<LossCategoryValue | null>(null);
  private readonly reasonBreakdownSignal = signal<ReasonBreakdownDto[]>([]);
  private readonly errorSignal = signal(false);

  readonly targetType = this.targetTypeSignal.asReadonly();
  readonly selectedTargetId = this.selectedTargetIdSignal.asReadonly();
  readonly selectedDate = this.selectedDateSignal.asReadonly();
  readonly reasonBreakdownCategory = this.reasonBreakdownCategorySignal.asReadonly();
  readonly reasonBreakdown = this.reasonBreakdownSignal.asReadonly();
  /** A failed fetch (network blip, scope rejection, ...) — mirrors `DashboardPage`'s `loadError` pattern (Story 2.2) rather than leaving the widget silently stuck on stale data. */
  readonly error = this.errorSignal.asReadonly();

  readonly chartOptions = { plugins: { legend: { position: 'bottom' } } };

  constructor(
    private readonly lossAnalytics: LossAnalyticsService,
    private readonly translate: TranslateService,
  ) {}

  targetTypeOptions() {
    return [
      { label: this.translate.instant('lossChart.byEquipment'), value: 'Equipment' },
      { label: this.translate.instant('lossChart.byArea'), value: 'Area' },
    ];
  }

  targetOptions() {
    return this.targetType() === 'Equipment'
      ? this.equipmentOptions.map((m) => ({ label: m.machineName, value: m.machineId }))
      : this.areasSignal().map((a) => ({ label: a.lineName, value: a.lineId }));
  }

  async onTargetTypeChange(targetType: LossBreakdownTargetType): Promise<void> {
    this.targetTypeSignal.set(targetType);
    this.selectedTargetIdSignal.set(null);
    this.breakdownSignal.set(null);
    this.errorSignal.set(false);
    this.clearReasonBreakdown();

    if (targetType === 'Area' && this.areasSignal().length === 0) {
      try {
        this.areasSignal.set(await this.lossAnalytics.listAreas());
      } catch {
        this.errorSignal.set(true);
      }
    }
  }

  async onTargetChange(targetId: string): Promise<void> {
    this.selectedTargetIdSignal.set(targetId);
    this.clearReasonBreakdown();
    await this.refetchBreakdown();
  }

  /** `null` clears the filter — back to Story 3.1's all-time default (AC #1's premise). */
  async onDateChange(date: Date | null): Promise<void> {
    this.selectedDateSignal.set(date);
    this.clearReasonBreakdown();
    await this.refetchBreakdown();
  }

  private async refetchBreakdown(): Promise<void> {
    const targetId = this.selectedTargetIdSignal();
    if (!targetId) {
      return;
    }

    this.errorSignal.set(false);
    try {
      this.breakdownSignal.set(await this.lossAnalytics.getBreakdown(this.targetType(), targetId, this.selectedDateSignal()));
    } catch {
      this.breakdownSignal.set(null);
      this.errorSignal.set(true);
    }
  }

  /** `element.index` (Chart.js dataset point index) → LossCategory, per `CATEGORY_BY_SLICE_INDEX` (Story 3.2, AC #2). */
  async onSliceSelected(event: ChartSliceSelectedEvent): Promise<void> {
    const targetId = this.selectedTargetIdSignal();
    const category = CATEGORY_BY_SLICE_INDEX[event.element.index];
    if (!targetId || !category) {
      return;
    }

    this.reasonBreakdownCategorySignal.set(category);
    try {
      this.reasonBreakdownSignal.set(
        await this.lossAnalytics.getReasonBreakdown(this.targetType(), targetId, category, this.selectedDateSignal()),
      );
    } catch {
      // The pie chart itself is still showing valid data — only the drill-down panel failed, so
      // this doesn't set the page-level error() state, just leaves the reason list empty.
      this.reasonBreakdownSignal.set([]);
    }
  }

  reasonBreakdownCategoryLabel(): string {
    const category = this.reasonBreakdownCategorySignal();
    const key = category === 'AvailabilityLoss' ? 'availability' : category === 'PerformanceLoss' ? 'performance' : 'quality';
    return this.translate.instant(`lossChart.${key}`);
  }

  private clearReasonBreakdown(): void {
    this.reasonBreakdownCategorySignal.set(null);
    this.reasonBreakdownSignal.set([]);
  }

  readonly chartData = computed(() => {
    const breakdown = this.breakdownSignal();
    if (!breakdown) {
      return null;
    }

    return {
      labels: [
        this.translate.instant('lossChart.availability'),
        this.translate.instant('lossChart.performance'),
        this.translate.instant('lossChart.quality'),
      ],
      datasets: [
        {
          data: [breakdown.availabilitySeconds, breakdown.performanceSeconds, breakdown.qualitySeconds],
          backgroundColor: [LOSS_COLORS.availability, LOSS_COLORS.performance, LOSS_COLORS.quality],
        },
      ],
    };
  });

  readonly isEmpty = computed(() => {
    const breakdown = this.breakdownSignal();
    return breakdown !== null && breakdown.availabilitySeconds + breakdown.performanceSeconds + breakdown.qualitySeconds === 0;
  });

  /**
   * Supplementary figures Story 3.1 deliberately keeps out of the pie's 3 slices — `qualityRejectQuantity`
   * (no time value to convert into a slice) and `unattributedSeconds` (closed downtime with no ReasonCode,
   * excluded from the fixed 3-color palette per UX-DR8) — surfaced here instead of silently dropped.
   */
  readonly breakdownCaption = computed(() => {
    const breakdown = this.breakdownSignal();
    if (!breakdown) {
      return null;
    }

    const parts: string[] = [];
    if (breakdown.qualityRejectQuantity > 0) {
      parts.push(this.translate.instant('lossChart.qualityRejectCaption', { quantity: breakdown.qualityRejectQuantity }));
    }
    if (breakdown.unattributedSeconds > 0) {
      parts.push(this.translate.instant('lossChart.unattributedCaption', { seconds: breakdown.unattributedSeconds }));
    }

    return parts.length > 0 ? parts.join(' · ') : null;
  });
}
