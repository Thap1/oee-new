import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslateLoader, provideTranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { HttpTranslateLoader } from '../../core/i18n/http-translate-loader';
import { QualityRejectControl } from './quality-reject-control';

const I18N_VI = {
  dashboard: { qualityReject: { title: 'Ghi nhận phế phẩm', quantity: 'Số lượng', submit: 'Lưu' } },
};

describe('QualityRejectControl', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [QualityRejectControl],
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

  function create(machineId = 'm1') {
    const fixture = TestBed.createComponent(QualityRejectControl);
    fixture.componentInstance.machineId = machineId;
    fixture.detectChanges();
    httpMock.expectOne('/i18n/vi.json').flush(I18N_VI);
    fixture.detectChanges();
    return fixture;
  }

  it('clicking the trigger opens the dialog', () => {
    const fixture = create();

    (fixture.nativeElement.querySelector('[data-testid="quality-reject-trigger"]') as HTMLElement).click();
    fixture.detectChanges();

    expect(fixture.componentInstance.dialogOpen()).toBe(true);
  });

  it('the submit button is disabled while quantity is unset or non-positive', () => {
    const fixture = create();
    fixture.componentInstance.openDialog();
    fixture.detectChanges();

    let submitBtn = fixture.nativeElement.querySelector('[data-testid="quality-reject-submit-btn"] button') as HTMLButtonElement;
    expect(submitBtn?.disabled ?? true).toBe(true);

    fixture.componentInstance.quantity.set(0);
    fixture.detectChanges();
    submitBtn = fixture.nativeElement.querySelector('[data-testid="quality-reject-submit-btn"] button') as HTMLButtonElement;
    expect(submitBtn?.disabled ?? true).toBe(true);
  });

  it('submitting a valid quantity calls the service with the right machineId/quantity and closes the dialog', async () => {
    const fixture = create('machine-42');
    fixture.componentInstance.openDialog();
    fixture.componentInstance.quantity.set(5);

    const submitPromise = fixture.componentInstance.submit();
    const req = httpMock.expectOne('/api/production/machines/machine-42/quality-rejects');
    expect(req.request.body).toEqual({ quantity: 5 });
    req.flush(null);
    await submitPromise;

    expect(fixture.componentInstance.dialogOpen()).toBe(false);
  });

  it('does not call the service when quantity is zero', async () => {
    const fixture = create();
    fixture.componentInstance.openDialog();
    fixture.componentInstance.quantity.set(0);

    await fixture.componentInstance.submit();

    httpMock.expectNone('/api/production/machines/m1/quality-rejects');
  });
});
