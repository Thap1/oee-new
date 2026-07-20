import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, afterEach, describe, it, expect } from 'vitest';
import { ScopeService } from './scope.service';

function flushMicrotasks(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

describe('ScopeService', () => {
  let service: ScopeService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ScopeService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('hides the selector when the caller only has one Site', async () => {
    const loadPromise = service.loadSites();
    httpMock.expectOne('/api/master-data/sites').flush([{ id: 'site-1', name: 'Site A' }]);
    await flushMicrotasks();
    httpMock.expectOne('/api/master-data/sites/site-1/lines').flush([]);
    await loadPromise;

    expect(service.showSelector()).toBe(false);
    expect(service.selectedSiteId()).toBe('site-1');
  });

  it('shows the selector and exposes only the Sites returned by the (already scope-filtered) API', async () => {
    const loadPromise = service.loadSites();
    httpMock
      .expectOne('/api/master-data/sites')
      .flush([
        { id: 'site-1', name: 'Site A' },
        { id: 'site-2', name: 'Site B' },
      ]);
    await flushMicrotasks();
    httpMock.expectOne('/api/master-data/sites/site-1/lines').flush([]);
    await loadPromise;

    expect(service.showSelector()).toBe(true);
    expect(service.sites()).toHaveLength(2);
  });

  it('selecting a different Site loads its Lines and resets the selected Line', async () => {
    const loadPromise = service.loadSites();
    httpMock
      .expectOne('/api/master-data/sites')
      .flush([
        { id: 'site-1', name: 'Site A' },
        { id: 'site-2', name: 'Site B' },
      ]);
    await flushMicrotasks();
    httpMock.expectOne('/api/master-data/sites/site-1/lines').flush([]);
    await loadPromise;

    service.selectLine('line-x');
    const selectPromise = service.selectSite('site-2');
    httpMock.expectOne('/api/master-data/sites/site-2/lines').flush([{ id: 'line-1', name: 'Line A', siteId: 'site-2' }]);
    await selectPromise;

    expect(service.selectedSiteId()).toBe('site-2');
    expect(service.selectedLineId()).toBeNull();
    expect(service.lines()).toEqual([{ id: 'line-1', name: 'Line A', siteId: 'site-2' }]);
  });

  it('an empty scoped Site list clears selection instead of erroring', async () => {
    const loadPromise = service.loadSites();
    httpMock.expectOne('/api/master-data/sites').flush([]);
    await loadPromise;

    expect(service.showSelector()).toBe(false);
    expect(service.selectedSiteId()).toBeNull();
  });
});
