import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslateLoader, provideTranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { ClockTickService } from '../../core/realtime/clock-tick.service';
import { HttpTranslateLoader } from '../../core/i18n/http-translate-loader';
import { MachineStatusDto } from './dashboard.service';
import { MachineStatusCard } from './machine-status-card';

const I18N_VI = {
  dashboard: { status: { Running: 'Đang chạy', Stopped: 'Dừng', Idle: 'Chờ', Fault: 'Lỗi', noSignal: 'Mất tín hiệu {{minutes}}p' } },
};

const BASE_TIME = '2026-07-21T08:00:00Z';
const BASE_TIME_MS = Date.parse(BASE_TIME);
const THRESHOLD_SECONDS = 60;

function snapshot(overrides: Partial<MachineStatusDto> = {}): MachineStatusDto {
  return {
    machineId: 'machine-1',
    machineName: 'Machine 1',
    lineId: 'line-1',
    siteId: 'site-1',
    status: 'Running',
    counter: 10,
    lastReportedAt: BASE_TIME,
    ...overrides,
  };
}

describe('MachineStatusCard', () => {
  let httpMock: HttpTestingController;
  let clockTick: ClockTickService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MachineStatusCard],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'vi', fallbackLang: 'vi' }),
        provideTranslateLoader(HttpTranslateLoader),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    clockTick = TestBed.inject(ClockTickService);
    // Pin the shared clock to the snapshot's own baseline time so "now - lastReportedAt" is
    // deterministic instead of depending on the real wall clock at whatever moment the test runs.
    clockTick.nowMs.set(BASE_TIME_MS);
  });

  afterEach(() => httpMock.verify());

  function create(snap: MachineStatusDto) {
    const fixture = TestBed.createComponent(MachineStatusCard);
    fixture.componentInstance.snapshot = snap;
    fixture.componentInstance.noSignalThresholdSeconds = THRESHOLD_SECONDS;
    fixture.detectChanges();
    httpMock.expectOne('/i18n/vi.json').flush(I18N_VI);
    fixture.detectChanges();
    return fixture;
  }

  it('renders a skeleton when the machine has never reported (status: null)', () => {
    // The `@else`/`@else if` branches (the only places TranslatePipe is used) never render here, so
    // no i18n HTTP request is made at all — nothing to flush, unlike the other cases below.
    const fixture = TestBed.createComponent(MachineStatusCard);
    fixture.componentInstance.snapshot = snapshot({ status: null, counter: null, lastReportedAt: null });
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.machine-status-card--skeleton')).toBeTruthy();
    expect(el.querySelector('p-skeleton')).toBeTruthy();
  });

  it('renders the running state with its own color class, icon and label', () => {
    const fixture = create(snapshot({ status: 'Running' }));

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.machine-status-card--running')).toBeTruthy();
    expect(el.querySelector('.pi-play')).toBeTruthy();
    expect(el.textContent).toContain('Đang chạy');
  });

  it('renders the stopped state with its own color class, icon and label', () => {
    const fixture = create(snapshot({ status: 'Stopped' }));

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.machine-status-card--stopped')).toBeTruthy();
    expect(el.querySelector('.pi-stop-circle')).toBeTruthy();
    expect(el.textContent).toContain('Dừng');
  });

  it('renders the idle state with its own color class, icon and label', () => {
    const fixture = create(snapshot({ status: 'Idle' }));

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.machine-status-card--idle')).toBeTruthy();
    expect(el.querySelector('.pi-pause')).toBeTruthy();
    expect(el.textContent).toContain('Chờ');
  });

  it('applies the pulse class only when justUpdated is true', () => {
    const fixture = create(snapshot({ status: 'Running' }));
    // setInput (not a direct property assignment) is the API Angular's change-detection tracking
    // expects for a value mutated after the initial render — a raw assignment here previously
    // triggered a spurious NG0100 (the previous/current comparison read a stale cached render).
    fixture.componentRef.setInput('justUpdated', true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.machine-status-card--pulse')).toBeTruthy();
  });

  it('clicking a Stopped card emits cardTapped with the machineId', () => {
    const fixture = create(snapshot({ status: 'Stopped', machineId: 'm-42' }));
    const emitted: string[] = [];
    fixture.componentInstance.cardTapped.subscribe((id: string) => emitted.push(id));

    (fixture.nativeElement.querySelector('[data-testid="machine-status-card"]') as HTMLElement).click();

    expect(emitted).toEqual(['m-42']);
  });

  it('clicking a Running card emits nothing', () => {
    const fixture = create(snapshot({ status: 'Running' }));
    const emitted: string[] = [];
    fixture.componentInstance.cardTapped.subscribe((id: string) => emitted.push(id));

    (fixture.nativeElement.querySelector('[data-testid="machine-status-card"]') as HTMLElement).click();

    expect(emitted).toEqual([]);
  });

  it('stays the real Stopped color when still within the no-signal threshold (AC #2)', () => {
    clockTick.nowMs.set(BASE_TIME_MS + (THRESHOLD_SECONDS - 1) * 1000);
    const fixture = create(snapshot({ status: 'Stopped' }));

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.machine-status-card--stopped')).toBeTruthy();
    expect(el.querySelector('.machine-status-card--no-signal')).toBeNull();
  });

  it('flips to the distinct gray no-signal card once past the threshold, even for a Stopped reading (AC #1/#2)', () => {
    clockTick.nowMs.set(BASE_TIME_MS + (THRESHOLD_SECONDS + 61) * 1000);
    const fixture = create(snapshot({ status: 'Stopped' }));

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.machine-status-card--no-signal')).toBeTruthy();
    expect(el.querySelector('.machine-status-card--stopped')).toBeNull();
    expect(el.textContent).toContain('Mất tín hiệu 2p');
  });

  it('a never-reported machine (lastReportedAt: null) stays the skeleton, not no-signal', () => {
    clockTick.nowMs.set(BASE_TIME_MS + 1_000_000);
    const fixture = TestBed.createComponent(MachineStatusCard);
    fixture.componentInstance.snapshot = snapshot({ status: null, counter: null, lastReportedAt: null });
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.machine-status-card--skeleton')).toBeTruthy();
    expect(el.querySelector('.machine-status-card--no-signal')).toBeNull();
  });
});
