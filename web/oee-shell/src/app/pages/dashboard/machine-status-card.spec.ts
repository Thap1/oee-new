import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslateLoader, provideTranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { HttpTranslateLoader } from '../../core/i18n/http-translate-loader';
import { MachineStatusDto } from './dashboard.service';
import { MachineStatusCard } from './machine-status-card';

const I18N_VI = {
  dashboard: { status: { Running: 'Đang chạy', Stopped: 'Dừng', Idle: 'Chờ', Fault: 'Lỗi' } },
};

function snapshot(overrides: Partial<MachineStatusDto> = {}): MachineStatusDto {
  return {
    machineId: 'machine-1',
    machineName: 'Machine 1',
    lineId: 'line-1',
    status: 'Running',
    counter: 10,
    lastReportedAt: '2026-07-21T08:00:00Z',
    ...overrides,
  };
}

describe('MachineStatusCard', () => {
  let httpMock: HttpTestingController;

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
  });

  afterEach(() => httpMock.verify());

  function create(snap: MachineStatusDto) {
    const fixture = TestBed.createComponent(MachineStatusCard);
    fixture.componentInstance.snapshot = snap;
    httpMock.expectOne('/i18n/vi.json').flush(I18N_VI);
    fixture.detectChanges();
    return fixture;
  }

  it('renders a skeleton when the machine has never reported (status: null)', () => {
    const fixture = create(snapshot({ status: null, counter: null, lastReportedAt: null }));

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
    fixture.componentInstance.justUpdated = true;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.machine-status-card--pulse')).toBeTruthy();
  });
});
