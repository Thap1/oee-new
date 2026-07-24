import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface SiteSyncStatusDto {
  siteId: string;
  siteName: string;
  lastSyncedAt: string | null;
  isStale: boolean;
}

/** Sync status badge data source (Story 5.3, UX-DR11) — Central-only in practice, but the endpoint itself has no AppMode gate (see `SyncStatusController`'s doc comment). */
@Injectable({ providedIn: 'root' })
export class SyncStatusService {
  constructor(private readonly http: HttpClient) {}

  getStatuses(): Promise<SiteSyncStatusDto[]> {
    return firstValueFrom(this.http.get<SiteSyncStatusDto[]>('/api/sync/status'));
  }
}
