import { Component, OnDestroy, OnInit, computed, effect, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { AppModeService } from '../../core/app-mode/app-mode.service';
import { MachineStatusChangedEvent, MachineStatusHubService } from '../../core/realtime/machine-status-hub.service';
import { MasterDataService, ReasonCodeDto } from '../master-data/master-data.service';
import { DashboardService, MachineStatusDto } from './dashboard.service';
import { LossPieChart } from './loss-pie-chart';
import { MachineStatusCard } from './machine-status-card';
import { ReasonCodePicker } from './reason-code-picker';

const PULSE_DURATION_MS = 700;

/**
 * Real-time dashboard (Story 2.2, FR-004, NFR-1). Loads the caller's scoped machine states once,
 * then keeps them live via SignalR — an incoming event for a `machineId` not currently in the list
 * is ignored (defense-in-depth client-side scope filter; see Story 2.2 Dev Notes on AD-8's
 * single site-wide hub not doing per-connection scoping itself).
 *
 * Story 2.5: tapping a `Stopped` card opens the Reason Code Picker with that machine's Site's
 * active-only Reason Codes; selecting one records it and closes the picker. A 404 (the machine
 * resumed before the tap landed — Application layer's `DowntimeEventNotOpenException`) is a
 * legitimate race, not a crash — the picker still just closes.
 */
@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [TranslatePipe, MachineStatusCard, ReasonCodePicker, LossPieChart],
  template: `
    @if (appMode.isCentral()) {
      <div class="dashboard-header">
        <h2>{{ 'dashboard.centralAggregateTitle' | translate }}</h2>
        <p class="dashboard-header__subtitle">{{ 'dashboard.centralAggregateSubtitle' | translate }}</p>
      </div>
      <app-loss-pie-chart [equipmentOptions]="[]" />
    } @else {
      <div class="dashboard-header">
        <h2>{{ 'nav.dashboard' | translate }}</h2>
        <p class="dashboard-header__subtitle">{{ 'dashboard.subtitle' | translate }}</p>
      </div>
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
        <div class="dashboard-grid">
          @for (machine of machines(); track machine.machineId) {
            <app-machine-status-card
              [snapshot]="machine"
              [justUpdated]="recentlyUpdated().has(machine.machineId)"
              [noSignalThresholdSeconds]="noSignalThresholdSeconds()"
              (cardTapped)="onCardTapped($event)"
            />
          }
        </div>
      }
      <app-loss-pie-chart [equipmentOptions]="equipmentOptions()" />
      <app-reason-code-picker
        [open]="pickerOpen()"
        [reasonCodes]="pickerReasonCodes()"
        (reasonSelected)="onReasonSelected($event)"
        (closed)="closePicker()"
      />
    }
  `,
  styles: [
    `
      .dashboard-header {
        margin-bottom: 1.5rem;

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

      .dashboard-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
        gap: 1.25rem;
        margin-bottom: 2rem;
      }

      .dashboard-empty-state {
        text-align: center;
        padding: 3.5rem 1.5rem;
        margin-bottom: 2rem;
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
  private pickerMachineId: string | null = null;

  readonly machines = this.machinesSignal.asReadonly();
  readonly recentlyUpdated = this.recentlyUpdatedSignal.asReadonly();
  readonly noSignalThresholdSeconds = this.noSignalThresholdSecondsSignal.asReadonly();
  /** True once the initial `listMachineStates()` load resolves — distinguishes "still loading" from "loaded and genuinely empty" (Story 2.4 AC #2), unrelated to a single card's own skeleton state (Story 2.2). */
  readonly loaded = this.loadedSignal.asReadonly();
  readonly loadError = this.loadErrorSignal.asReadonly();
  readonly pickerOpen = this.pickerOpenSignal.asReadonly();
  readonly pickerReasonCodes = this.pickerReasonCodesSignal.asReadonly();
  /** Story 3.1's Equipment dropdown source — reuses this page's already-scoped machine list (Story 2.2) instead of a redundant endpoint. */
  readonly equipmentOptions = computed(() =>
    this.machinesSignal().map((m) => ({ machineId: m.machineId, machineName: m.machineName })),
  );

  constructor(
    private readonly dashboardService: DashboardService,
    private readonly masterDataService: MasterDataService,
    private readonly hub: MachineStatusHubService,
    readonly appMode: AppModeService,
  ) {
    effect(() => {
      const event = this.hub.lastEvent();
      if (event) {
        this.applyEvent(event);
      }
    });
  }

  async ngOnInit(): Promise<void> {
    await this.appMode.load();
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
    const current = this.machinesSignal();
    const index = current.findIndex((m) => m.machineId === event.machineId);
    if (index === -1) {
      return;
    }

    const updated = [...current];
    updated[index] = {
      ...updated[index],
      status: event.status as MachineStatusDto['status'],
      counter: event.counter,
      lastReportedAt: event.lastReportedAt,
    };
    this.machinesSignal.set(updated);
    this.markRecentlyUpdated(event.machineId);
  }

  private markRecentlyUpdated(machineId: string): void {
    const ids = new Set(this.recentlyUpdatedSignal());
    ids.add(machineId);
    this.recentlyUpdatedSignal.set(ids);

    setTimeout(() => {
      const cleared = new Set(this.recentlyUpdatedSignal());
      cleared.delete(machineId);
      this.recentlyUpdatedSignal.set(cleared);
    }, PULSE_DURATION_MS);
  }
}
