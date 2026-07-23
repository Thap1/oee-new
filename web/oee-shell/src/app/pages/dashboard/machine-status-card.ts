import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Skeleton } from 'primeng/skeleton';
import { ClockTickService } from '../../core/realtime/clock-tick.service';
import { MachineStatusDto } from './dashboard.service';
import { QualityRejectControl } from './quality-reject-control';

const STATUS_ICON: Record<string, string> = {
  Running: 'pi-play',
  Stopped: 'pi-stop-circle',
  Idle: 'pi-pause',
  Fault: 'pi-exclamation-triangle',
};

const NO_SIGNAL_ICON = 'pi-ban';

/**
 * Machine Status Card (Story 2.2, UX-DR6/13/14). `snapshot.status === null` renders the skeleton
 * loading state (UX-DR12) — a machine that has never reported is not an error, see Story 2.1/2.2 Dev
 * Notes. `justUpdated` drives a one-shot pulse (UX-DR6) — never a jarring flash.
 *
 * No-signal (Story 2.3, FR-003/UX-DR9) is a **presentation-layer override**: once
 * `now - lastReportedAt` exceeds `noSignalThresholdSeconds`, the gray no-signal variant wins over
 * whatever `snapshot.status` says — checked *before* branching on status, so a stale `Stopped`
 * reading can never masquerade as a real stoppage (AC #2).
 *
 * `Fault` has no dedicated DESIGN.md color token (only running/stopped/idle/no-signal are defined) —
 * it reuses `--status-stopped` (closest analog: not producing, needs attention) with its own
 * icon/label so color is still never the only signal (Accessibility Floor).
 */
@Component({
  selector: 'app-machine-status-card',
  standalone: true,
  imports: [TranslatePipe, Skeleton, QualityRejectControl],
  template: `
    @if (snapshot.status === null) {
      <div class="machine-status-card machine-status-card--skeleton" data-testid="machine-status-card">
        <p-skeleton width="100%" height="148px" />
      </div>
    } @else if (isNoSignal()) {
      <div class="machine-status-card machine-status-card--no-signal" data-testid="machine-status-card">
        <i class="pi {{ noSignalIcon }} machine-status-card__icon" aria-hidden="true"></i>
        <div class="machine-status-card__name">{{ snapshot.machineName }}</div>
        <div class="machine-status-card__status">{{ 'dashboard.status.noSignal' | translate: { minutes: noSignalElapsedMinutes() } }}</div>
      </div>
    } @else {
      <div [class]="cardClasses()" data-testid="machine-status-card" (click)="onClick()">
        <i class="pi {{ iconFor(snapshot.status) }} machine-status-card__icon" aria-hidden="true"></i>
        <div class="machine-status-card__name">{{ snapshot.machineName }}</div>
        <div class="machine-status-card__status">{{ 'dashboard.status.' + snapshot.status | translate }}</div>
        <div class="machine-status-card__actions" (click)="$event.stopPropagation()">
          <app-quality-reject-control [machineId]="snapshot.machineId" />
        </div>
      </div>
    }
  `,
  styles: [
    `
      .machine-status-card {
        position: relative;
        min-height: 148px;
        padding: 20px 22px;
        border-radius: var(--app-card-radius, 14px);
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
        justify-content: center;
        box-shadow: var(--app-shadow-md);
        transition:
          transform 0.15s ease,
          box-shadow 0.15s ease;
      }

      .machine-status-card__icon {
        position: absolute;
        top: 16px;
        right: 18px;
        font-size: 1.25rem;
        opacity: 0.55;
      }

      .machine-status-card__name {
        font-size: var(--shopfloor-display-size, 56px);
        font-weight: 700;
        line-height: 1.1;
        letter-spacing: -0.01em;
      }

      .machine-status-card__status {
        font-size: var(--shopfloor-label-size, 20px);
        font-weight: 600;
        opacity: 0.92;
      }

      .machine-status-card__actions {
        position: absolute;
        bottom: 6px;
        right: 6px;
      }

      .machine-status-card--running {
        background: linear-gradient(155deg, var(--status-running), color-mix(in srgb, var(--status-running) 80%, black));
        color: var(--status-running-fg);
      }

      .machine-status-card--stopped,
      .machine-status-card--fault {
        background: linear-gradient(155deg, var(--status-stopped), color-mix(in srgb, var(--status-stopped) 80%, black));
        color: var(--status-stopped-fg);
        cursor: pointer;
      }

      .machine-status-card--stopped:hover,
      .machine-status-card--fault:hover {
        transform: translateY(-2px);
        box-shadow: var(--app-shadow-lg);
      }

      .machine-status-card--idle {
        background: linear-gradient(155deg, var(--status-idle), color-mix(in srgb, var(--status-idle) 82%, black));
        color: var(--status-idle-fg);
      }

      .machine-status-card--no-signal {
        background: var(--status-no-signal);
        color: var(--status-no-signal-fg);
        box-shadow: none;
        opacity: 0.85;
      }

      .machine-status-card--skeleton {
        min-height: 148px;
        padding: 0;
        border-radius: var(--app-card-radius, 14px);
        overflow: hidden;
        box-shadow: var(--app-shadow-sm);

        ::ng-deep .p-skeleton {
          border-radius: var(--app-card-radius, 14px);
        }
      }

      .machine-status-card--pulse {
        animation: machine-status-pulse 600ms ease-out;
      }

      @keyframes machine-status-pulse {
        0% {
          opacity: 0.6;
          transform: scale(0.98);
        }
        100% {
          opacity: 1;
          transform: scale(1);
        }
      }
    `,
  ],
})
export class MachineStatusCard {
  private readonly clockTick = inject(ClockTickService);

  @Input({ required: true }) snapshot!: MachineStatusDto;
  @Input() justUpdated = false;
  @Input() noSignalThresholdSeconds = 60;
  /** Story 2.5, UX-DR7: the Reason Code Picker opens only when tapping a card that's actually Stopped — tapping any other status does nothing. */
  @Output() cardTapped = new EventEmitter<string>();

  readonly noSignalIcon = NO_SIGNAL_ICON;

  statusClass(): string {
    return (this.snapshot.status ?? '').toLowerCase();
  }

  /** Single combined class binding — mixing `[class]` with a separate `[class.foo]` toggle on the same element is an Angular anti-pattern that can cause `ExpressionChangedAfterItHasBeenCheckedError`. */
  cardClasses(): string {
    const classes = ['machine-status-card', `machine-status-card--${this.statusClass()}`];
    if (this.justUpdated) {
      classes.push('machine-status-card--pulse');
    }
    return classes.join(' ');
  }

  iconFor(status: string | null): string {
    return status ? (STATUS_ICON[status] ?? 'pi-question') : '';
  }

  onClick(): void {
    if (this.snapshot.status === 'Stopped') {
      this.cardTapped.emit(this.snapshot.machineId);
    }
  }

  /** A machine that has never reported (`status: null`) is the skeleton case (Story 2.2), not no-signal — only a machine that WAS reporting and has since gone quiet is this story's concern. */
  isNoSignal(): boolean {
    if (this.snapshot.lastReportedAt === null) {
      return false;
    }
    return this.elapsedSeconds() > this.noSignalThresholdSeconds;
  }

  noSignalElapsedMinutes(): number {
    return Math.max(1, Math.floor(this.elapsedSeconds() / 60));
  }

  private elapsedSeconds(): number {
    const lastReportedAtMs = new Date(this.snapshot.lastReportedAt!).getTime();
    return (this.clockTick.nowMs() - lastReportedAtMs) / 1000;
  }
}
