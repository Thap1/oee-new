import { Component, OnInit, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { SiteSyncStatusDto, SyncStatusService } from './sync-status.service';

interface SyncBadgeViewModel {
  siteId: string;
  siteName: string;
  isStale: boolean;
  labelKey: string;
  labelParams: { minutes: number } | Record<string, never>;
}

/**
 * Per-site "last synced X minutes ago" badge row (Story 5.3, UX-DR11) — shown at Central alongside the
 * Loss Pie Chart (Dashboard) and Reports, above whatever cross-site content those pages already render.
 * Self-contained widget with its own fetch/signals, same shape as `LossPieChart`/`LossAnalyticsService`.
 * Elapsed time is computed once at load, not live-ticking (AD-8: no real-time expectation at Central) —
 * unlike `MachineStatusCard`'s `ClockTickService`-driven no-signal label, which is a genuine real-time concern.
 */
@Component({
  selector: 'app-sync-status-panel',
  standalone: true,
  imports: [TranslatePipe],
  template: `
    <div class="sync-status-panel" data-testid="sync-status-panel">
      @for (badge of badges(); track badge.siteId) {
        <span class="sync-badge" [class.sync-badge--stale]="badge.isStale" data-testid="sync-badge">
          <i class="pi pi-sync" aria-hidden="true"></i>
          <span class="sync-badge__site">{{ badge.siteName }}</span>
          <span class="sync-badge__text">{{ badge.labelKey | translate: badge.labelParams }}</span>
        </span>
      }
    </div>
  `,
  styles: [
    `
      .sync-status-panel {
        display: flex;
        flex-wrap: wrap;
        gap: 0.5rem;
        margin-bottom: 1.25rem;
      }

      .sync-badge {
        display: inline-flex;
        align-items: center;
        gap: 0.4rem;
        padding: 0.3rem 0.75rem;
        border-radius: 999px;
        font-size: 0.85rem;
        background: var(--p-surface-100, #f1f5f9);
        color: var(--p-surface-600, #475569);

        i {
          font-size: 0.9rem;
        }
      }

      .sync-badge--stale {
        background: var(--sync-badge-stale);
        color: var(--sync-badge-stale-fg);
      }

      .sync-badge__site {
        font-weight: 700;
      }
    `,
  ],
})
export class SyncStatusPanel implements OnInit {
  private readonly statusesSignal = signal<SiteSyncStatusDto[]>([]);

  readonly badges = signal<SyncBadgeViewModel[]>([]);

  constructor(private readonly syncStatus: SyncStatusService) {}

  async ngOnInit(): Promise<void> {
    try {
      const statuses = await this.syncStatus.getStatuses();
      this.statusesSignal.set(statuses);
      this.badges.set(statuses.map((s) => this.toBadge(s)));
    } catch {
      // Informational-only widget (AC #2) — a failed fetch just means no badges render, not a page-level error.
      this.badges.set([]);
    }
  }

  private toBadge(status: SiteSyncStatusDto): SyncBadgeViewModel {
    if (status.lastSyncedAt === null) {
      return {
        siteId: status.siteId,
        siteName: status.siteName,
        isStale: status.isStale,
        labelKey: 'sync.badge.neverSynced',
        labelParams: {},
      };
    }

    const minutes = Math.max(0, Math.round((Date.now() - Date.parse(status.lastSyncedAt)) / 60_000));
    return {
      siteId: status.siteId,
      siteName: status.siteName,
      isStale: status.isStale,
      labelKey: 'sync.badge.lastSynced',
      labelParams: { minutes },
    };
  }
}
