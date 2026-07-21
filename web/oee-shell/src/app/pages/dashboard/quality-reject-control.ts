import { Component, Input, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { DashboardService } from './dashboard.service';

/**
 * Records basic reject/scrap quantity for a Machine (Story 2.6, FR-010). No UX-DR governs this
 * screen specifically — kept minimal per Dev Notes: a stepper input (`p-inputnumber`'s built-in
 * increment/decrement, touch-friendlier than a bare text field) + submit, nothing else (no
 * root-cause/category — explicitly out of MVP scope).
 */
@Component({
  selector: 'app-quality-reject-control',
  standalone: true,
  imports: [FormsModule, TranslatePipe, ButtonModule, DialogModule, InputNumberModule],
  template: `
    <button
      type="button"
      class="quality-reject-trigger"
      data-testid="quality-reject-trigger"
      [attr.aria-label]="'dashboard.qualityReject.title' | translate"
      (click)="openDialog()"
    >
      <i class="pi pi-exclamation-circle" aria-hidden="true"></i>
    </button>
    <p-dialog
      [visible]="dialogOpen()"
      (visibleChange)="!$event && closeDialog()"
      [modal]="true"
      [header]="'dashboard.qualityReject.title' | translate"
      data-testid="quality-reject-dialog"
    >
      <label for="quality-reject-quantity">{{ 'dashboard.qualityReject.quantity' | translate }}</label>
      <p-inputnumber
        inputId="quality-reject-quantity"
        [showButtons]="true"
        [min]="1"
        [ngModel]="quantity()"
        (ngModelChange)="quantity.set($event)"
        data-testid="quality-reject-quantity-input"
      />
      <p-button
        [label]="'dashboard.qualityReject.submit' | translate"
        [disabled]="!quantity() || quantity()! <= 0"
        (onClick)="submit()"
        data-testid="quality-reject-submit-btn"
      />
    </p-dialog>
  `,
  styles: [
    `
      .quality-reject-trigger {
        min-height: 64px;
        min-width: 64px;
        border: none;
        background: transparent;
        cursor: pointer;
        font-size: 1.25rem;
      }
    `,
  ],
})
export class QualityRejectControl {
  private readonly dashboardService = inject(DashboardService);

  @Input({ required: true }) machineId!: string;

  readonly dialogOpen = signal(false);
  readonly quantity = signal<number | null>(null);

  openDialog(): void {
    this.quantity.set(null);
    this.dialogOpen.set(true);
  }

  closeDialog(): void {
    this.dialogOpen.set(false);
  }

  async submit(): Promise<void> {
    const value = this.quantity();
    if (!value || value <= 0) {
      return;
    }
    await this.dashboardService.recordQualityReject(this.machineId, value);
    this.closeDialog();
  }
}
