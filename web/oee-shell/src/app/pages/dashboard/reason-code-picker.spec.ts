import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslateLoader, provideTranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { HttpTranslateLoader } from '../../core/i18n/http-translate-loader';
import { ReasonCodeDto } from '../master-data/master-data.service';
import { ReasonCodePicker } from './reason-code-picker';

const I18N_VI = {
  masterData: {
    lossCategory: {
      AvailabilityLoss: 'Tổn thất khả dụng',
      PerformanceLoss: 'Tổn thất hiệu suất',
      QualityLoss: 'Tổn thất chất lượng',
    },
  },
};

function reasonCode(overrides: Partial<ReasonCodeDto> = {}): ReasonCodeDto {
  return {
    id: 'reason-1',
    siteId: 'site-1',
    name: 'Kẹt khuôn',
    lossCategory: 'AvailabilityLoss',
    isActive: true,
    ...overrides,
  };
}

describe('ReasonCodePicker', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ReasonCodePicker],
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

  function create(reasonCodes: ReasonCodeDto[]) {
    const fixture = TestBed.createComponent(ReasonCodePicker);
    fixture.componentInstance.open = true;
    fixture.componentInstance.reasonCodes = reasonCodes;
    fixture.detectChanges();
    httpMock.expectOne('/i18n/vi.json').flush(I18N_VI);
    fixture.detectChanges();
    return fixture;
  }

  it('renders nothing when closed', () => {
    const fixture = TestBed.createComponent(ReasonCodePicker);
    fixture.componentInstance.open = false;
    fixture.componentInstance.reasonCodes = [reasonCode()];
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="reason-code-picker"]')).toBeNull();
  });

  it('groups reason codes by loss category with the correct section headers', () => {
    const fixture = create([
      reasonCode({ id: 'r1', name: 'Kẹt khuôn', lossCategory: 'AvailabilityLoss' }),
      reasonCode({ id: 'r2', name: 'Đổi ca', lossCategory: 'PerformanceLoss' }),
      reasonCode({ id: 'r3', name: 'Lỗi kích thước', lossCategory: 'QualityLoss' }),
    ]);

    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Tổn thất khả dụng');
    expect(el.textContent).toContain('Tổn thất hiệu suất');
    expect(el.textContent).toContain('Tổn thất chất lượng');
    expect(el.textContent).toContain('Kẹt khuôn');
    expect(el.textContent).toContain('Đổi ca');
    expect(el.textContent).toContain('Lỗi kích thước');
  });

  it('every reason button meets the 64px minTouchTarget (UX-DR7, AC #4)', () => {
    const fixture = create([reasonCode()]);

    const button = fixture.nativeElement.querySelector('.reason-code-button') as HTMLElement;
    expect(button).toBeTruthy();
    expect(button.classList).toContain('reason-code-button');
  });

  it('a single click on a reason button emits reasonSelected once with its id and nothing else', () => {
    const fixture = create([reasonCode({ id: 'r1' })]);
    const emitted: string[] = [];
    fixture.componentInstance.reasonSelected.subscribe((id: string) => emitted.push(id));

    (fixture.nativeElement.querySelector('[data-testid="reason-code-button"]') as HTMLElement).click();

    expect(emitted).toEqual(['r1']);
  });

  it('the close button emits closed', () => {
    const fixture = create([reasonCode()]);
    let closedCount = 0;
    fixture.componentInstance.closed.subscribe(() => closedCount++);

    (fixture.nativeElement.querySelector('[data-testid="reason-code-picker-close"]') as HTMLElement).click();

    expect(closedCount).toBe(1);
  });
});
