import { Component, Input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Skeleton } from 'primeng/skeleton';
import { MachineStatusDto } from './dashboard.service';

const STATUS_ICON: Record<string, string> = {
  Running: 'pi-play',
  Stopped: 'pi-stop-circle',
  Idle: 'pi-pause',
  Fault: 'pi-exclamation-triangle',
};

/**
 * Machine Status Card (Story 2.2, UX-DR6/13/14). `snapshot.status === null` renders the skeleton
 * loading state (UX-DR12) — a machine that has never reported is not an error, see Story 2.1/2.2 Dev
 * Notes. `justUpdated` drives a one-shot pulse (UX-DR6) — never a jarring flash.
 *
 * `Fault` has no dedicated DESIGN.md color token (only running/stopped/idle/no-signal are defined) —
 * it reuses `--status-stopped` (closest analog: not producing, needs attention) with its own
 * icon/label so color is still never the only signal (Accessibility Floor).
 */
@Component({
  selector: 'app-machine-status-card',
  standalone: true,
  imports: [TranslatePipe, Skeleton],
  template: `
    @if (snapshot.status === null) {
      <div class="machine-status-card machine-status-card--skeleton" data-testid="machine-status-card">
        <p-skeleton width="100%" height="96px" />
      </div>
    } @else {
      <div
        class="machine-status-card"
        [class]="'machine-status-card--' + statusClass()"
        [class.machine-status-card--pulse]="justUpdated"
        data-testid="machine-status-card"
      >
        <i class="pi {{ iconFor(snapshot.status) }}" aria-hidden="true"></i>
        <div class="machine-status-card__name">{{ snapshot.machineName }}</div>
        <div class="machine-status-card__status">{{ 'dashboard.status.' + snapshot.status | translate }}</div>
      </div>
    }
  `,
  styles: [
    `
      .machine-status-card {
        min-height: 96px;
        padding: 24px;
        border-radius: var(--content-border-radius, 12px);
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
        justify-content: center;
      }

      .machine-status-card__name {
        font-size: var(--shopfloor-display-size, 56px);
        font-weight: 700;
        line-height: 1.1;
      }

      .machine-status-card__status {
        font-size: var(--shopfloor-label-size, 20px);
        font-weight: 600;
      }

      .machine-status-card--running {
        background: var(--status-running);
        color: var(--status-running-fg);
      }

      .machine-status-card--stopped,
      .machine-status-card--fault {
        background: var(--status-stopped);
        color: var(--status-stopped-fg);
      }

      .machine-status-card--idle {
        background: var(--status-idle);
        color: var(--status-idle-fg);
      }

      .machine-status-card--pulse {
        animation: machine-status-pulse 600ms ease-out;
      }

      @keyframes machine-status-pulse {
        0% {
          opacity: 0.6;
        }
        100% {
          opacity: 1;
        }
      }
    `,
  ],
})
export class MachineStatusCard {
  @Input({ required: true }) snapshot!: MachineStatusDto;
  @Input() justUpdated = false;

  statusClass(): string {
    return (this.snapshot.status ?? '').toLowerCase();
  }

  iconFor(status: string | null): string {
    return status ? (STATUS_ICON[status] ?? 'pi-question') : '';
  }
}
