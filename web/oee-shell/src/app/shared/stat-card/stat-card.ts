import { Component, Input } from '@angular/core';

export type StatCardTrend = 'up' | 'down' | 'flat';

/** KPI tile: icon, label, one big figure and an optional period-over-period delta. Presentational only — the owner supplies already-formatted strings. */
@Component({
  selector: 'app-stat-card',
  standalone: true,
  template: `
    <div class="stat-card" [attr.data-testid]="testId">
      <div class="stat-card__head">
        <span class="stat-card__icon" [style.color]="accent" [style.background]="'color-mix(in srgb, ' + accent + ' 14%, transparent)'">
          <i class="pi {{ icon }}" aria-hidden="true"></i>
        </span>
        <span class="stat-card__label">{{ label }}</span>
      </div>
      <div class="stat-card__value">
        {{ value }}<span class="stat-card__unit">{{ unit }}</span>
      </div>
      @if (delta) {
        <div class="stat-card__delta stat-card__delta--{{ trend }}">
          <i class="pi {{ trend === 'up' ? 'pi-arrow-up-right' : trend === 'down' ? 'pi-arrow-down-right' : 'pi-minus' }}" aria-hidden="true"></i>
          {{ delta }}<span class="stat-card__delta-caption">{{ deltaCaption }}</span>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .stat-card {
        background: var(--p-surface-0, #fff);
        border: 1px solid var(--p-surface-200, #e5e7eb);
        border-radius: var(--app-card-radius, 14px);
        box-shadow: var(--app-shadow-sm);
        padding: 1.1rem 1.25rem;
        display: flex;
        flex-direction: column;
        gap: 0.55rem;
      }

      .stat-card__head {
        display: flex;
        align-items: center;
        gap: 0.6rem;
      }

      .stat-card__icon {
        display: grid;
        place-items: center;
        width: 2rem;
        height: 2rem;
        border-radius: 9px;
        font-size: 0.9rem;
        flex: none;
      }

      .stat-card__label {
        font-size: 0.85rem;
        font-weight: 600;
        color: var(--p-surface-500, #64748b);
      }

      .stat-card__value {
        font-size: 1.9rem;
        font-weight: 800;
        line-height: 1.05;
        letter-spacing: -0.02em;
        color: var(--p-surface-900, #0f172a);
      }

      .stat-card__unit {
        font-size: 1rem;
        font-weight: 600;
        margin-left: 0.15rem;
        color: var(--p-surface-500, #64748b);
      }

      .stat-card__delta {
        display: flex;
        align-items: center;
        gap: 0.3rem;
        font-size: 0.8rem;
        font-weight: 600;

        i {
          font-size: 0.7rem;
        }
      }

      .stat-card__delta--up {
        color: #16a34a;
      }

      .stat-card__delta--down {
        color: #dc2626;
      }

      .stat-card__delta--flat {
        color: var(--p-surface-500, #64748b);
      }

      .stat-card__delta-caption {
        margin-left: 0.3rem;
        font-weight: 500;
        color: var(--p-surface-500, #64748b);
      }
    `,
  ],
})
export class StatCard {
  @Input({ required: true }) label = '';
  @Input({ required: true }) value = '';
  @Input() unit = '';
  @Input() icon = 'pi-chart-bar';
  @Input() accent = '#0284c7';
  @Input() delta: string | null = null;
  @Input() deltaCaption = '';
  @Input() trend: StatCardTrend = 'flat';
  @Input() testId = 'stat-card';
}
