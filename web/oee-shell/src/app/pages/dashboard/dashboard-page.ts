import { Component, OnDestroy, OnInit, computed, effect, signal, untracked } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { AppModeService } from '../../core/app-mode/app-mode.service';
import { AuthService } from '../../core/auth/auth.service';
import { RunawayGuard } from '../../core/diagnostics/runaway-guard';
import { MachineStatusChangedEvent, MachineStatusHubService } from '../../core/realtime/machine-status-hub.service';
import { StatCard, StatCardTrend } from '../../shared/stat-card/stat-card';
import { SyncStatusPanel } from '../../shared/sync-status/sync-status-panel';
import { MasterDataService, ReasonCodeDto } from '../master-data/master-data.service';
import { OeeReportDto, OeeReportService, OeeTrendPointDto } from '../reports/oee-report.service';
import { DashboardService, MachineStatusDto } from './dashboard.service';
import { LossPieChart } from './loss-pie-chart';
import { MachineStatusTable } from './machine-status-table';
import { OeeTrendChart } from './oee-trend-chart';
import { ReasonCodePicker } from './reason-code-picker';

const PULSE_DURATION_MS = 700;

const TREND_RANGE_OPTIONS = [7, 14, 30] as const;

/** `/api/reports/**` is Manager/Viewer/Admin only (Story 4.1 AC #3) — an Operator's dashboard simply omits the OEE panels rather than rendering four failed requests. */
const REPORTS_ACCESS_ROLES = ['Admin', 'Manager', 'Viewer'];

type TrendMetric = Exclude<keyof OeeTrendPointDto, 'date'>;

/**
 * Real-time dashboard (Story 2.2, FR-004, NFR-1). Loads the caller's scoped machine states once,
 * then keeps them live via SignalR — an incoming event for a `machineId` not currently in the list
 * is ignored (defense-in-depth client-side scope filter; see Story 2.2 Dev Notes on AD-8's
 * single site-wide hub not doing per-connection scoping itself).
 *
 * Story 2.5: tapping a `Stopped` row opens the Reason Code Picker with that machine's Site's
 * active-only Reason Codes; selecting one records it and closes the picker. A 404 (the machine
 * resumed before the tap landed — Application layer's `DowntimeEventNotOpenException`) is a
 * legitimate race, not a crash — the picker still just closes.
 *
 * The live machine list is a table rather than Story 2.2's wall of full-bleed tiles (see
 * `MachineStatusTable`), and the OEE figures/trend that used to live only on the Reports page are
 * surfaced here as the page's headline.
 */
@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [TranslatePipe, MachineStatusTable, ReasonCodePicker, LossPieChart, SyncStatusPanel, StatCard, OeeTrendChart],
  template: `
    <div class="dashboard">
      <div class="dashboard-header">
        <div>
          <h2>{{ (appMode.isCentral() ? 'dashboard.centralAggregateTitle' : 'nav.dashboard') | translate }}</h2>
          <p class="dashboard-header__subtitle">
            {{ (appMode.isCentral() ? 'dashboard.centralAggregateSubtitle' : 'dashboard.subtitle') | translate }}
          </p>
        </div>
        @if (canReadReports()) {
          <div class="dashboard-header__ranges">
            @for (option of rangeOptions; track option) {
              <button
                type="button"
                class="dashboard-header__range"
                [class.dashboard-header__range--active]="trendDays() === option"
                (click)="onRangeChange(option)"
                [attr.data-testid]="'dashboard-range-' + option"
              >
                {{ 'dashboard.range.days' | translate: { days: option } }}
              </button>
            }
          </div>
        }
      </div>

      @if (appMode.isCentral()) {
        <app-sync-status-panel />
      }

      @if (canReadReports()) {
        <div class="dashboard-stats" data-testid="dashboard-stats">
          <app-stat-card
            [label]="'reports.oee' | translate"
            [value]="percent(report()?.oeePercent)"
            unit="%"
            icon="pi-gauge"
            accent="#0284c7"
            [delta]="delta('oeePercent')"
            [deltaCaption]="'dashboard.stat.vsPreviousDay' | translate"
            [trend]="trend('oeePercent')"
            testId="dashboard-stat-oee"
          />
          <app-stat-card
            [label]="'reports.availability' | translate"
            [value]="percent(report()?.availabilityPercent)"
            unit="%"
            icon="pi-clock"
            accent="#38bdf8"
            [delta]="delta('availabilityPercent')"
            [deltaCaption]="'dashboard.stat.vsPreviousDay' | translate"
            [trend]="trend('availabilityPercent')"
            testId="dashboard-stat-availability"
          />
          <app-stat-card
            [label]="'reports.performance' | translate"
            [value]="percent(report()?.performancePercent)"
            unit="%"
            icon="pi-bolt"
            accent="#2dd4bf"
            [delta]="delta('performancePercent')"
            [deltaCaption]="'dashboard.stat.vsPreviousDay' | translate"
            [trend]="trend('performancePercent')"
            testId="dashboard-stat-performance"
          />
          <app-stat-card
            [label]="'reports.quality' | translate"
            [value]="percent(report()?.qualityPercent)"
            unit="%"
            icon="pi-verified"
            accent="#818cf8"
            [delta]="delta('qualityPercent')"
            [deltaCaption]="'dashboard.stat.vsPreviousDay' | translate"
            [trend]="trend('qualityPercent')"
            testId="dashboard-stat-quality"
          />
          @if (!appMode.isCentral()) {
            <app-stat-card
              [label]="'dashboard.stat.running' | translate"
              [value]="runningCount() + '/' + machines().length"
              icon="pi-cog"
              accent="#16a34a"
              testId="dashboard-stat-running"
            />
          }
        </div>
      }

      @if (canReadReports()) {
        <div class="dashboard-row dashboard-row--charts">
          <app-oee-trend-chart [points]="trendPoints()" />
          <app-loss-pie-chart [equipmentOptions]="equipmentOptions()" />
        </div>
      } @else {
        <app-loss-pie-chart [equipmentOptions]="equipmentOptions()" />
      }

      @if (!appMode.isCentral()) {
        <div class="dashboard-row dashboard-row--fleet">
          @if (loadError()) {
            <div class="dashboard-empty-state" data-testid="dashboard-load-error">
              <i class="pi pi-exclamation-triangle" aria-hidden="true"></i>
              <div class="dashboard-empty-state__title">{{ 'dashboard.loadError.title' | translate }}</div>
              <div class="dashboard-empty-state__message">{{ 'dashboard.loadError.message' | translate }}</div>
              <button type="button" class="dashboard-empty-state__action" data-testid="dashboard-load-error-retry" (click)="retryLoad()">
                {{ 'dashboard.loadError.retry' | translate }}
              </button>
            </div>
          } @else if (loaded() && machines().length === 0) {
            <div class="dashboard-empty-state" data-testid="dashboard-empty-state">
              <i class="pi pi-info-circle" aria-hidden="true"></i>
              <div class="dashboard-empty-state__title">{{ 'dashboard.emptyState.title' | translate }}</div>
              <div class="dashboard-empty-state__message">{{ 'dashboard.emptyState.message' | translate }}</div>
            </div>
          } @else {
            <app-machine-status-table
              [machines]="machines()"
              [recentlyUpdated]="recentlyUpdated()"
              [noSignalThresholdSeconds]="noSignalThresholdSeconds()"
              (rowTapped)="onCardTapped($event)"
            />
          }

          <div class="dashboard-side">
            <div class="dashboard-note" data-testid="dashboard-top-downtime-reason">
              <span class="dashboard-note__label">{{ 'reports.topDowntimeReason.title' | translate }}</span>
              @if (report()?.topDowntimeReasonName; as name) {
                <strong class="dashboard-note__value">{{ name }}</strong>
                <span class="dashboard-note__caption">
                  {{ 'reports.topDowntimeReason.seconds' | translate: { seconds: report()!.topDowntimeReasonSeconds } }}
                </span>
              } @else {
                <strong class="dashboard-note__value dashboard-note__value--muted">
                  {{ (canReadReports() ? 'reports.topDowntimeReason.empty' : 'dashboard.stat.reportsRestricted') | translate }}
                </strong>
              }
            </div>
            <div class="dashboard-note" data-testid="dashboard-quality-reject">
              <span class="dashboard-note__label">{{ 'reports.qualityReject' | translate }}</span>
              <strong class="dashboard-note__value">{{ report()?.qualityRejectQuantity ?? '—' }}</strong>
              <span class="dashboard-note__caption">{{ 'dashboard.stat.today' | translate }}</span>
            </div>
            <div class="dashboard-note" data-testid="dashboard-fleet-summary">
              <span class="dashboard-note__label">{{ 'dashboard.stat.fleet' | translate }}</span>
              <strong class="dashboard-note__value">{{ machines().length }}</strong>
              <span class="dashboard-note__caption">
                {{ 'dashboard.stat.runningStopped' | translate: { running: runningCount(), stopped: stoppedCount() } }}
              </span>
            </div>
          </div>
        </div>

        <app-reason-code-picker
          [open]="pickerOpen()"
          [reasonCodes]="pickerReasonCodes()"
          (reasonSelected)="onReasonSelected($event)"
          (closed)="closePicker()"
        />
      }
    </div>
  `,
  styles: [
    `
      .dashboard {
        display: flex;
        flex-direction: column;
        gap: 1.25rem;
      }

      .dashboard-header {
        display: flex;
        flex-wrap: wrap;
        align-items: flex-start;
        justify-content: space-between;
        gap: 0.75rem;

        h2 {
          margin: 0;
          font-size: 1.5rem;
          font-weight: 800;
          letter-spacing: -0.01em;
          color: var(--p-surface-900, #0f172a);
        }
      }

      .dashboard-header__subtitle {
        margin: 0.25rem 0 0;
        color: var(--p-surface-500, #64748b);
        font-size: 0.9rem;
      }

      .dashboard-header__ranges {
        display: flex;
        gap: 0.3rem;
        background: var(--p-surface-0, #fff);
        border: 1px solid var(--p-surface-200, #e5e7eb);
        border-radius: 999px;
        padding: 0.25rem;
      }

      .dashboard-header__range {
        border: none;
        background: transparent;
        border-radius: 999px;
        padding: 0.35rem 0.85rem;
        font-size: 0.8rem;
        font-weight: 600;
        color: var(--p-surface-600, #475569);
        cursor: pointer;
      }

      .dashboard-header__range--active {
        background: var(--p-primary-color, #10b981);
        color: #fff;
      }

      .dashboard-stats {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(190px, 1fr));
        gap: 1rem;
      }

      .dashboard-row {
        display: grid;
        gap: 1.25rem;
        align-items: start;
      }

      .dashboard-row--charts {
        grid-template-columns: minmax(0, 1.6fr) minmax(0, 1fr);
      }

      .dashboard-row--fleet {
        grid-template-columns: minmax(0, 2.2fr) minmax(0, 1fr);
      }

      @media (max-width: 1100px) {
        .dashboard-row--charts,
        .dashboard-row--fleet {
          grid-template-columns: minmax(0, 1fr);
        }
      }

      .dashboard-side {
        display: flex;
        flex-direction: column;
        gap: 1rem;
      }

      .dashboard-note {
        background: var(--p-surface-0, #fff);
        border: 1px solid var(--p-surface-200, #e5e7eb);
        border-radius: var(--app-card-radius, 14px);
        box-shadow: var(--app-shadow-sm);
        padding: 1.1rem 1.25rem;
        display: flex;
        flex-direction: column;
        gap: 0.3rem;
      }

      .dashboard-note__label {
        font-size: 0.82rem;
        font-weight: 600;
        color: var(--p-surface-500, #64748b);
      }

      .dashboard-note__value {
        font-size: 1.3rem;
        font-weight: 800;
        letter-spacing: -0.01em;
        color: var(--p-primary-color, #10b981);
        line-height: 1.2;
      }

      .dashboard-note__value--muted {
        font-size: 1rem;
        color: var(--p-surface-500, #64748b);
      }

      .dashboard-note__caption {
        font-size: 0.8rem;
        color: var(--p-surface-500, #64748b);
      }

      .dashboard-empty-state {
        text-align: center;
        padding: 3.5rem 1.5rem;
        background: var(--p-surface-0, #fff);
        border: 1px solid var(--p-surface-200, #e5e7eb);
        border-radius: var(--app-panel-radius, 16px);
        box-shadow: var(--app-shadow-sm);

        i {
          font-size: 2.5rem;
          color: var(--p-surface-300, #cbd5e1);
        }
      }

      .dashboard-empty-state__title {
        font-size: 1.15rem;
        font-weight: 700;
        margin-top: 1rem;
        color: var(--p-surface-900, #0f172a);
      }

      .dashboard-empty-state__message {
        margin-top: 0.35rem;
        color: var(--p-surface-500, #64748b);
      }

      .dashboard-empty-state__action {
        margin-top: 1.25rem;
        border: none;
        background: var(--p-primary-color, #10b981);
        color: #fff;
        font-weight: 600;
        padding: 0.6rem 1.5rem;
        border-radius: 999px;
        cursor: pointer;
        transition:
          filter 0.15s ease,
          transform 0.1s ease;

        &:hover {
          filter: brightness(0.95);
        }

        &:active {
          transform: scale(0.98);
        }
      }
    `,
  ],
})
export class DashboardPage implements OnInit, OnDestroy {
  private readonly machinesSignal = signal<MachineStatusDto[]>([]);
  private readonly recentlyUpdatedSignal = signal<ReadonlySet<string>>(new Set());
  private readonly noSignalThresholdSecondsSignal = signal(60);
  private readonly loadedSignal = signal(false);
  private readonly loadErrorSignal = signal(false);
  private readonly pickerOpenSignal = signal(false);
  private readonly pickerReasonCodesSignal = signal<ReasonCodeDto[]>([]);
  private readonly reportSignal = signal<OeeReportDto | null>(null);
  private readonly trendPointsSignal = signal<OeeTrendPointDto[]>([]);
  private readonly trendDaysSignal = signal<number>(TREND_RANGE_OPTIONS[0]);
  private pickerMachineId: string | null = null;
  private readonly runawayGuard = new RunawayGuard('DashboardPage.applyBatch');

  readonly machines = this.machinesSignal.asReadonly();
  readonly recentlyUpdated = this.recentlyUpdatedSignal.asReadonly();
  readonly noSignalThresholdSeconds = this.noSignalThresholdSecondsSignal.asReadonly();
  /** True once the initial `listMachineStates()` load resolves — distinguishes "still loading" from "loaded and genuinely empty" (Story 2.4 AC #2), unrelated to a single row's own awaiting-signal state (Story 2.2). */
  readonly loaded = this.loadedSignal.asReadonly();
  readonly loadError = this.loadErrorSignal.asReadonly();
  readonly pickerOpen = this.pickerOpenSignal.asReadonly();
  readonly pickerReasonCodes = this.pickerReasonCodesSignal.asReadonly();
  readonly report = this.reportSignal.asReadonly();
  readonly trendPoints = this.trendPointsSignal.asReadonly();
  readonly trendDays = this.trendDaysSignal.asReadonly();
  readonly rangeOptions = TREND_RANGE_OPTIONS;

  readonly canReadReports = computed(() => {
    const role = this.auth.role();
    return role !== null && REPORTS_ACCESS_ROLES.includes(role);
  });

  readonly runningCount = computed(() => this.machinesSignal().filter((m) => m.status === 'Running').length);
  readonly stoppedCount = computed(() => this.machinesSignal().filter((m) => m.status === 'Stopped' || m.status === 'Fault').length);

  /** Story 3.1's Equipment dropdown source — reuses this page's already-scoped machine list (Story 2.2) instead of a redundant endpoint. */
  readonly equipmentOptions = computed(() =>
    this.machinesSignal().map((m) => ({ machineId: m.machineId, machineName: m.machineName })),
  );

  constructor(
    private readonly dashboardService: DashboardService,
    private readonly masterDataService: MasterDataService,
    private readonly oeeReport: OeeReportService,
    private readonly auth: AuthService,
    private readonly hub: MachineStatusHubService,
    readonly appMode: AppModeService,
  ) {
    // `untracked` is load-bearing, not a tidiness nicety: applying an event reads `machinesSignal()`
    // and then writes it back. Without this, that read registers as a dependency of the effect, the
    // write immediately invalidates it, and the effect re-runs forever — allocating a fresh array,
    // Set and setTimeout on every pass until the browser tab runs out of memory (~1 minute).
    // The effect must depend on the hub signal alone.
    effect(() => {
      const event = this.hub.lastEvent();
      if (event) {
        untracked(() => this.applyEvent(event));
      }
    });
    effect(() => {
      const batch = this.hub.lastBatch();
      if (batch) {
        untracked(() => this.applyBatch(batch));
      }
    });
  }

  async ngOnInit(): Promise<void> {
    await this.appMode.load();

    if (this.canReadReports()) {
      void this.loadOeeFigures();
    }

    if (this.appMode.isCentral()) {
      // Central never receives MachineState (Story 5.1's sync payload deliberately excludes live
      // signal state) — every synced Machine would otherwise show a permanent false "no signal."
      return;
    }

    await this.loadMachineStates();
  }

  async retryLoad(): Promise<void> {
    await this.loadMachineStates();
  }

  async onRangeChange(days: number): Promise<void> {
    this.trendDaysSignal.set(days);
    await this.loadOeeFigures();
  }

  percent(ratio: number | null | undefined): string {
    return ratio === null || ratio === undefined ? '—' : (ratio * 100).toFixed(1);
  }

  /** Today vs the preceding day in the loaded trend — the same figure the KPI card headlines, so the two can never disagree. */
  delta(metric: TrendMetric): string | null {
    const change = this.metricChange(metric);
    return change === null ? null : `${change >= 0 ? '+' : ''}${change.toFixed(1)}%`;
  }

  trend(metric: TrendMetric): StatCardTrend {
    const change = this.metricChange(metric);
    if (change === null || Math.abs(change) < 0.05) {
      return 'flat';
    }
    return change > 0 ? 'up' : 'down';
  }

  private metricChange(metric: TrendMetric): number | null {
    const points = this.trendPointsSignal();
    if (points.length < 2) {
      return null;
    }
    return (points[points.length - 1][metric] - points[points.length - 2][metric]) * 100;
  }

  private async loadOeeFigures(): Promise<void> {
    const today = new Date();
    try {
      const [report, trendPoints] = await Promise.all([
        this.oeeReport.getReport('Day', today),
        this.oeeReport.getDailyTrend(today, this.trendDaysSignal()),
      ]);
      this.reportSignal.set(report);
      this.trendPointsSignal.set(trendPoints);
    } catch {
      // The OEE panels are supplementary to the live machine list — a failed report request leaves
      // them showing em-dashes rather than blocking the whole page behind an error state.
      this.reportSignal.set(null);
      this.trendPointsSignal.set([]);
    }
  }

  private async loadMachineStates(): Promise<void> {
    this.loadErrorSignal.set(false);
    try {
      const result = await this.dashboardService.listMachineStates();
      this.machinesSignal.set(result.machines);
      this.noSignalThresholdSecondsSignal.set(result.noSignalThresholdSeconds);
      this.loadedSignal.set(true);
      this.hub.connect();
    } catch {
      this.loadErrorSignal.set(true);
    }
  }

  ngOnDestroy(): void {
    this.hub.disconnect();
  }

  async onCardTapped(machineId: string): Promise<void> {
    const machine = this.machinesSignal().find((m) => m.machineId === machineId);
    if (!machine) {
      return;
    }

    const reasonCodes = await this.masterDataService.listReasonCodes(machine.siteId);
    this.pickerMachineId = machineId;
    this.pickerReasonCodesSignal.set(reasonCodes.filter((r) => r.isActive));
    this.pickerOpenSignal.set(true);
  }

  async onReasonSelected(reasonCodeId: string): Promise<void> {
    const machineId = this.pickerMachineId;
    this.closePicker();
    if (!machineId) {
      return;
    }

    try {
      await this.dashboardService.recordDowntimeReason(machineId, reasonCodeId);
    } catch {
      // A 404 here means the machine already resumed before the tap landed — a legitimate race
      // (Application layer's DowntimeEventNotOpenException), not something to crash over.
    }
  }

  closePicker(): void {
    this.pickerOpenSignal.set(false);
    this.pickerReasonCodesSignal.set([]);
    this.pickerMachineId = null;
  }

  private applyEvent(event: MachineStatusChangedEvent): void {
    this.applyBatch([event]);
  }

  /** Applies every event in one array copy + one signal write, regardless of batch size — a tick
   * that changes the whole fleet still costs one re-render, not one per machine. */
  private applyBatch(events: MachineStatusChangedEvent[]): void {
    // Last-resort net against a re-entrant update loop taking the whole tab down (see RunawayGuard).
    // A real fleet updates at most once per simulator/PLC tick, so this never trips in normal use.
    if (!this.runawayGuard.check()) {
      return;
    }

    const byMachineId = new Map(events.map((e) => [e.machineId, e]));
    const current = this.machinesSignal();
    let changed = false;
    const updated = current.map((machine) => {
      const event = byMachineId.get(machine.machineId);
      if (!event) {
        return machine;
      }
      changed = true;
      return {
        ...machine,
        status: event.status as MachineStatusDto['status'],
        counter: event.counter,
        lastReportedAt: event.lastReportedAt,
      };
    });

    if (!changed) {
      return;
    }

    this.machinesSignal.set(updated);
    this.markRecentlyUpdated(updated.filter((m) => byMachineId.has(m.machineId)).map((m) => m.machineId));
  }

  private markRecentlyUpdated(machineIds: string[]): void {
    const ids = new Set(this.recentlyUpdatedSignal());
    for (const machineId of machineIds) {
      ids.add(machineId);
    }
    this.recentlyUpdatedSignal.set(ids);

    setTimeout(() => {
      const cleared = new Set(this.recentlyUpdatedSignal());
      for (const machineId of machineIds) {
        cleared.delete(machineId);
      }
      this.recentlyUpdatedSignal.set(cleared);
    }, PULSE_DURATION_MS);
  }
}
