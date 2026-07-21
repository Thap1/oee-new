import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslateLoader, provideTranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { HttpTranslateLoader } from '../../core/i18n/http-translate-loader';
import { MachineStatusHubService } from '../../core/realtime/machine-status-hub.service';
import { DashboardPage } from './dashboard-page';

const I18N_VI = {
  nav: { dashboard: 'Bảng điều khiển' },
  dashboard: { status: { Running: 'Đang chạy', Stopped: 'Dừng', Idle: 'Chờ', Fault: 'Lỗi' } },
};

function flushMicrotasks(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

describe('DashboardPage', () => {
  let httpMock: HttpTestingController;
  let hub: MachineStatusHubService;

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
    // Never open a real WebSocket in a unit test — connect()/disconnect() become no-ops, and
    // `lastEvent` (a plain writable signal) is how tests simulate an incoming broadcast.
    vi.spyOn(hub, 'connect').mockImplementation(() => {});
    vi.spyOn(hub, 'disconnect').mockImplementation(() => {});
  });

  afterEach(() => httpMock.verify());

  async function createDashboard(machineStates: unknown[]) {
    const fixture = TestBed.createComponent(DashboardPage);
    httpMock.expectOne('/i18n/vi.json').flush(I18N_VI);
    fixture.detectChanges();

    await flushMicrotasks();
    httpMock.expectOne('/api/production/machine-states').flush(machineStates);
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
      { machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', status: 'Running', counter: 5, lastReportedAt: '2026-07-21T08:00:00Z' },
    ]);

    expect(fixture.nativeElement.querySelector('.machine-status-card--running')).toBeTruthy();
  });

  it('a MachineStatusChanged event updates the matching card and triggers the pulse', async () => {
    const fixture = await createDashboard([
      { machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', status: 'Running', counter: 5, lastReportedAt: '2026-07-21T08:00:00Z' },
    ]);

    hub.lastEvent.set({ machineId: 'm1', status: 'Stopped', counter: 6, lastReportedAt: '2026-07-21T08:00:05Z' });
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.machine-status-card--stopped')).toBeTruthy();
    expect(el.querySelector('.machine-status-card--pulse')).toBeTruthy();
  });

  it('ignores an event for a machineId not in the current scoped list', async () => {
    const fixture = await createDashboard([
      { machineId: 'm1', machineName: 'Machine 1', lineId: 'l1', status: 'Running', counter: 5, lastReportedAt: '2026-07-21T08:00:00Z' },
    ]);

    hub.lastEvent.set({ machineId: 'out-of-scope-machine', status: 'Stopped', counter: 1, lastReportedAt: '2026-07-21T08:00:05Z' });
    fixture.detectChanges();

    expect(fixture.componentInstance.machines()).toHaveLength(1);
    expect(fixture.componentInstance.machines()[0].status).toBe('Running');
  });

  it('disconnects the hub on destroy', async () => {
    const fixture = await createDashboard([]);

    fixture.destroy();

    expect(hub.disconnect).toHaveBeenCalled();
  });
});
