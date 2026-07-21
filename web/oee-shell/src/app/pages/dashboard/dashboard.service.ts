import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type MachineStatusValue = 'Running' | 'Stopped' | 'Idle' | 'Fault';

export interface MachineStatusDto {
  machineId: string;
  machineName: string;
  lineId: string;
  status: MachineStatusValue | null;
  counter: number | null;
  lastReportedAt: string | null;
}

/** Current status of every Machine in the caller's scope (Story 2.2, FR-004/006). */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  constructor(private readonly http: HttpClient) {}

  listMachineStates(): Promise<MachineStatusDto[]> {
    return firstValueFrom(this.http.get<MachineStatusDto[]>('/api/production/machine-states'));
  }
}
