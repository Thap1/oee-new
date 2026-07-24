import { Component, OnInit, signal } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { TableModule } from 'primeng/table';
import { DowntimeHistoryEntryDto, DowntimeService } from './downtime.service';

/**
 * Downtime history (nav "Dừng máy") — a flat list of DowntimeEvent rows (open or closed) across the
 * caller's scoped machines, most recent first. Complements the Dashboard (live status + tap-to-assign
 * reason on the currently open event) and Reports' Loss Pie Chart (aggregated totals) with the
 * individual-event view neither of those provides.
 */
@Component({
  selector: 'app-downtime-page',
  standalone: true,
  imports: [TranslatePipe, TableModule],
  template: `
    <h2>{{ 'nav.downtime' | translate }}</h2>
    @if (error()) {
      <div class="downtime-empty-state" data-testid="downtime-load-error">
        <i class="pi pi-exclamation-triangle" aria-hidden="true"></i>
        <div>{{ 'downtime.loadError' | translate }}</div>
      </div>
    } @else if (loaded() && entries().length === 0) {
      <div class="downtime-empty-state" data-testid="downtime-empty-state">
        <i class="pi pi-info-circle" aria-hidden="true"></i>
        <div>{{ 'downtime.emptyState' | translate }}</div>
      </div>
    } @else {
      <p-table [value]="entries()" dataKey="id" [loading]="!loaded()">
        <ng-template pTemplate="header">
          <tr>
            <th>{{ 'downtime.machine' | translate }}</th>
            <th>{{ 'downtime.reason' | translate }}</th>
            <th>{{ 'downtime.startedAt' | translate }}</th>
            <th>{{ 'downtime.endedAt' | translate }}</th>
            <th>{{ 'downtime.duration' | translate }}</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-entry>
          <tr [attr.data-testid]="'downtime-row-' + entry.id">
            <td>{{ entry.machineName }}</td>
            <td>{{ entry.reasonCodeName ?? ('downtime.unattributed' | translate) }}</td>
            <td>{{ formatDateTime(entry.startedAt) }}</td>
            <td>
              @if (entry.endedAt) {
                {{ formatDateTime(entry.endedAt) }}
              } @else {
                <span class="downtime-ongoing" data-testid="downtime-ongoing-badge">{{ 'downtime.ongoing' | translate }}</span>
              }
            </td>
            <td>{{ entry.durationSeconds !== null ? formatDuration(entry.durationSeconds) : '—' }}</td>
          </tr>
        </ng-template>
      </p-table>
    }
  `,
  styles: [
    `
      h2 {
        margin: 0 0 1.25rem;
        font-size: 1.5rem;
        font-weight: 800;
        letter-spacing: -0.01em;
        color: var(--p-surface-900, #0f172a);
      }

      .downtime-empty-state {
        text-align: center;
        padding: 3.5rem 1.5rem;
        background: var(--p-surface-0, #fff);
        border: 1px solid var(--p-surface-200, #e5e7eb);
        border-radius: var(--app-panel-radius, 16px);
        color: var(--p-surface-500, #64748b);

        i {
          font-size: 2.5rem;
          color: var(--p-surface-300, #cbd5e1);
          display: block;
          margin-bottom: 1rem;
        }
      }

      .downtime-ongoing {
        font-weight: 700;
        color: var(--status-stopped, #ef4444);
      }
    `,
  ],
})
export class DowntimePage implements OnInit {
  private readonly entriesSignal = signal<DowntimeHistoryEntryDto[]>([]);
  private readonly loadedSignal = signal(false);
  private readonly errorSignal = signal(false);

  readonly entries = this.entriesSignal.asReadonly();
  readonly loaded = this.loadedSignal.asReadonly();
  readonly error = this.errorSignal.asReadonly();

  constructor(
    private readonly downtimeService: DowntimeService,
    private readonly translate: TranslateService,
  ) {}

  async ngOnInit(): Promise<void> {
    try {
      this.entriesSignal.set(await this.downtimeService.listHistory());
    } catch {
      this.errorSignal.set(true);
    } finally {
      this.loadedSignal.set(true);
    }
  }

  formatDateTime(iso: string): string {
    return new Date(iso).toLocaleString(this.translate.currentLang() === 'en' ? 'en-US' : 'vi-VN');
  }

  formatDuration(totalSeconds: number): string {
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = Math.floor(totalSeconds % 60);
    const parts: string[] = [];
    if (hours > 0) {
      parts.push(`${hours}h`);
    }
    if (hours > 0 || minutes > 0) {
      parts.push(`${minutes}m`);
    }
    parts.push(`${seconds}s`);
    return parts.join(' ');
  }
}
