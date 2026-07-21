import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type MachineStatusValue = 'Running' | 'Stopped' | 'Idle' | 'Fault';

export interface MachineStatusDto {
  machineId: string;
  machineName: string;
  lineId: string;
  siteId: string;
  status: MachineStatusValue | null;
  counter: number | null;
  lastReportedAt: string | null;
}

/** Story 2.3: the no-signal threshold rides along so the dashboard needs only one round trip. */
export interface MachineStatesResult {
  noSignalThresholdSeconds: number;
  machines: MachineStatusDto[];
}

/** Current status of every Machine in the caller's scope (Story 2.2, FR-004/006). */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  constructor(private readonly http: HttpClient) {}

  listMachineStates(): Promise<MachineStatesResult> {
    return firstValueFrom(this.http.get<MachineStatesResult>('/api/production/machine-states'));
  }

  recordDowntimeReason(machineId: string, reasonCodeId: string): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/production/machines/${machineId}/downtime-reason`, { reasonCodeId }));
  }

  recordQualityReject(machineId: string, quantity: number): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/production/machines/${machineId}/quality-rejects`, { quantity }));
  }
}
