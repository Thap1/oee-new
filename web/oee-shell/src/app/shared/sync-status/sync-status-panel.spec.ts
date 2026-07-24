import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslateLoader, provideTranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { HttpTranslateLoader } from '../../core/i18n/http-translate-loader';
import { SyncStatusPanel } from './sync-status-panel';

const I18N_VI = {
  sync: {
    badge: {
      lastSynced: 'Đồng bộ lần cuối: {{minutes}} phút trước',
      neverSynced: 'Chưa đồng bộ lần nào',
    },
  },
};

function flushMicrotasks(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

describe('SyncStatusPanel', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SyncStatusPanel],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'vi', fallbackLang: 'vi' }),
        provideTranslateLoader(HttpTranslateLoader),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  async function create(statuses: unknown[]) {
    const fixture = TestBed.createComponent(SyncStatusPanel);
    fixture.detectChanges();
    // No `| translate` pipe is ever instantiated until the @for loop has at least one item, so with
    // zero statuses at creation time, /i18n/vi.json isn't requested until after this flush + a
    // subsequent detectChanges() actually creates the per-badge views.
    httpMock.expectOne('/api/sync/status').flush(statuses);
    await flushMicrotasks();
    fixture.detectChanges();

    if (statuses.length > 0) {
      httpMock.expectOne('/i18n/vi.json').flush(I18N_VI);
      fixture.detectChanges();
    }

    return fixture;
  }

  it('renders one badge per returned site', async () => {
    const fixture = await create([
      { siteId: 's1', siteName: 'Site A', lastSyncedAt: new Date().toISOString(), isStale: false },
      { siteId: 's2', siteName: 'Site B', lastSyncedAt: new Date().toISOString(), isStale: false },
    ]);

    expect(fixture.nativeElement.querySelectorAll('[data-testid="sync-badge"]')).toHaveLength(2);
  });

  it('a stale site gets the stale class', async () => {
    const fixture = await create([{ siteId: 's1', siteName: 'Site A', lastSyncedAt: new Date().toISOString(), isStale: true }]);

    const badge = fixture.nativeElement.querySelector('[data-testid="sync-badge"]') as HTMLElement;
    expect(badge.classList.contains('sync-badge--stale')).toBe(true);
  });

  it('a fresh site does not get the stale class', async () => {
    const fixture = await create([{ siteId: 's1', siteName: 'Site A', lastSyncedAt: new Date().toISOString(), isStale: false }]);

    const badge = fixture.nativeElement.querySelector('[data-testid="sync-badge"]') as HTMLElement;
    expect(badge.classList.contains('sync-badge--stale')).toBe(false);
  });

  it('a never-synced site renders the neverSynced label, not "NaN minutes ago"', async () => {
    const fixture = await create([{ siteId: 's1', siteName: 'Site A', lastSyncedAt: null, isStale: true }]);

    const badge = fixture.nativeElement.querySelector('[data-testid="sync-badge"]') as HTMLElement;
    expect(badge.textContent).toContain('Chưa đồng bộ lần nào');
    expect(badge.textContent).not.toContain('NaN');
  });
});
