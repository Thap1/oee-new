import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslateLoader, provideTranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { HttpTranslateLoader } from '../../core/i18n/http-translate-loader';
import { DowntimePage } from './downtime-page';

const I18N_VI = {
  nav: { downtime: 'Dừng máy' },
  downtime: {
    machine: 'Máy',
    reason: 'Lý do',
    startedAt: 'Bắt đầu',
    endedAt: 'Kết thúc',
    duration: 'Thời lượng',
    ongoing: 'Đang dừng',
    unattributed: 'Chưa gán lý do',
    emptyState: 'Chưa có dữ liệu dừng máy trong phạm vi của bạn.',
    loadError: 'Không tải được lịch sử dừng máy. Vui lòng thử lại.',
  },
};

function flushMicrotasks(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

describe('DowntimePage', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DowntimePage],
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

  async function createPage(entries: unknown[] | null) {
    const fixture = TestBed.createComponent(DowntimePage);
    fixture.detectChanges();
    httpMock.expectOne('/i18n/vi.json').flush(I18N_VI);

    const req = httpMock.expectOne('/api/production/downtime-history');
    if (entries === null) {
      req.flush({ code: 'INTERNAL_ERROR', message: 'boom' }, { status: 500, statusText: 'Internal Server Error' });
    } else {
      req.flush(entries);
    }
    await flushMicrotasks();
    fixture.detectChanges();

    return fixture;
  }

  it('renders a row per downtime entry, most recent first as returned by the API', async () => {
    const fixture = await createPage([
      {
        id: 'evt-1',
        machineId: 'm-1',
        machineName: 'Máy ép nhựa 01',
        reasonCodeId: 'r-1',
        reasonCodeName: 'Hỏng máy',
        startedAt: '2026-07-24T08:00:00Z',
        endedAt: '2026-07-24T08:05:30Z',
        durationSeconds: 330,
      },
    ]);
    const el: HTMLElement = fixture.nativeElement;

    const row = el.querySelector('[data-testid="downtime-row-evt-1"]')!;
    expect(row).toBeTruthy();
    expect(row.textContent).toContain('Máy ép nhựa 01');
    expect(row.textContent).toContain('Hỏng máy');
    expect(row.textContent).toContain('5m 30s');
  });

  it('shows an ongoing badge and no duration for an open downtime event', async () => {
    const fixture = await createPage([
      {
        id: 'evt-2',
        machineId: 'm-2',
        machineName: 'Máy đóng gói 01',
        reasonCodeId: null,
        reasonCodeName: null,
        startedAt: '2026-07-24T09:00:00Z',
        endedAt: null,
        durationSeconds: null,
      },
    ]);
    const el: HTMLElement = fixture.nativeElement;

    const row = el.querySelector('[data-testid="downtime-row-evt-2"]')!;
    expect(row.querySelector('[data-testid="downtime-ongoing-badge"]')).toBeTruthy();
    expect(row.textContent).toContain('Chưa gán lý do');
  });

  it('shows an empty state when there is no downtime history', async () => {
    const fixture = await createPage([]);
    const el: HTMLElement = fixture.nativeElement;

    expect(el.querySelector('[data-testid="downtime-empty-state"]')).toBeTruthy();
  });

  it('shows a load error state when the request fails', async () => {
    const fixture = await createPage(null);
    const el: HTMLElement = fixture.nativeElement;

    expect(el.querySelector('[data-testid="downtime-load-error"]')).toBeTruthy();
  });
});
