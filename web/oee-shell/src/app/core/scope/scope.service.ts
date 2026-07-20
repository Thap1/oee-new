import { HttpClient } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { LineDto, SiteDto } from '../../pages/master-data/master-data.service';

/**
 * Global Site/Line scope selection (Story 1.6, AC #1-#3, FR-015) — shared by every screen, not just
 * Master Data (Epic 2/4's dashboard/reports reuse this same service). `sites`/`lines` come from the
 * already scope-filtered master-data API (see Story 1.6 AC #4 server-side enforcement) — this
 * service never decodes the JWT itself to build the list, so it can never show an out-of-scope Site.
 */
@Injectable({ providedIn: 'root' })
export class ScopeService {
  private readonly sitesSignal = signal<SiteDto[]>([]);
  private readonly linesSignal = signal<LineDto[]>([]);

  readonly sites = this.sitesSignal.asReadonly();
  readonly lines = this.linesSignal.asReadonly();
  readonly selectedSiteId = signal<string | null>(null);
  readonly selectedLineId = signal<string | null>(null);

  /** Progressive disclosure (UX-DR3): hidden entirely when the caller only has one Site. */
  readonly showSelector = computed(() => this.sitesSignal().length > 1);

  constructor(private readonly http: HttpClient) {}

  async loadSites(): Promise<void> {
    const sites = await firstValueFrom(this.http.get<SiteDto[]>('/api/master-data/sites'));
    this.sitesSignal.set(sites);

    if (sites.length === 0) {
      this.selectedSiteId.set(null);
      this.linesSignal.set([]);
      return;
    }

    if (!sites.some((s) => s.id === this.selectedSiteId())) {
      await this.selectSite(sites[0].id);
    }
  }

  async selectSite(siteId: string): Promise<void> {
    this.selectedSiteId.set(siteId);
    this.selectedLineId.set(null);
    this.linesSignal.set(await firstValueFrom(this.http.get<LineDto[]>(`/api/master-data/sites/${siteId}/lines`)));
  }

  selectLine(lineId: string | null): void {
    this.selectedLineId.set(lineId);
  }
}
