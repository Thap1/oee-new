import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslateLoader, provideTranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { HttpTranslateLoader } from '../../core/i18n/http-translate-loader';
import { ClockTickService } from '../../core/realtime/clock-tick.service';
import { MachineStatusHubService } from '../../core/realtime/machine-status-hub.service';
import { DashboardPage } from './dashboard-page';

const I18N_VI = {
  nav: { dashboard: 'Bảng điều khiển' },
  dashboard: {
    status: { Running: 'Đang chạy', Stopped: 'Dừng', Idle: 'Chờ', Fault: 'Lỗi', noSignal: 'Mất tín hiệu {{minutes}}p' },
    emptyState: { title: 'Chưa có máy nào', message: 'Liên hệ Admin' },
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

  afterEach(() => httpMock.verify());

  async function createDashboard(machines: unknown[], noSignalThresholdSeconds = 60) {
    const fixture = TestBed.createComponent(DashboardPage);
    httpMock.expectOne('/i18n/vi.json').flush(I18N_VI);
    fixture.detectChanges();

    await flushMicrotasks();
    httpMock.expectOne('/api/production/machine-states').flush({ noSignalThresholdSeconds, machines });
    await flushMicrotasks();
    fixture.detectChanges();

    return fixture;
  }

  it('renders a skeleton card for a machine that has never reported', async () => {
    const fixture = await createDashboard([
      { machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', status: null, counter: null, lastReportedAt: null },
    ]);

    expect(fixture.nativeElement.querySelector('.machine-status-card--skeleton')).toBeTruthy();
  });

  it('renders the loaded status for a machine that already has a reading', async () => {
    const fixture = await createDashboard([
      { machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', status: 'Running', counter: 5, lastReportedAt: BASE_TIME },
    ]);

    expect(fixture.nativeElement.querySelector('.machine-status-card--running')).toBeTruthy();
  });

  it('a MachineStatusChanged event updates the matching card and triggers the pulse', async () => {
    const fixture = await createDashboard([
      { machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', status: 'Running', counter: 5, lastReportedAt: BASE_TIME },
    ]);

    hub.lastEvent.set({ machineId: 'm1', status: 'Stopped', counter: 6, lastReportedAt: '2026-07-21T08:00:05Z' });
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.machine-status-card--stopped')).toBeTruthy();
    expect(el.querySelector('.machine-status-card--pulse')).toBeTruthy();
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

  it('renders the empty state (not an empty grid, not a stuck skeleton) once loaded with zero machines', async () => {
    const fixture = await createDashboard([]);

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="dashboard-empty-state"]')).toBeTruthy();
    expect(el.querySelector('.dashboard-grid')).toBeNull();
    expect(el.textContent).toContain('Chưa có máy nào');
  });

  it('disconnects the hub on destroy', async () => {
    const fixture = await createDashboard([]);

    fixture.destroy();

    expect(hub.disconnect).toHaveBeenCalled();
  });

  it('a card flips to no-signal once the clock advances past the threshold, with no new SignalR event (AC #3 setup)', async () => {
    const fixture = await createDashboard(
      [{ machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', status: 'Running', counter: 5, lastReportedAt: BASE_TIME }],
      30,
    );
    expect(fixture.nativeElement.querySelector('.machine-status-card--running')).toBeTruthy();

    clockTick.nowMs.set(BASE_TIME_MS + 31_000);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.machine-status-card--no-signal')).toBeTruthy();
  });

  it('a card returns to its real status once a new reading arrives, even after going no-signal (AC #3)', async () => {
    const fixture = await createDashboard(
      [{ machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', status: 'Running', counter: 5, lastReportedAt: BASE_TIME }],
      30,
    );
    clockTick.nowMs.set(BASE_TIME_MS + 31_000);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.machine-status-card--no-signal')).toBeTruthy();

    hub.lastEvent.set({ machineId: 'm1', status: 'Running', counter: 6, lastReportedAt: new Date(BASE_TIME_MS + 31_000).toISOString() });
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.machine-status-card--running')).toBeTruthy();
    expect(el.querySelector('.machine-status-card--no-signal')).toBeNull();
  });

  it('tapping a Stopped card opens the picker with that machine\'s active-only reason codes', async () => {
    const fixture = await createDashboard([
      { machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', siteId: 's1', status: 'Stopped', counter: 5, lastReportedAt: BASE_TIME },
    ]);

    (fixture.nativeElement.querySelector('[data-testid="machine-status-card"]') as HTMLElement).click();
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
    (fixture.nativeElement.querySelector('[data-testid="machine-status-card"]') as HTMLElement).click();
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
});
