import { Component, OnDestroy, OnInit, effect, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { MachineStatusChangedEvent, MachineStatusHubService } from '../../core/realtime/machine-status-hub.service';
import { DashboardService, MachineStatusDto } from './dashboard.service';
import { MachineStatusCard } from './machine-status-card';

const PULSE_DURATION_MS = 700;

/**
 * Real-time dashboard (Story 2.2, FR-004, NFR-1). Loads the caller's scoped machine states once,
 * then keeps them live via SignalR — an incoming event for a `machineId` not currently in the list
 * is ignored (defense-in-depth client-side scope filter; see Story 2.2 Dev Notes on AD-8's
 * single site-wide hub not doing per-connection scoping itself).
 */
@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [TranslatePipe, MachineStatusCard],
  template: `
    <h2>{{ 'nav.dashboard' | translate }}</h2>
    <div class="dashboard-grid">
      @for (machine of machines(); track machine.machineId) {
        <app-machine-status-card [snapshot]="machine" [justUpdated]="recentlyUpdated().has(machine.machineId)" />
      }
    </div>
  `,
  styles: [
    `
      .dashboard-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
        gap: 1rem;
      }
    `,
  ],
})
export class DashboardPage implements OnInit, OnDestroy {
  private readonly machinesSignal = signal<MachineStatusDto[]>([]);
  private readonly recentlyUpdatedSignal = signal<ReadonlySet<string>>(new Set());

  readonly machines = this.machinesSignal.asReadonly();
  readonly recentlyUpdated = this.recentlyUpdatedSignal.asReadonly();

  constructor(
    private readonly dashboardService: DashboardService,
    private readonly hub: MachineStatusHubService,
  ) {
    effect(() => {
      const event = this.hub.lastEvent();
      if (event) {
        this.applyEvent(event);
      }
    });
  }

  async ngOnInit(): Promise<void> {
    this.machinesSignal.set(await this.dashboardService.listMachineStates());
    this.hub.connect();
  }

  ngOnDestroy(): void {
    this.hub.disconnect();
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
