import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { LossCategoryValue } from '../master-data/master-data.service';

export type LossBreakdownTargetType = 'Equipment' | 'Area';

export interface LossAreaDto {
  lineId: string;
  lineName: string;
  siteId: string;
}

export interface LossBreakdownDto {
  targetId: string;
  targetType: LossBreakdownTargetType;
  availabilitySeconds: number;
  performanceSeconds: number;
  qualitySeconds: number;
  unattributedSeconds: number;
  qualityRejectQuantity: number;
}

export interface ReasonBreakdownDto {
  reasonCodeId: string;
  reasonCodeName: string;
  durationSeconds: number;
}

/** yyyy-MM-dd in the browser's local calendar — good enough for a date-picker's single-day selection; the backend interprets it as a UTC calendar day (Story 3.1/3.2 Dev Notes). */
function toDateParam(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/** Loss pie chart data for the Dashboard (Story 3.1/3.2, FR-019/020/021). */
@Injectable({ providedIn: 'root' })
export class LossAnalyticsService {
  constructor(private readonly http: HttpClient) {}

  listAreas(): Promise<LossAreaDto[]> {
    return firstValueFrom(this.http.get<LossAreaDto[]>('/api/analytics/loss-areas'));
  }

  getBreakdown(targetType: LossBreakdownTargetType, targetId: string, date: Date | null = null): Promise<LossBreakdownDto> {
    const params = new URLSearchParams({ targetType, targetId });
    if (date) {
      params.set('date', toDateParam(date));
    }
    return firstValueFrom(this.http.get<LossBreakdownDto>(`/api/analytics/loss-breakdown?${params.toString()}`));
  }

  getReasonBreakdown(
    targetType: LossBreakdownTargetType,
    targetId: string,
    lossCategory: LossCategoryValue,
    date: Date | null = null,
  ): Promise<ReasonBreakdownDto[]> {
    const params = new URLSearchParams({ targetType, targetId, lossCategory });
    if (date) {
      params.set('date', toDateParam(date));
    }
    return firstValueFrom(this.http.get<ReasonBreakdownDto[]>(`/api/analytics/loss-breakdown/reasons?${params.toString()}`));
  }
}
