import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type ReportPeriodType = 'Shift' | 'Day' | 'Week';

export interface OeeReportDto {
  periodType: ReportPeriodType;
  periodStart: string;
  periodEnd: string;
  availabilityPercent: number;
  performancePercent: number;
  qualityPercent: number;
  oeePercent: number;
  availabilityLossSeconds: number;
  performanceLossSeconds: number;
  qualityLossSeconds: number;
  unattributedSeconds: number;
  qualityRejectQuantity: number;
}

/** yyyy-MM-dd in the browser's local calendar — the backend interprets it as a UTC calendar date (same convention as `loss-analytics.service.ts`'s `toDateParam`). */
function toDateParam(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/** Aggregated OEE report by Shift/Day/Week (Story 4.1, FR-016/017/018). */
@Injectable({ providedIn: 'root' })
export class OeeReportService {
  constructor(private readonly http: HttpClient) {}

  getReport(periodType: ReportPeriodType, referenceDate: Date, shiftScheduleId?: string | null): Promise<OeeReportDto> {
    const params = new URLSearchParams({ periodType, referenceDate: toDateParam(referenceDate) });
    if (shiftScheduleId) {
      params.set('shiftScheduleId', shiftScheduleId);
    }
    return firstValueFrom(this.http.get<OeeReportDto>(`/api/reports/oee?${params.toString()}`));
  }
}
