import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { TableModule } from 'primeng/table';
import { ClockTickService } from '../../core/realtime/clock-tick.service';
import { MachineStatusDto, MachineStatusValue } from './dashboard.service';
import { QualityRejectControl } from './quality-reject-control';

const STATUS_ICON: Record<MachineStatusValue, string> = {
  Running: 'pi-play',
  Stopped: 'pi-stop-circle',
  Idle: 'pi-pause',
  Fault: 'pi-exclamation-triangle',
};

/**
 * Presentation-layer status, with Story 2.3's no-signal override folded in. `Unknown` is a machine
 * that has never reported (`status: null`, Story 2.2) — not an error, and distinct from `NoSignal`,
 * which is a machine that WAS reporting and has since gone quiet.
 */
export type DisplayStatus = MachineStatusValue | 'NoSignal' | 'Unknown';

const FILTER_OPTIONS = ['all', 'Running', 'Stopped', 'Idle', 'Fault'] as const;

type FilterOption = (typeof FILTER_OPTIONS)[number];

interface MachineRow {
  snapshot: MachineStatusDto;
  displayStatus: DisplayStatus;
  noSignalMinutes: number;
  justUpdated: boolean;
}

/**
 * Live machine status as a scannable table, replacing Story 2.2's wall of full-bleed colored tiles —
 * at 30+ machines the tile grid pushed everything else below the fold and status colour was the only
 * information carried per screenful.
 *
 * The no-signal override (Story 2.3, FR-003/UX-DR9) is checked *before* branching on status, so a
 * stale `Stopped` reading can never masquerade as a real stoppage (AC #2), and tapping a `Stopped`
 * row still opens the Reason Code Picker (Story 2.5, UX-DR7) — only the presentation changed.
 *
 * `Fault` has no dedicated DESIGN.md color token (only running/stopped/idle/no-signal are defined) —
 * it reuses `--status-stopped` with its own icon/label so color is never the only signal.
 */
@Component({
  selector: 'app-machine-status-table',
  standalone: true,
  imports: [FormsModule, TranslatePipe, TableModule, QualityRejectControl],
  template: `
    <div class="machine-table" data-testid="machine-status-table">
      <div class="machine-table__header">
        <div>
          <h3>{{ 'dashboard.machineTable.title' | translate }}</h3>
          <p>{{ 'dashboard.machineTable.subtitle' | translate: { total: machines().length } }}</p>
        </div>
        <div class="machine-table__filters">
          @for (option of filterOptions; track option) {
            <button
              type="button"
              class="machine-table__filter"
              [class.machine-table__filter--active]="statusFilter() === option"
              (click)="setFilter(option)"
              [attr.data-testid]="'machine-status-filter-' + option"
            >
              {{ 'dashboard.machineTable.filter.' + option | translate }}
              <span class="machine-table__filter-count">{{ countFor(option) }}</span>
            </button>
          }
        </div>
      </div>

      <p-table [value]="rows()" [scrollable]="true" scrollHeight="360px" [rows]="50" styleClass="machine-table__grid">
        <ng-template pTemplate="header">
          <tr>
            <th>{{ 'dashboard.machineTable.machine' | translate }}</th>
            <th>{{ 'dashboard.machineTable.status' | translate }}</th>
            <th>{{ 'dashboard.machineTable.counter' | translate }}</th>
            <th>{{ 'dashboard.machineTable.lastReported' | translate }}</th>
            <th></th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row>
          <tr
            [class.machine-table__row--actionable]="row.displayStatus === 'Stopped'"
            [class.machine-table__row--pulse]="row.justUpdated"
            (click)="onRowClick(row)"
            data-testid="machine-status-row"
          >
            <td class="machine-table__name">{{ row.snapshot.machineName }}</td>
            <td>
              <span class="machine-table__badge machine-table__badge--{{ row.displayStatus.toLowerCase() }}">
                <i class="pi {{ iconFor(row.displayStatus) }}" aria-hidden="true"></i>
                @if (row.displayStatus === 'NoSignal') {
                  {{ 'dashboard.status.noSignal' | translate: { minutes: row.noSignalMinutes } }}
                } @else if (row.displayStatus === 'Unknown') {
                  {{ 'dashboard.machineTable.awaitingSignal' | translate }}
                } @else {
                  {{ 'dashboard.status.' + row.displayStatus | translate }}
                }
              </span>
            </td>
            <td>{{ row.snapshot.counter ?? '—' }}</td>
            <td class="machine-table__muted">{{ relativeTime(row.snapshot.lastReportedAt) }}</td>
            <td class="machine-table__actions" (click)="$event.stopPropagation()">
              @if (row.displayStatus !== 'Unknown') {
                <app-quality-reject-control [machineId]="row.snapshot.machineId" />
              }
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr>
            <td colspan="5" class="machine-table__empty" data-testid="machine-status-table-empty">
              {{ 'dashboard.machineTable.noMatch' | translate }}
            </td>
          </tr>
        </ng-template>
      </p-table>
    </div>
  `,
  styles: [
    `
      .machine-table {
        background: var(--p-surface-0, #fff);
        border: 1px solid var(--p-surface-200, #e5e7eb);
        border-radius: var(--app-panel-radius, 16px);
        box-shadow: var(--app-shadow-sm);
        padding: 1.35rem 1.5rem 1rem;
      }

      .machine-table__header {
        display: flex;
        flex-wrap: wrap;
        align-items: flex-start;
        justify-content: space-between;
        gap: 0.75rem;
        margin-bottom: 1rem;

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

      .machine-table__filters {
        display: flex;
        flex-wrap: wrap;
        gap: 0.35rem;
      }

      .machine-table__filter {
        display: inline-flex;
        align-items: center;
        gap: 0.4rem;
        border: 1px solid var(--p-surface-200, #e5e7eb);
        background: var(--p-surface-0, #fff);
        border-radius: 999px;
        padding: 0.3rem 0.75rem;
        font-size: 0.78rem;
        font-weight: 600;
        color: var(--p-surface-700, #334155);
        cursor: pointer;
      }

      .machine-table__filter--active {
        border-color: var(--p-primary-color, #10b981);
        color: var(--p-primary-color, #10b981);
        background: color-mix(in srgb, var(--p-primary-color, #10b981) 8%, transparent);
      }

      .machine-table__filter-count {
        font-variant-numeric: tabular-nums;
        opacity: 0.7;
      }

      .machine-table__name {
        font-weight: 600;
        color: var(--p-surface-900, #0f172a);
      }

      .machine-table__muted {
        color: var(--p-surface-500, #64748b);
        font-size: 0.85rem;
      }

      .machine-table__actions {
        text-align: right;
      }

      .machine-table__row--actionable {
        cursor: pointer;
      }

      .machine-table__row--pulse {
        animation: machine-row-pulse 700ms ease-out;
      }

      @keyframes machine-row-pulse {
        0% {
          background: color-mix(in srgb, var(--p-primary-color, #10b981) 16%, transparent);
        }
        100% {
          background: transparent;
        }
      }

      .machine-table__badge {
        display: inline-flex;
        align-items: center;
        gap: 0.35rem;
        padding: 0.2rem 0.6rem;
        border-radius: 999px;
        font-size: 0.78rem;
        font-weight: 700;
        white-space: nowrap;

        i {
          font-size: 0.7rem;
        }
      }

      .machine-table__badge--running {
        background: color-mix(in srgb, var(--status-running) 15%, transparent);
        color: color-mix(in srgb, var(--status-running) 78%, black);
      }

      .machine-table__badge--stopped,
      .machine-table__badge--fault {
        background: color-mix(in srgb, var(--status-stopped) 15%, transparent);
        color: color-mix(in srgb, var(--status-stopped) 82%, black);
      }

      .machine-table__badge--idle {
        background: color-mix(in srgb, var(--status-idle) 18%, transparent);
        color: color-mix(in srgb, var(--status-idle) 82%, black);
      }

      .machine-table__badge--nosignal,
      .machine-table__badge--unknown {
        background: color-mix(in srgb, var(--status-no-signal) 15%, transparent);
        color: color-mix(in srgb, var(--status-no-signal) 88%, black);
      }

      .machine-table__empty {
        text-align: center;
        padding: 2rem 1rem;
        color: var(--p-surface-500, #64748b);
      }
    `,
  ],
})
export class MachineStatusTable {
  private readonly clockTick = inject(ClockTickService);

  readonly machines = input.required<MachineStatusDto[]>();
  readonly recentlyUpdated = input<ReadonlySet<string>>(new Set());
  readonly noSignalThresholdSeconds = input(60);
  /** Story 2.5, UX-DR7: only a row that is actually `Stopped` opens the Reason Code Picker. */
  readonly rowTapped = output<string>();

  readonly filterOptions = FILTER_OPTIONS;
  private readonly statusFilterSignal = signal<FilterOption>('all');
  readonly statusFilter = this.statusFilterSignal.asReadonly();

  private readonly allRows = computed<MachineRow[]>(() => {
    const threshold = this.noSignalThresholdSeconds();
    const recent = this.recentlyUpdated();
    const nowMs = this.clockTick.nowMs();

    return this.machines().map((snapshot) => {
      const elapsedSeconds = snapshot.lastReportedAt ? (nowMs - new Date(snapshot.lastReportedAt).getTime()) / 1000 : 0;
      const noSignal = snapshot.lastReportedAt !== null && elapsedSeconds > threshold;
      const displayStatus: DisplayStatus = noSignal ? 'NoSignal' : (snapshot.status ?? 'Unknown');
      return {
        snapshot,
        displayStatus,
        noSignalMinutes: Math.max(1, Math.floor(elapsedSeconds / 60)),
        justUpdated: recent.has(snapshot.machineId),
      };
    });
  });

  readonly rows = computed(() => {
    const filter = this.statusFilterSignal();
    return filter === 'all' ? this.allRows() : this.allRows().filter((row) => row.displayStatus === filter);
  });

  setFilter(option: FilterOption): void {
    this.statusFilterSignal.set(option);
  }

  countFor(option: FilterOption): number {
    return option === 'all' ? this.allRows().length : this.allRows().filter((row) => row.displayStatus === option).length;
  }

  iconFor(status: DisplayStatus): string {
    return status === 'NoSignal' ? 'pi-ban' : status === 'Unknown' ? 'pi-clock' : STATUS_ICON[status];
  }

  relativeTime(lastReportedAt: string | null): string {
    if (!lastReportedAt) {
      return '—';
    }
    const seconds = Math.max(0, (this.clockTick.nowMs() - new Date(lastReportedAt).getTime()) / 1000);
    if (seconds < 60) {
      return `${Math.floor(seconds)}s`;
    }
    if (seconds < 3600) {
      return `${Math.floor(seconds / 60)}m`;
    }
    return `${Math.floor(seconds / 3600)}h`;
  }

  onRowClick(row: MachineRow): void {
    if (row.displayStatus === 'Stopped') {
      this.rowTapped.emit(row.snapshot.machineId);
    }
  }
}
