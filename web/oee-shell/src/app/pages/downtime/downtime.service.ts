import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface DowntimeHistoryEntryDto {
  id: string;
  machineId: string;
  machineName: string;
  reasonCodeId: string | null;
  reasonCodeName: string | null;
  startedAt: string;
  endedAt: string | null;
  durationSeconds: number | null;
}

/** Downtime history list for the nav "Dừng máy" page — most recent events (open or closed) across the caller's scoped machines. */
@Injectable({ providedIn: 'root' })
export class DowntimeService {
  constructor(private readonly http: HttpClient) {}

  listHistory(): Promise<DowntimeHistoryEntryDto[]> {
    return firstValueFrom(this.http.get<DowntimeHistoryEntryDto[]>('/api/production/downtime-history'));
  }
}
