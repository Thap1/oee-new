import { HttpRequest, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslateLoader, provideTranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { HttpTranslateLoader } from '../../core/i18n/http-translate-loader';
import { ScopeService } from '../../core/scope/scope.service';
import { ReportsPage } from './reports-page';

const I18N_VI = {
  reports: {
    title: 'Báo cáo OEE',
    periodType: { shift: 'Ca', day: 'Ngày', week: 'Tuần' },
    selectShift: 'Chọn một ca...',
    availability: 'Availability',
    performance: 'Performance',
    quality: 'Quality',
    oee: 'OEE',
    qualityReject: 'Phế phẩm',
    filter: { label: 'Bộ lọc', none: 'Không lọc', site: 'Site', line: 'Line', machine: 'Máy', selectTarget: 'Chọn...' },
    topDowntimeReason: { title: 'Nguyên nhân dừng máy nhiều nhất', empty: 'Không có dữ liệu dừng máy', seconds: '{{seconds}}s' },
  },
  masterData: {
    error: { generic: 'Đã xảy ra lỗi. Vui lòng thử lại.' },
  },
};

function report(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    periodType: 'Day',
    periodStart: '2026-07-20T00:00:00Z',
    periodEnd: '2026-07-21T00:00:00Z',
    availabilityPercent: 0.9,
    performancePercent: 0.8,
    qualityPercent: 0.95,
    oeePercent: 0.684,
    availabilityLossSeconds: 100,
    performanceLossSeconds: 50,
    qualityLossSeconds: 20,
    unattributedSeconds: 0,
    qualityRejectQuantity: 3,
    topDowntimeReasonCodeId: null,
    topDowntimeReasonName: null,
    topDowntimeReasonSeconds: null,
    ...overrides,
  };
}

/** The service builds its query string directly into the URL passed to `http.get`, so `HttpRequest.params` stays empty — match against the raw query string instead (same shape as `loss-analytics.service.ts`'s callers). */
function oeeReportRequest(r: HttpRequest<unknown>, expected: Record<string, string> = {}): boolean {
  if (!r.url.startsWith('/api/reports/oee')) {
    return false;
  }
  const query = new URLSearchParams(r.url.split('?')[1] ?? '');
  return Object.entries(expected).every(([key, value]) => query.get(key) === value);
}

/** `ngOnInit`'s fire-and-forget `refetchReport()` isn't awaitable from the test — flushing a macrotask lets its microtask chain (firstValueFrom + async/await) settle before assertions run (same pattern as `scope.service.spec.ts`). */
function flushMicrotasks(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

describe('ReportsPage', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ReportsPage],
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

  function create() {
    const fixture = TestBed.createComponent(ReportsPage);
    fixture.detectChanges();
    httpMock.expectOne('/i18n/vi.json').flush(I18N_VI);
    fixture.detectChanges();
    return fixture;
  }

  it('fetches a Day report for today on load by default', async () => {
    const fixture = create();

    const req = httpMock.expectOne((r) => oeeReportRequest(r, { periodType: 'Day' }));
    req.flush(report());
    await flushMicrotasks();
    fixture.detectChanges();

    expect(fixture.componentInstance.report()).not.toBeNull();
  });

  it('switching periodType to Week re-fetches with the new period', async () => {
    const fixture = create();
    httpMock.expectOne((r) => oeeReportRequest(r)).flush(report());

    const switchPromise = fixture.componentInstance.onPeriodTypeChange('Week');
    httpMock.expectOne((r) => oeeReportRequest(r, { periodType: 'Week' })).flush(report({ periodType: 'Week' }));
    await switchPromise;

    expect(fixture.componentInstance.report()!.periodType).toBe('Week');
  });

  it('selecting a reference date re-fetches the report', async () => {
    const fixture = create();
    httpMock.expectOne((r) => oeeReportRequest(r)).flush(report());

    const datePromise = fixture.componentInstance.onReferenceDateChange(new Date(2026, 6, 21));
    httpMock.expectOne((r) => oeeReportRequest(r, { referenceDate: '2026-07-21' })).flush(report());
    await datePromise;

    expect(fixture.componentInstance.referenceDate().getDate()).toBe(21);
  });

  it('switching to Shift does not fetch a report until a shift is picked', async () => {
    const fixture = create();
    httpMock.expectOne((r) => oeeReportRequest(r)).flush(report());

    const scope = TestBed.inject(ScopeService);
    scope.selectedSiteId.set('site-1');
    fixture.detectChanges();

    const switchPromise = fixture.componentInstance.onPeriodTypeChange('Shift');
    httpMock.expectOne('/api/master-data/sites/site-1/shift-schedules').flush([
      { id: 'shift-1', siteId: 'site-1', lineId: null, name: 'Day Shift', startTime: '08:00:00', endTime: '16:00:00' },
    ]);
    await switchPromise;

    // No /api/reports/oee request pending — httpMock.verify() in afterEach proves it.
    expect(fixture.componentInstance.shiftOptions()).toEqual([{ label: 'Day Shift', value: 'shift-1' }]);
  });

  it('picking a shift fetches the report with the shiftScheduleId param', async () => {
    const fixture = create();
    httpMock.expectOne((r) => oeeReportRequest(r)).flush(report());

    const scope = TestBed.inject(ScopeService);
    scope.selectedSiteId.set('site-1');
    fixture.detectChanges();

    const switchPromise = fixture.componentInstance.onPeriodTypeChange('Shift');
    httpMock.expectOne('/api/master-data/sites/site-1/shift-schedules').flush([
      { id: 'shift-1', siteId: 'site-1', lineId: null, name: 'Day Shift', startTime: '08:00:00', endTime: '16:00:00' },
    ]);
    await switchPromise;

    const shiftPromise = fixture.componentInstance.onShiftChange('shift-1');
    httpMock
      .expectOne((r) => oeeReportRequest(r, { shiftScheduleId: 'shift-1', periodType: 'Shift' }))
      .flush(report({ periodType: 'Shift' }));
    await shiftPromise;

    expect(fixture.componentInstance.report()!.periodType).toBe('Shift');
  });

  it('a failed fetch shows an error state', async () => {
    const fixture = create();

    httpMock.expectOne((r) => oeeReportRequest(r)).flush('boom', { status: 500, statusText: 'Server Error' });
    await flushMicrotasks();
    fixture.detectChanges();

    expect(fixture.componentInstance.error()).toBe(true);
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="reports-error"]')).toBeTruthy();
  });

  it('renders the four percentages and the supplementary quality reject quantity', async () => {
    const fixture = create();

    httpMock.expectOne((r) => oeeReportRequest(r)).flush(report());
    await flushMicrotasks();
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="reports-stat-availability"]')?.textContent).toContain('90.0');
    expect(el.querySelector('[data-testid="reports-stat-performance"]')?.textContent).toContain('80.0');
    expect(el.querySelector('[data-testid="reports-stat-quality"]')?.textContent).toContain('95.0');
    expect(el.querySelector('[data-testid="reports-stat-oee"]')?.textContent).toContain('68.4');
    expect(el.querySelector('[data-testid="reports-quality-reject"]')?.textContent).toContain('3');
  });

  it('picking a Site filter re-fetches with filterType/filterId, using the already-scoped sites() list', async () => {
    const fixture = create();
    httpMock.expectOne((r) => oeeReportRequest(r)).flush(report());

    const scope = TestBed.inject(ScopeService);
    const loadPromise = scope.loadSites();
    httpMock.expectOne('/api/master-data/sites').flush([{ id: 'site-1', name: 'Site A' }]);
    await flushMicrotasks();
    httpMock.expectOne('/api/master-data/sites/site-1/lines').flush([]);
    await loadPromise;
    fixture.detectChanges();

    const typePromise = fixture.componentInstance.onFilterTypeChange('Site');
    await typePromise;
    expect(fixture.componentInstance.filterTargetOptions()).toEqual([{ label: 'Site A', value: 'site-1' }]);
    // Picking the filter *level* alone (no target yet) must not fetch — httpMock.verify() proves it.

    const idPromise = fixture.componentInstance.onFilterIdChange('site-1');
    httpMock.expectOne((r) => oeeReportRequest(r, { filterType: 'Site', filterId: 'site-1' })).flush(report());
    await idPromise;
  });

  it('picking a Machine filter fetches that Line\'s machines, then re-fetches the report once a machine is picked', async () => {
    const fixture = create();
    httpMock.expectOne((r) => oeeReportRequest(r)).flush(report());

    const scope = TestBed.inject(ScopeService);
    scope.selectLine('line-1');
    fixture.detectChanges();

    const typePromise = fixture.componentInstance.onFilterTypeChange('Machine');
    httpMock.expectOne('/api/master-data/lines/line-1/machines').flush([{ id: 'm1', name: 'Machine 1', lineId: 'line-1' }]);
    await typePromise;
    expect(fixture.componentInstance.filterTargetOptions()).toEqual([{ label: 'Machine 1', value: 'm1' }]);

    const idPromise = fixture.componentInstance.onFilterIdChange('m1');
    httpMock.expectOne((r) => oeeReportRequest(r, { filterType: 'Machine', filterId: 'm1' })).flush(report());
    await idPromise;
  });

  it('clearing the filter (back to None) re-fetches without filterType/filterId', async () => {
    const fixture = create();
    httpMock.expectOne((r) => oeeReportRequest(r)).flush(report());

    const scope = TestBed.inject(ScopeService);
    scope.selectLine('line-1');
    fixture.detectChanges();
    const typePromise = fixture.componentInstance.onFilterTypeChange('Machine');
    httpMock.expectOne('/api/master-data/lines/line-1/machines').flush([{ id: 'm1', name: 'Machine 1', lineId: 'line-1' }]);
    await typePromise;
    const idPromise = fixture.componentInstance.onFilterIdChange('m1');
    httpMock.expectOne((r) => oeeReportRequest(r, { filterType: 'Machine', filterId: 'm1' })).flush(report());
    await idPromise;

    const clearPromise = fixture.componentInstance.onFilterTypeChange(null);
    httpMock
      .expectOne((r) => r.url.startsWith('/api/reports/oee') && !r.url.includes('filterType'))
      .flush(report());
    await clearPromise;
  });

  it('renders the top downtime reason name and seconds when present', async () => {
    const fixture = create();

    httpMock
      .expectOne((r) => oeeReportRequest(r))
      .flush(report({ topDowntimeReasonCodeId: 'r1', topDowntimeReasonName: 'Kẹt khuôn', topDowntimeReasonSeconds: 120 }));
    await flushMicrotasks();
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const section = el.querySelector('[data-testid="reports-top-downtime-reason"]');
    expect(section?.textContent).toContain('Kẹt khuôn');
    expect(section?.textContent).toContain('120s');
    expect(el.querySelector('[data-testid="reports-top-downtime-reason-empty"]')).toBeNull();
  });

  it('renders the empty state when there is no top downtime reason (AC #3)', async () => {
    const fixture = create();

    httpMock.expectOne((r) => oeeReportRequest(r)).flush(report());
    await flushMicrotasks();
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="reports-top-downtime-reason-empty"]')?.textContent).toContain('Không có dữ liệu dừng máy');
  });
});
