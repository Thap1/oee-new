import { Component, EventEmitter, Input, Output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { LossCategoryValue, ReasonCodeDto } from '../master-data/master-data.service';

const LOSS_CATEGORIES: LossCategoryValue[] = ['AvailabilityLoss', 'PerformanceLoss', 'QualityLoss'];

/**
 * Full-screen Reason Code Picker (Story 2.5, FR-008/009, UX-DR7). `reasonCodes` is expected to
 * already be active-only (the dashboard filters before passing it in — the Master Data admin list
 * intentionally includes inactive ones for its own screen, this component doesn't re-filter).
 * A single tap on a reason button both records and closes — no confirmation step.
 */
@Component({
  selector: 'app-reason-code-picker',
  standalone: true,
  imports: [TranslatePipe],
  template: `
    @if (open) {
      <div class="reason-code-picker" data-testid="reason-code-picker">
        <button type="button" class="reason-code-picker__close" data-testid="reason-code-picker-close" (click)="closed.emit()">
          <i class="pi pi-times" aria-hidden="true"></i>
        </button>
        @for (category of categories; track category) {
          @if (groupedReasonCodes(category).length > 0) {
            <div class="reason-code-picker__group">
              <div class="reason-code-picker__group-title">{{ 'masterData.lossCategory.' + category | translate }}</div>
              <div class="reason-code-picker__buttons">
                @for (reasonCode of groupedReasonCodes(category); track reasonCode.id) {
                  <button type="button" class="reason-code-button" data-testid="reason-code-button" (click)="select(reasonCode)">
                    {{ reasonCode.name }}
                  </button>
                }
              </div>
            </div>
          }
        }
      </div>
    }
  `,
  styles: [
    `
      .reason-code-picker {
        position: fixed;
        inset: 0;
        z-index: 1000;
        background: var(--surface-ground, #ffffff);
        overflow-y: auto;
        padding: 2rem;
      }

      .reason-code-picker__close {
        position: absolute;
        top: 1rem;
        right: 1rem;
        min-height: 64px;
        min-width: 64px;
        border: none;
        background: transparent;
        font-size: 1.5rem;
        cursor: pointer;
      }

      .reason-code-picker__group {
        margin-bottom: 2rem;
      }

      .reason-code-picker__group-title {
        font-size: 1.25rem;
        font-weight: 700;
        margin-bottom: 1rem;
      }

      .reason-code-picker__buttons {
        display: flex;
        flex-wrap: wrap;
        gap: 0.75rem;
      }

      .reason-code-button {
        min-height: 64px;
        min-width: 64px;
        padding: 0.75rem 1.25rem;
        cursor: pointer;
      }
    `,
  ],
})
export class ReasonCodePicker {
  @Input() open = false;
  @Input() reasonCodes: ReasonCodeDto[] = [];
  @Output() reasonSelected = new EventEmitter<string>();
  @Output() closed = new EventEmitter<void>();

  readonly categories = LOSS_CATEGORIES;

  groupedReasonCodes(category: LossCategoryValue): ReasonCodeDto[] {
    return this.reasonCodes.filter((r) => r.lossCategory === category);
  }

  select(reasonCode: ReasonCodeDto): void {
    this.reasonSelected.emit(reasonCode.id);
  }
}
