import { HttpClient } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type AppMode = 'Site' | 'Central';

interface AppModeResponse {
  mode: AppMode;
}

/**
 * Which AppMode (Site | Central, Architecture Spine AD-2) this deployment is running as (Story 5.2).
 * Fetched once from the anonymous `/api/app-mode` endpoint and cached in a signal — `DashboardPage` and
 * `MasterDataPage` both call `load()` but only the first resolves an actual HTTP request, same
 * "call once, cache in a signal" shape as `AuthService`'s token/role state.
 */
@Injectable({ providedIn: 'root' })
export class AppModeService {
  private readonly modeSignal = signal<AppMode | null>(null);
  private loadPromise: Promise<void> | null = null;

  readonly mode = this.modeSignal.asReadonly();
  readonly isCentral = computed(() => this.mode() === 'Central');

  constructor(private readonly http: HttpClient) {}

  async load(): Promise<void> {
    if (this.modeSignal() !== null) {
      return;
    }

    this.loadPromise ??= this.fetchAndCache();
    await this.loadPromise;
  }

  private async fetchAndCache(): Promise<void> {
    const response = await firstValueFrom(this.http.get<AppModeResponse>('/api/app-mode'));
    this.modeSignal.set(response.mode);
  }
}
