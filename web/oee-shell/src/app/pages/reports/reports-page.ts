import { Component, OnInit, effect, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DatePicker } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { ScopeService } from '../../core/scope/scope.service';
import { MasterDataService, ShiftScheduleDto } from '../master-data/master-data.service';
import { OeeReportDto, OeeReportService, ReportPeriodType } from './oee-report.service';

/**
 * OEE report by Shift/Day/Week (Story 4.1, FR-016). Self-contained widget: own signals for
 * selection state, own service, `computed()` for derived display — same shape as
 * `LossPieChart`/`LossAnalyticsService` (Story 3.1) rather than a new pattern. The Shift picker
 * reuses `ScopeService.selectedSiteId` (the topbar's Site/Line selector) instead of adding a
 * second independent site selector.
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
      </div>

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
    `,
  ],
})
export class ReportsPage implements OnInit {
  private readonly periodTypeSignal = signal<ReportPeriodType>('Day');
  private readonly referenceDateSignal = signal<Date>(new Date());
  private readonly shiftScheduleIdSignal = signal<string | null>(null);
  private readonly shiftsSignal = signal<ShiftScheduleDto[]>([]);
  private readonly reportSignal = signal<OeeReportDto | null>(null);
  private readonly errorSignal = signal(false);

  readonly periodType = this.periodTypeSignal.asReadonly();
  readonly referenceDate = this.referenceDateSignal.asReadonly();
  readonly shiftScheduleId = this.shiftScheduleIdSignal.asReadonly();
  readonly report = this.reportSignal.asReadonly();
  readonly error = this.errorSignal.asReadonly();

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
    effect(() => {
      const siteId = this.scope.selectedSiteId();
      if (siteId && untracked(this.periodTypeSignal) === 'Shift') {
        void this.loadShiftsForSite(siteId);
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

  private async refetchReport(): Promise<void> {
    if (this.periodTypeSignal() === 'Shift' && !this.shiftScheduleIdSignal()) {
      return;
    }

    this.errorSignal.set(false);
    try {
      this.reportSignal.set(
        await this.oeeReport.getReport(this.periodTypeSignal(), this.referenceDateSignal(), this.shiftScheduleIdSignal()),
      );
    } catch {
      this.reportSignal.set(null);
      this.errorSignal.set(true);
    }
  }
}
