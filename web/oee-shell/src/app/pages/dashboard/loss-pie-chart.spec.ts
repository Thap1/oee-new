import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslateLoader, provideTranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { HttpTranslateLoader } from '../../core/i18n/http-translate-loader';
import { EquipmentOption, LossPieChart } from './loss-pie-chart';

const I18N_VI = {
  lossChart: {
    title: 'Phân bổ tổn thất',
    byEquipment: 'Theo Equipment',
    byArea: 'Theo Production Area',
    selectTarget: 'Chọn...',
    availability: 'Availability',
    performance: 'Performance',
    quality: 'Quality',
    emptyState: 'Chưa có dữ liệu dừng máy cho lựa chọn này.',
    loadError: 'Không tải được dữ liệu tổn thất. Vui lòng thử lại.',
    selectDate: 'Chọn ngày...',
    qualityRejectCaption: 'Phế phẩm: {{quantity}}',
    unattributedCaption: 'Dừng máy chưa gán lý do: {{seconds}}s',
    reasonBreakdown: {
      title: '{{category}} — theo mã lý do',
      empty: 'Không có mã lý do nào góp phần vào lát này.',
    },
  },
};

const EQUIPMENT: EquipmentOption[] = [{ machineId: 'm1', machineName: 'Machine 1' }];

function breakdown(overrides: Partial<Record<string, number>> = {}) {
  return {
    targetId: 'm1',
    targetType: 'Equipment',
    availabilitySeconds: 0,
    performanceSeconds: 0,
    qualitySeconds: 0,
    unattributedSeconds: 0,
    qualityRejectQuantity: 0,
    ...overrides,
  };
}

describe('LossPieChart', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [LossPieChart],
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

  function create(equipmentOptions: EquipmentOption[] = EQUIPMENT) {
    const fixture = TestBed.createComponent(LossPieChart);
    fixture.componentInstance.equipmentOptions = equipmentOptions;
    fixture.detectChanges();
    httpMock.expectOne('/i18n/vi.json').flush(I18N_VI);
    fixture.detectChanges();
    return fixture;
  }

  it('does not fetch anything until the user picks a target (no auto-select)', () => {
    create();
    // afterEach's httpMock.verify() itself proves no unexpected request was made on load.
  });

  it('selecting an Equipment target fetches its breakdown and populates the chart data', async () => {
    const fixture = create();

    const selectPromise = fixture.componentInstance.onTargetChange('m1');
    httpMock
      .expectOne('/api/analytics/loss-breakdown?targetType=Equipment&targetId=m1')
      .flush(breakdown({ availabilitySeconds: 100, performanceSeconds: 20, qualitySeconds: 5 }));
    await selectPromise;

    const chartData = fixture.componentInstance.chartData();
    expect(chartData).not.toBeNull();
    expect(chartData!.datasets[0].data).toEqual([100, 20, 5]);
    expect(chartData!.datasets[0].backgroundColor).toEqual(['#EF4444', '#F59E0B', '#8B5CF6']);
  });

  it('switching to Area lazily fetches the scoped area list', async () => {
    const fixture = create();

    const switchPromise = fixture.componentInstance.onTargetTypeChange('Area');
    httpMock
      .expectOne('/api/analytics/loss-areas')
      .flush([{ lineId: 'l1', lineName: 'Line 1', siteId: 's1' }]);
    await switchPromise;

    expect(fixture.componentInstance.targetOptions()).toEqual([{ label: 'Line 1', value: 'l1' }]);
  });

  it('switching back to Equipment does not re-fetch areas already loaded once', async () => {
    const fixture = create();
    const firstSwitch = fixture.componentInstance.onTargetTypeChange('Area');
    httpMock.expectOne('/api/analytics/loss-areas').flush([{ lineId: 'l1', lineName: 'Line 1', siteId: 's1' }]);
    await firstSwitch;

    await fixture.componentInstance.onTargetTypeChange('Equipment');
    await fixture.componentInstance.onTargetTypeChange('Area');
    // A second /api/analytics/loss-areas call here would fail httpMock.verify() in afterEach.
  });

  it('selecting a date re-fetches the current target breakdown with the date query param', async () => {
    const fixture = create();
    const selectPromise = fixture.componentInstance.onTargetChange('m1');
    httpMock.expectOne('/api/analytics/loss-breakdown?targetType=Equipment&targetId=m1').flush(breakdown());
    await selectPromise;

    const datePromise = fixture.componentInstance.onDateChange(new Date(2026, 6, 20));
    httpMock
      .expectOne('/api/analytics/loss-breakdown?targetType=Equipment&targetId=m1&date=2026-07-20')
      .flush(breakdown({ availabilitySeconds: 10 }));
    await datePromise;

    expect(fixture.componentInstance.chartData()!.datasets[0].data).toEqual([10, 0, 0]);
  });

  it('clearing the date (null) re-fetches without a date param', async () => {
    const fixture = create();
    const selectPromise = fixture.componentInstance.onTargetChange('m1');
    httpMock.expectOne('/api/analytics/loss-breakdown?targetType=Equipment&targetId=m1').flush(breakdown());
    await selectPromise;
    const setDatePromise = fixture.componentInstance.onDateChange(new Date(2026, 6, 20));
    httpMock.expectOne('/api/analytics/loss-breakdown?targetType=Equipment&targetId=m1&date=2026-07-20').flush(breakdown());
    await setDatePromise;

    const clearPromise = fixture.componentInstance.onDateChange(null);
    httpMock.expectOne('/api/analytics/loss-breakdown?targetType=Equipment&targetId=m1').flush(breakdown());
    await clearPromise;
  });

  it('selecting a pie slice fetches and exposes that category\'s reason breakdown (AC #2)', async () => {
    // jsdom has no real <canvas> 2D context, so a real chart click can't be simulated here — this
    // calls the handler directly, the same boundary drawn around chartData() for the same reason.
    const fixture = create();
    const selectPromise = fixture.componentInstance.onTargetChange('m1');
    httpMock.expectOne('/api/analytics/loss-breakdown?targetType=Equipment&targetId=m1').flush(breakdown({ availabilitySeconds: 50 }));
    await selectPromise;

    const slicePromise = fixture.componentInstance.onSliceSelected({ element: { index: 0 } });
    httpMock
      .expectOne('/api/analytics/loss-breakdown/reasons?targetType=Equipment&targetId=m1&lossCategory=AvailabilityLoss')
      .flush([{ reasonCodeId: 'r1', reasonCodeName: 'Kẹt khuôn', durationSeconds: 50 }]);
    await slicePromise;

    expect(fixture.componentInstance.reasonBreakdownCategory()).toBe('AvailabilityLoss');
    expect(fixture.componentInstance.reasonBreakdown()).toEqual([{ reasonCodeId: 'r1', reasonCodeName: 'Kẹt khuôn', durationSeconds: 50 }]);
  });

  it('switching targets clears any previously shown reason breakdown', async () => {
    const fixture = create([...EQUIPMENT, { machineId: 'm2', machineName: 'Machine 2' }]);
    const selectPromise = fixture.componentInstance.onTargetChange('m1');
    httpMock.expectOne('/api/analytics/loss-breakdown?targetType=Equipment&targetId=m1').flush(breakdown({ availabilitySeconds: 50 }));
    await selectPromise;
    const slicePromise = fixture.componentInstance.onSliceSelected({ element: { index: 0 } });
    httpMock
      .expectOne('/api/analytics/loss-breakdown/reasons?targetType=Equipment&targetId=m1&lossCategory=AvailabilityLoss')
      .flush([{ reasonCodeId: 'r1', reasonCodeName: 'Kẹt khuôn', durationSeconds: 50 }]);
    await slicePromise;

    const secondSelectPromise = fixture.componentInstance.onTargetChange('m2');
    httpMock.expectOne('/api/analytics/loss-breakdown?targetType=Equipment&targetId=m2').flush(breakdown());
    await secondSelectPromise;

    expect(fixture.componentInstance.reasonBreakdownCategory()).toBeNull();
    expect(fixture.componentInstance.reasonBreakdown()).toEqual([]);
  });

  it('a failed breakdown fetch shows an error state instead of leaving stale UI with no feedback', async () => {
    const fixture = create();

    const selectPromise = fixture.componentInstance.onTargetChange('m1');
    httpMock.expectOne('/api/analytics/loss-breakdown?targetType=Equipment&targetId=m1').flush('boom', { status: 500, statusText: 'Server Error' });
    await selectPromise;
    fixture.detectChanges();

    expect(fixture.componentInstance.error()).toBe(true);
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="loss-pie-chart-error"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="loss-pie-chart-canvas"]')).toBeNull();
  });

  it('a successful fetch after a prior error clears the error state', async () => {
    const fixture = create();
    const firstSelect = fixture.componentInstance.onTargetChange('m1');
    httpMock.expectOne('/api/analytics/loss-breakdown?targetType=Equipment&targetId=m1').flush('boom', { status: 500, statusText: 'Server Error' });
    await firstSelect;
    expect(fixture.componentInstance.error()).toBe(true);

    const retryPromise = fixture.componentInstance.onDateChange(null);
    httpMock.expectOne('/api/analytics/loss-breakdown?targetType=Equipment&targetId=m1').flush(breakdown({ availabilitySeconds: 10 }));
    await retryPromise;

    expect(fixture.componentInstance.error()).toBe(false);
  });

  it('a failed reason-breakdown fetch on slice tap does not disturb the still-valid chart', async () => {
    const fixture = create();
    const selectPromise = fixture.componentInstance.onTargetChange('m1');
    httpMock.expectOne('/api/analytics/loss-breakdown?targetType=Equipment&targetId=m1').flush(breakdown({ availabilitySeconds: 50 }));
    await selectPromise;

    const slicePromise = fixture.componentInstance.onSliceSelected({ element: { index: 0 } });
    httpMock
      .expectOne('/api/analytics/loss-breakdown/reasons?targetType=Equipment&targetId=m1&lossCategory=AvailabilityLoss')
      .flush('boom', { status: 500, statusText: 'Server Error' });
    await slicePromise;

    expect(fixture.componentInstance.error()).toBe(false);
    expect(fixture.componentInstance.reasonBreakdown()).toEqual([]);
    expect(fixture.componentInstance.chartData()).not.toBeNull();
  });

  it('surfaces qualityRejectQuantity and unattributedSeconds as a caption instead of silently dropping them', async () => {
    const fixture = create();
    const selectPromise = fixture.componentInstance.onTargetChange('m1');
    httpMock
      .expectOne('/api/analytics/loss-breakdown?targetType=Equipment&targetId=m1')
      .flush(breakdown({ availabilitySeconds: 50, unattributedSeconds: 15, qualityRejectQuantity: 3 }));
    await selectPromise;
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const caption = el.querySelector('[data-testid="loss-pie-chart-caption"]')!;
    expect(caption).toBeTruthy();
    expect(caption.textContent).toContain('Phế phẩm: 3');
    expect(caption.textContent).toContain('Dừng máy chưa gán lý do: 15s');
  });

  it('does not render a caption when there is nothing supplementary to report', async () => {
    const fixture = create();
    const selectPromise = fixture.componentInstance.onTargetChange('m1');
    httpMock.expectOne('/api/analytics/loss-breakdown?targetType=Equipment&targetId=m1').flush(breakdown({ availabilitySeconds: 50 }));
    await selectPromise;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="loss-pie-chart-caption"]')).toBeNull();
  });

  it('an all-zero breakdown shows the empty state instead of the chart', async () => {
    const fixture = create();

    const selectPromise = fixture.componentInstance.onTargetChange('m1');
    httpMock.expectOne('/api/analytics/loss-breakdown?targetType=Equipment&targetId=m1').flush(breakdown());
    await selectPromise;
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="loss-pie-chart-empty"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="loss-pie-chart-canvas"]')).toBeNull();
    expect(el.textContent).toContain('Chưa có dữ liệu dừng máy cho lựa chọn này.');
  });
});
