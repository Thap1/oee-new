import { Component, OnInit, computed, effect, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DatePicker } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { ScopeService } from '../../core/scope/scope.service';
import { MachineDto, MasterDataService, ShiftScheduleDto } from '../master-data/master-data.service';
import { OeeReportDto, OeeReportService, ReportFilterTargetType, ReportPeriodType } from './oee-report.service';

/**
 * OEE report by Shift/Day/Week (Story 4.1, FR-016), with an optional Site/Line/Machine filter
 * within the caller's scope (Story 4.2, FR-017). Self-contained widget: own signals for
 * selection state, own service, `computed()` for derived display — same shape as
 * `LossPieChart`/`LossAnalyticsService` (Story 3.1) rather than a new pattern. The Shift picker
 * and the Site/Line filter options reuse `ScopeService` (the topbar's Site/Line selector) instead
 * of adding independent selectors/refetches.
 */
@Component({
  selector: 'app-reports-page',
  standalone: true,
  imports: [FormsModule, TranslatePipe, SelectModule, DatePicker],
  template: `
    <div class="reports-page" data-testid="reports-page">
      <h2>{{ 'reports.title' | translate }}</h2>
      <div class="reports-page__controls">
        <p-select
          [options]="periodTypeOptions()"
          optionLabel="label"
          optionValue="value"
          [ngModel]="periodType()"
          (ngModelChange)="onPeriodTypeChange($event)"
          data-testid="reports-period-type"
        />
        <p-datepicker
          [ngModel]="referenceDate()"
          (ngModelChange)="onReferenceDateChange($event)"
          dateFormat="yy-mm-dd"
          data-testid="reports-reference-date"
        />
        @if (periodType() === 'Shift') {
          <p-select
            [options]="shiftOptions()"
            optionLabel="label"
            optionValue="value"
            [ngModel]="shiftScheduleId()"
            (ngModelChange)="onShiftChange($event)"
            [placeholder]="'reports.selectShift' | translate"
            data-testid="reports-shift"
          />
        }
        <p-select
          [options]="filterTypeOptions()"
          optionLabel="label"
          optionValue="value"
          [ngModel]="filterType()"
          (ngModelChange)="onFilterTypeChange($event)"
          data-testid="reports-filter-type"
        />
        @if (filterType(); as type) {
          <p-select
            [options]="filterTargetOptions()"
            optionLabel="label"
            optionValue="value"
            [ngModel]="filterId()"
            (ngModelChange)="onFilterIdChange($event)"
            [placeholder]="'reports.filter.selectTarget' | translate"
            data-testid="reports-filter-target"
          />
        }
      </div>

      @if (contextHint(); as hint) {
        <p class="reports-page__hint" data-testid="reports-context-hint">{{ hint }}</p>
      }

      @if (error()) {
        <div class="reports-page__error" data-testid="reports-error">{{ 'masterData.error.generic' | translate }}</div>
      } @else if (report(); as r) {
        <div class="reports-page__stats" data-testid="reports-stats">
          <div class="reports-page__stat" data-testid="reports-stat-availability">
            <span>{{ 'reports.availability' | translate }}</span>
            <strong>{{ percent(r.availabilityPercent) }}%</strong>
          </div>
          <div class="reports-page__stat" data-testid="reports-stat-performance">
            <span>{{ 'reports.performance' | translate }}</span>
            <strong>{{ percent(r.performancePercent) }}%</strong>
          </div>
          <div class="reports-page__stat" data-testid="reports-stat-quality">
            <span>{{ 'reports.quality' | translate }}</span>
            <strong>{{ percent(r.qualityPercent) }}%</strong>
          </div>
          <div class="reports-page__stat" data-testid="reports-stat-oee">
            <span>{{ 'reports.oee' | translate }}</span>
            <strong>{{ percent(r.oeePercent) }}%</strong>
          </div>
          <p class="reports-page__reject" data-testid="reports-quality-reject">
            {{ 'reports.qualityReject' | translate }}: {{ r.qualityRejectQuantity }}
          </p>
        </div>
        <div class="reports-page__top-reason" data-testid="reports-top-downtime-reason">
          <h3>{{ 'reports.topDowntimeReason.title' | translate }}</h3>
          @if (r.topDowntimeReasonName; as name) {
            <p>{{ name }} — {{ 'reports.topDowntimeReason.seconds' | translate: { seconds: r.topDowntimeReasonSeconds } }}</p>
          } @else {
            <p data-testid="reports-top-downtime-reason-empty">{{ 'reports.topDowntimeReason.empty' | translate }}</p>
          }
        </div>
      }
    </div>
  `,
  styles: [
    `
      .reports-page__controls {
        display: flex;
        gap: 0.5rem;
        margin-bottom: 1rem;
      }

      .reports-page__stats {
        display: flex;
        gap: 1.5rem;
        flex-wrap: wrap;
      }

      .reports-page__stat {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
      }

      .reports-page__error {
        text-align: center;
        padding: 2rem 1rem;
      }

      .reports-page__reject {
        width: 100%;
        opacity: 0.75;
        font-size: 0.9rem;
      }

      .reports-page__top-reason {
        margin-top: 1.5rem;
      }

      .reports-page__hint {
        opacity: 0.75;
        font-size: 0.9rem;
        margin: -0.5rem 0 1rem;
      }
    `,
  ],
})
export class ReportsPage implements OnInit {
  private readonly periodTypeSignal = signal<ReportPeriodType>('Day');
  private readonly referenceDateSignal = signal<Date>(new Date());
  private readonly shiftScheduleIdSignal = signal<string | null>(null);
  private readonly shiftsSignal = signal<ShiftScheduleDto[]>([]);
  private readonly filterTypeSignal = signal<ReportFilterTargetType | null>(null);
  private readonly filterIdSignal = signal<string | null>(null);
  private readonly filterMachinesSignal = signal<MachineDto[]>([]);
  private readonly reportSignal = signal<OeeReportDto | null>(null);
  private readonly errorSignal = signal(false);

  readonly periodType = this.periodTypeSignal.asReadonly();
  readonly referenceDate = this.referenceDateSignal.asReadonly();
  readonly shiftScheduleId = this.shiftScheduleIdSignal.asReadonly();
  readonly filterType = this.filterTypeSignal.asReadonly();
  readonly filterId = this.filterIdSignal.asReadonly();
  readonly report = this.reportSignal.asReadonly();
  readonly error = this.errorSignal.asReadonly();

  /**
   * Code-review fix: previously, picking Shift with no topbar Site selected (or Machine filter with no
   * topbar Line selected) left the dependent dropdown silently empty with no explanation. Surfaces a
   * guidance hint instead — distinct from `error()`, which is reserved for actual failed requests.
   */
  readonly contextHint = computed(() => {
    if (this.periodTypeSignal() === 'Shift' && !this.scope.selectedSiteId()) {
      return this.translate.instant('reports.noSiteForShift');
    }
    if (this.filterTypeSignal() === 'Machine' && !this.scope.selectedLineId()) {
      return this.translate.instant('reports.noLineForMachineFilter');
    }
    return null;
  });

  constructor(
    private readonly oeeReport: OeeReportService,
    private readonly masterData: MasterDataService,
    private readonly scope: ScopeService,
    private readonly translate: TranslateService,
  ) {
    // Re-fetch this Site's Shift options whenever the topbar's Site/Line selector changes, so a
    // Shift picked before a Site switch never lingers stale (mirrors ScopeService consumers elsewhere).
    // `untracked` keeps this effect scoped to Site changes only — periodType changes are handled by
    // onPeriodTypeChange, and reading it here without untracked would double-fire both on switch.
    // Code-review fix: the previously-picked shiftScheduleId belongs to the *old* Site and won't exist
    // in the reloaded list — clear it (and the now-stale displayed report) instead of leaving a dangling
    // selection silently pointing at data that no longer applies.
    effect(() => {
      const siteId = this.scope.selectedSiteId();
      if (siteId && untracked(this.periodTypeSignal) === 'Shift') {
        this.shiftScheduleIdSignal.set(null);
        this.reportSignal.set(null);
        void this.loadShiftsForSite(siteId);
      }
    });

    // Same reasoning for the Machine-level filter's target options: reload whenever the topbar's
    // selected Line changes while that filter level is active, and clear the stale selection/report.
    effect(() => {
      const lineId = this.scope.selectedLineId();
      if (lineId && untracked(this.filterTypeSignal) === 'Machine') {
        this.filterIdSignal.set(null);
        this.reportSignal.set(null);
        void this.loadMachinesForLine(lineId);
      }
    });
  }

  ngOnInit(): void {
    void this.refetchReport();
  }

  periodTypeOptions() {
    return [
      { label: this.translate.instant('reports.periodType.day'), value: 'Day' },
      { label: this.translate.instant('reports.periodType.week'), value: 'Week' },
      { label: this.translate.instant('reports.periodType.shift'), value: 'Shift' },
    ];
  }

  shiftOptions() {
    return this.shiftsSignal().map((s) => ({ label: s.name, value: s.id }));
  }

  filterTypeOptions() {
    return [
      { label: this.translate.instant('reports.filter.none'), value: null },
      { label: this.translate.instant('reports.filter.site'), value: 'Site' },
      { label: this.translate.instant('reports.filter.line'), value: 'Line' },
      { label: this.translate.instant('reports.filter.machine'), value: 'Machine' },
    ];
  }

  filterTargetOptions() {
    switch (this.filterTypeSignal()) {
      case 'Site':
        return this.scope.sites().map((s) => ({ label: s.name, value: s.id }));
      case 'Line':
        return this.scope.lines().map((l) => ({ label: l.name, value: l.id }));
      case 'Machine':
        return this.filterMachinesSignal().map((m) => ({ label: m.name, value: m.id }));
      default:
        return [];
    }
  }

  async onPeriodTypeChange(periodType: ReportPeriodType): Promise<void> {
    this.periodTypeSignal.set(periodType);
    this.shiftScheduleIdSignal.set(null);

    if (periodType === 'Shift') {
      const siteId = this.scope.selectedSiteId();
      if (siteId) {
        await this.loadShiftsForSite(siteId);
      }
      return;
    }

    await this.refetchReport();
  }

  async onReferenceDateChange(date: Date): Promise<void> {
    this.referenceDateSignal.set(date);
    await this.refetchReport();
  }

  async onShiftChange(shiftScheduleId: string): Promise<void> {
    this.shiftScheduleIdSignal.set(shiftScheduleId);
    await this.refetchReport();
  }

  /** `null` (the "No filter" option) re-fetches the unfiltered report immediately — same default-scope report Story 4.1 built. */
  async onFilterTypeChange(filterType: ReportFilterTargetType | null): Promise<void> {
    this.filterTypeSignal.set(filterType);
    this.filterIdSignal.set(null);

    if (filterType === 'Machine') {
      const lineId = this.scope.selectedLineId();
      if (lineId) {
        await this.loadMachinesForLine(lineId);
      }
      return;
    }

    if (filterType === null) {
      await this.refetchReport();
    }
  }

  async onFilterIdChange(filterId: string): Promise<void> {
    this.filterIdSignal.set(filterId);
    await this.refetchReport();
  }

  percent(ratio: number): string {
    return (ratio * 100).toFixed(1);
  }

  private async loadShiftsForSite(siteId: string): Promise<void> {
    try {
      this.shiftsSignal.set(await this.masterData.listShiftSchedules(siteId));
    } catch {
      this.errorSignal.set(true);
    }
  }

  private async loadMachinesForLine(lineId: string): Promise<void> {
    try {
      this.filterMachinesSignal.set(await this.masterData.listMachines(lineId));
    } catch {
      this.errorSignal.set(true);
    }
  }

  private async refetchReport(): Promise<void> {
    if (this.periodTypeSignal() === 'Shift' && !this.shiftScheduleIdSignal()) {
      return;
    }
    if (this.filterTypeSignal() && !this.filterIdSignal()) {
      return;
    }

    this.errorSignal.set(false);
    try {
      this.reportSignal.set(
        await this.oeeReport.getReport(
          this.periodTypeSignal(),
          this.referenceDateSignal(),
          this.shiftScheduleIdSignal(),
          this.filterTypeSignal(),
          this.filterIdSignal(),
        ),
      );
    } catch {
      this.reportSignal.set(null);
      this.errorSignal.set(true);
    }
  }
}
