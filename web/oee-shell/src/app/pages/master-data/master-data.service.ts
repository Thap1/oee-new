import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface SiteDto {
  id: string;
  name: string;
  /** Story 5.2: only populated at a Central instance, and only for a Site with a configured `Central:SiteLinks` entry — "Mở tại site X" (UX-DR5). */
  openAtUrl: string | null;
}

export interface LineDto {
  id: string;
  name: string;
  siteId: string;
}

export interface MachineDto {
  id: string;
  name: string;
  lineId: string;
}

export interface ShiftScheduleDto {
  id: string;
  siteId: string;
  lineId: string | null;
  name: string;
  startTime: string;
  endTime: string;
}

export type LossCategoryValue = 'AvailabilityLoss' | 'PerformanceLoss' | 'QualityLoss';

export interface ReasonCodeDto {
  id: string;
  siteId: string;
  name: string;
  lossCategory: LossCategoryValue;
  isActive: boolean;
}

export type UserRoleValue = 'Admin' | 'Manager' | 'Operator' | 'Viewer';

export interface UserDto {
  id: string;
  username: string;
  role: UserRoleValue;
  siteIds: string[];
  lineIds: string[];
  isActive: boolean;
}

/** Site &gt; Line &gt; Machine CRUD (Story 1.2, FR-011). Reads: any role. Writes: Admin only (enforced server-side). */
@Injectable({ providedIn: 'root' })
export class MasterDataService {
  constructor(private readonly http: HttpClient) {}

  listSites(): Promise<SiteDto[]> {
    return firstValueFrom(this.http.get<SiteDto[]>('/api/master-data/sites'));
  }

  createSite(name: string): Promise<SiteDto> {
    return firstValueFrom(this.http.post<SiteDto>('/api/master-data/sites', { name }));
  }

  renameSite(id: string, name: string): Promise<SiteDto> {
    return firstValueFrom(this.http.put<SiteDto>(`/api/master-data/sites/${id}`, { name }));
  }

  deleteSite(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/master-data/sites/${id}`));
  }

  listLines(siteId: string): Promise<LineDto[]> {
    return firstValueFrom(this.http.get<LineDto[]>(`/api/master-data/sites/${siteId}/lines`));
  }

  createLine(siteId: string, name: string): Promise<LineDto> {
    return firstValueFrom(this.http.post<LineDto>(`/api/master-data/sites/${siteId}/lines`, { name }));
  }

  renameLine(id: string, name: string): Promise<LineDto> {
    return firstValueFrom(this.http.put<LineDto>(`/api/master-data/lines/${id}`, { name }));
  }

  deleteLine(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/master-data/lines/${id}`));
  }

  listMachines(lineId: string): Promise<MachineDto[]> {
    return firstValueFrom(this.http.get<MachineDto[]>(`/api/master-data/lines/${lineId}/machines`));
  }

  createMachine(lineId: string, name: string): Promise<MachineDto> {
    return firstValueFrom(this.http.post<MachineDto>(`/api/master-data/lines/${lineId}/machines`, { name }));
  }

  renameMachine(id: string, name: string): Promise<MachineDto> {
    return firstValueFrom(this.http.put<MachineDto>(`/api/master-data/machines/${id}`, { name }));
  }

  deleteMachine(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/master-data/machines/${id}`));
  }

  listShiftSchedules(siteId: string): Promise<ShiftScheduleDto[]> {
    return firstValueFrom(this.http.get<ShiftScheduleDto[]>(`/api/master-data/sites/${siteId}/shift-schedules`));
  }

  createShiftSchedule(siteId: string, name: string, lineId: string | null, startTime: string, endTime: string): Promise<ShiftScheduleDto> {
    return firstValueFrom(
      this.http.post<ShiftScheduleDto>(`/api/master-data/sites/${siteId}/shift-schedules`, { name, lineId, startTime, endTime }),
    );
  }

  rescheduleShiftSchedule(id: string, name: string, startTime: string, endTime: string): Promise<ShiftScheduleDto> {
    return firstValueFrom(this.http.put<ShiftScheduleDto>(`/api/master-data/shift-schedules/${id}`, { name, startTime, endTime }));
  }

  deleteShiftSchedule(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/master-data/shift-schedules/${id}`));
  }

  listReasonCodes(siteId: string): Promise<ReasonCodeDto[]> {
    return firstValueFrom(this.http.get<ReasonCodeDto[]>(`/api/master-data/sites/${siteId}/reason-codes`));
  }

  createReasonCode(siteId: string, name: string, lossCategory: LossCategoryValue): Promise<ReasonCodeDto> {
    return firstValueFrom(
      this.http.post<ReasonCodeDto>(`/api/master-data/sites/${siteId}/reason-codes`, { name, lossCategory }),
    );
  }

  deactivateReasonCode(id: string): Promise<ReasonCodeDto> {
    return firstValueFrom(this.http.put<ReasonCodeDto>(`/api/master-data/reason-codes/${id}/deactivate`, null));
  }

  deleteReasonCode(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/master-data/reason-codes/${id}`));
  }

  listUsers(): Promise<UserDto[]> {
    return firstValueFrom(this.http.get<UserDto[]>('/api/users'));
  }

  createUser(username: string, password: string, role: UserRoleValue, siteIds: string[], lineIds: string[]): Promise<UserDto> {
    return firstValueFrom(this.http.post<UserDto>('/api/users', { username, password, role, siteIds, lineIds }));
  }

  deactivateUser(id: string): Promise<UserDto> {
    return firstValueFrom(this.http.put<UserDto>(`/api/users/${id}/deactivate`, null));
  }
}
