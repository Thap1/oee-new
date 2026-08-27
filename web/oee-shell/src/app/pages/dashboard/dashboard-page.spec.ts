import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslateLoader, provideTranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fakeJwt } from '../../../testing/fake-jwt';
import { HttpTranslateLoader } from '../../core/i18n/http-translate-loader';
import { ClockTickService } from '../../core/realtime/clock-tick.service';
import { MachineStatusHubService } from '../../core/realtime/machine-status-hub.service';
import { DashboardPage } from './dashboard-page';

const I18N_VI = {
  nav: { dashboard: 'Bảng điều khiển' },
  dashboard: {
    status: { Running: 'Đang chạy', Stopped: 'Dừng', Idle: 'Chờ', Fault: 'Lỗi', noSignal: 'Mất tín hiệu {{minutes}}p' },
    emptyState: { title: 'Chưa có máy nào', message: 'Liên hệ Admin' },
    machineTable: { awaitingSignal: 'Chờ tín hiệu', filter: { all: 'Tất cả' } },
  },
};

const BASE_TIME = '2026-07-21T08:00:00Z';
const BASE_TIME_MS = Date.parse(BASE_TIME);

function flushMicrotasks(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

describe('DashboardPage', () => {
  let httpMock: HttpTestingController;
  let hub: MachineStatusHubService;
  let clockTick: ClockTickService;

  beforeEach(() => {
    // No token by default — `canReadReports()` is false, so the OEE panels (Manager/Viewer/Admin only,
    // Story 4.1 AC #3) never fire their requests and these tests stay focused on the live machine list.
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'vi', fallbackLang: 'vi' }),
        provideTranslateLoader(HttpTranslateLoader),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    hub = TestBed.inject(MachineStatusHubService);
    clockTick = TestBed.inject(ClockTickService);
    // Pin the shared clock so "now - lastReportedAt" is deterministic instead of depending on the
    // real wall clock at whatever moment the test happens to run (Story 2.3's no-signal override).
    clockTick.nowMs.set(BASE_TIME_MS);
    // Never open a real WebSocket in a unit test — connect()/disconnect() become no-ops, and
    // `lastEvent` (a plain writable signal) is how tests simulate an incoming broadcast.
    vi.spyOn(hub, 'connect').mockImplementation(() => {});
    vi.spyOn(hub, 'disconnect').mockImplementation(() => {});
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  async function createDashboard(machines: unknown[], noSignalThresholdSeconds = 60) {
    const fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();
    httpMock.expectOne('/i18n/vi.json').flush(I18N_VI);
    httpMock.expectOne('/api/app-mode').flush({ mode: 'Site' });

    await flushMicrotasks();
    httpMock.expectOne('/api/production/machine-states').flush({ noSignalThresholdSeconds, machines });
    await flushMicrotasks();
    fixture.detectChanges();

    return fixture;
  }

  async function createCentralDashboard() {
    const fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();
    httpMock.expectOne('/i18n/vi.json').flush(I18N_VI);
    httpMock.expectOne('/api/app-mode').flush({ mode: 'Central' });

    await flushMicrotasks();
    fixture.detectChanges();
    // The Central branch's <app-sync-status-panel> (Story 5.3) fetches its own data on init.
    httpMock.expectOne('/api/sync/status').flush([]);
    await flushMicrotasks();
    fixture.detectChanges();

    return fixture;
  }

  it('renders an awaiting-signal row for a machine that has never reported', async () => {
    const fixture = await createDashboard([
      { machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', status: null, counter: null, lastReportedAt: null },
    ]);

    expect(fixture.nativeElement.querySelector('.machine-table__badge--unknown')).toBeTruthy();
  });

  it('renders the loaded status for a machine that already has a reading', async () => {
    const fixture = await createDashboard([
      { machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', status: 'Running', counter: 5, lastReportedAt: BASE_TIME },
    ]);

    expect(fixture.nativeElement.querySelector('.machine-table__badge--running')).toBeTruthy();
  });

  it('a MachineStatusChanged event updates the matching row and triggers the pulse', async () => {
    const fixture = await createDashboard([
      { machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', status: 'Running', counter: 5, lastReportedAt: BASE_TIME },
    ]);

    hub.lastEvent.set({ machineId: 'm1', status: 'Stopped', counter: 6, lastReportedAt: '2026-07-21T08:00:05Z' });
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.machine-table__badge--stopped')).toBeTruthy();
    expect(el.querySelector('.machine-table__row--pulse')).toBeTruthy();
  });

  it('ignores an event for a machineId not in the current scoped list', async () => {
    const fixture = await createDashboard([
      { machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', status: 'Running', counter: 5, lastReportedAt: BASE_TIME },
    ]);

    hub.lastEvent.set({ machineId: 'out-of-scope-machine', status: 'Stopped', counter: 1, lastReportedAt: '2026-07-21T08:00:05Z' });
    fixture.detectChanges();

    expect(fixture.componentInstance.machines()).toHaveLength(1);
    expect(fixture.componentInstance.machines()[0].status).toBe('Running');
  });

  it('renders the empty state (not an empty table) once loaded with zero machines', async () => {
    const fixture = await createDashboard([]);

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="dashboard-empty-state"]')).toBeTruthy();
    expect(el.querySelector('app-machine-status-table')).toBeNull();
    expect(el.textContent).toContain('Chưa có máy nào');
  });

  it('disconnects the hub on destroy', async () => {
    const fixture = await createDashboard([]);

    fixture.destroy();

    expect(hub.disconnect).toHaveBeenCalled();
  });

  it('a row flips to no-signal once the clock advances past the threshold, with no new SignalR event (AC #3 setup)', async () => {
    const fixture = await createDashboard(
      [{ machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', status: 'Running', counter: 5, lastReportedAt: BASE_TIME }],
      30,
    );
    expect(fixture.nativeElement.querySelector('.machine-table__badge--running')).toBeTruthy();

    clockTick.nowMs.set(BASE_TIME_MS + 31_000);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.machine-table__badge--nosignal')).toBeTruthy();
  });

  it('a row returns to its real status once a new reading arrives, even after going no-signal (AC #3)', async () => {
    const fixture = await createDashboard(
      [{ machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', status: 'Running', counter: 5, lastReportedAt: BASE_TIME }],
      30,
    );
    clockTick.nowMs.set(BASE_TIME_MS + 31_000);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.machine-table__badge--nosignal')).toBeTruthy();

    hub.lastEvent.set({ machineId: 'm1', status: 'Running', counter: 6, lastReportedAt: new Date(BASE_TIME_MS + 31_000).toISOString() });
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.machine-table__badge--running')).toBeTruthy();
    expect(el.querySelector('.machine-table__badge--nosignal')).toBeNull();
  });

  it("tapping a Stopped row opens the picker with that machine's active-only reason codes", async () => {
    const fixture = await createDashboard([
      { machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', siteId: 's1', status: 'Stopped', counter: 5, lastReportedAt: BASE_TIME },
    ]);

    (fixture.nativeElement.querySelector('[data-testid="machine-status-row"]') as HTMLElement).click();
    httpMock.expectOne('/api/master-data/sites/s1/reason-codes').flush([
      { id: 'r1', siteId: 's1', name: 'Kẹt khuôn', lossCategory: 'AvailabilityLoss', isActive: true },
      { id: 'r2', siteId: 's1', name: 'Ngưng dùng', lossCategory: 'AvailabilityLoss', isActive: false },
    ]);
    await flushMicrotasks();
    fixture.detectChanges();

    expect(fixture.componentInstance.pickerOpen()).toBe(true);
    expect(fixture.componentInstance.pickerReasonCodes()).toHaveLength(1);
    expect(fixture.componentInstance.pickerReasonCodes()[0].id).toBe('r1');
  });

  it('selecting a reason calls the service and closes the picker', async () => {
    const fixture = await createDashboard([
      { machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', siteId: 's1', status: 'Stopped', counter: 5, lastReportedAt: BASE_TIME },
    ]);
    (fixture.nativeElement.querySelector('[data-testid="machine-status-row"]') as HTMLElement).click();
    httpMock.expectOne('/api/master-data/sites/s1/reason-codes').flush([
      { id: 'r1', siteId: 's1', name: 'Kẹt khuôn', lossCategory: 'AvailabilityLoss', isActive: true },
    ]);
    await flushMicrotasks();
    fixture.detectChanges();

    const selectPromise = fixture.componentInstance.onReasonSelected('r1');
    httpMock.expectOne('/api/production/machines/m1/downtime-reason').flush(null);
    await selectPromise;

    expect(fixture.componentInstance.pickerOpen()).toBe(false);
  });

  it('at Central mode, does not call listMachineStates()/hub.connect() and does not render the machine table', async () => {
    const fixture = await createCentralDashboard();

    httpMock.expectNone('/api/production/machine-states');
    expect(hub.connect).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('app-machine-status-table')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="dashboard-empty-state"]')).toBeNull();
  });

  it('at Central mode, still renders the Loss Pie Chart', async () => {
    const fixture = await createCentralDashboard();

    expect(fixture.nativeElement.querySelector('app-loss-pie-chart')).toBeTruthy();
  });

  it('at Central mode, renders the sync status panel', async () => {
    const fixture = await createCentralDashboard();

    expect(fixture.nativeElement.querySelector('app-sync-status-panel')).toBeTruthy();
  });

  it('at Site mode, does not render the sync status panel', async () => {
    const fixture = await createDashboard([]);

    expect(fixture.nativeElement.querySelector('app-sync-status-panel')).toBeNull();
  });

});

/**
 * Separate suite because `AuthService` reads the stored token once, at construction — the token has to
 * be in place before anything injects it, which the suite above already does in its own `beforeEach`.
 */
describe('DashboardPage reports access', () => {
  let httpMock: HttpTestingController;
  let hub: MachineStatusHubService;

  function setUp(role: string) {
    localStorage.setItem('oee_access_token', fakeJwt({ sub: '1', role }));
    TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'vi', fallbackLang: 'vi' }),
        provideTranslateLoader(HttpTranslateLoader),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    hub = TestBed.inject(MachineStatusHubService);
    TestBed.inject(ClockTickService).nowMs.set(BASE_TIME_MS);
    vi.spyOn(hub, 'connect').mockImplementation(() => {});
    vi.spyOn(hub, 'disconnect').mockImplementation(() => {});
  }

  beforeEach(() => localStorage.clear());

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('an Operator gets no OEE panels and fires no /api/reports request', async () => {
    setUp('Operator');

    const fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();
    httpMock.expectOne('/i18n/vi.json').flush(I18N_VI);
    httpMock.expectOne('/api/app-mode').flush({ mode: 'Site' });
    await flushMicrotasks();
    httpMock.expectOne('/api/production/machine-states').flush({ noSignalThresholdSeconds: 60, machines: [] });
    await flushMicrotasks();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="dashboard-stats"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('app-oee-trend-chart')).toBeNull();
    httpMock.expectNone((request) => request.url.startsWith('/api/reports/'));
  });

  it('a Manager gets the KPI row and trend chart, with the day-over-day delta from the trend', async () => {
    setUp('Manager');

    const fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();
    httpMock.expectOne('/i18n/vi.json').flush(I18N_VI);
    httpMock.expectOne('/api/app-mode').flush({ mode: 'Site' });
    await flushMicrotasks();

    httpMock.expectOne((r) => r.url.startsWith('/api/reports/oee?')).flush({
      periodType: 'Day',
      periodStart: BASE_TIME,
      periodEnd: BASE_TIME,
      availabilityPercent: 0.9,
      performancePercent: 0.9,
      qualityPercent: 0.9,
      oeePercent: 0.729,
      availabilityLossSeconds: 0,
      performanceLossSeconds: 0,
      qualityLossSeconds: 0,
      unattributedSeconds: 0,
      qualityRejectQuantity: 4,
      topDowntimeReasonCodeId: null,
      topDowntimeReasonName: null,
      topDowntimeReasonSeconds: null,
    });
    httpMock.expectOne((r) => r.url.startsWith('/api/reports/oee/trend?')).flush([
      { date: '2026-07-20', availabilityPercent: 0.8, performancePercent: 0.8, qualityPercent: 0.8, oeePercent: 0.512 },
      { date: '2026-07-21', availabilityPercent: 0.9, performancePercent: 0.9, qualityPercent: 0.9, oeePercent: 0.729 },
    ]);
    httpMock.expectOne('/api/production/machine-states').flush({ noSignalThresholdSeconds: 60, machines: [] });
    await flushMicrotasks();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="dashboard-stats"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('app-oee-trend-chart')).toBeTruthy();
    expect(fixture.componentInstance.percent(fixture.componentInstance.report()?.oeePercent)).toBe('72.9');
    expect(fixture.componentInstance.delta('oeePercent')).toBe('+21.7%');
    expect(fixture.componentInstance.trend('oeePercent')).toBe('up');
  });
});
