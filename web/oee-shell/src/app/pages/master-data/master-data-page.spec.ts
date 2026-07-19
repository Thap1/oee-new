import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslateLoader, provideTranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { HttpTranslateLoader } from '../../core/i18n/http-translate-loader';
import { fakeJwt } from '../../../testing/fake-jwt';
import { MasterDataPage } from './master-data-page';

const I18N_VI = {
  nav: { masterData: 'Danh mục' },
  masterData: {
    sites: 'Site',
    lines: 'Line',
    machines: 'Máy',
    add: 'Thêm',
    edit: 'Sửa',
    name: 'Tên',
    save: 'Lưu',
    cancel: 'Huỷ',
    selectSitePrompt: 'Chọn một Site',
    selectLinePrompt: 'Chọn một Line',
    confirmDelete: 'Xoá "{{name}}"?',
    shiftSchedules: 'Ca làm việc',
    selectSiteForShiftsPrompt: 'Chọn một Site để xem ca',
    allLines: 'Tất cả Line',
    shiftLine: 'Line',
    startTime: 'Giờ bắt đầu',
    endTime: 'Giờ kết thúc',
    confirmDeleteShift: 'Xoá ca "{{name}}"?',
    error: {
      notFound: 'Không tìm thấy.',
      parentNotFound: 'Không tìm thấy bản ghi cha.',
      hasDependents: 'Không thể xoá — vẫn còn: {{dependents}}',
      shiftOverlap: 'Chồng lấn: {{message}}',
      validationError: 'Không hợp lệ: {{message}}',
      forbidden: 'Không có quyền.',
      generic: 'Đã xảy ra lỗi.',
    },
  },
};

/** Flushes pending microtasks (e.g. the `await firstValueFrom(...)` chain inside loadSites()) so signal updates from an already-flushed HTTP response are visible before the next detectChanges(). */
function flushMicrotasks(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

async function setUp(role: string) {
  localStorage.setItem('oee_access_token', fakeJwt({ sub: '1', role }));

  TestBed.configureTestingModule({
    imports: [MasterDataPage],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideTranslateService({ lang: 'vi', fallbackLang: 'vi' }),
      provideTranslateLoader(HttpTranslateLoader),
    ],
  });

  const httpMock = TestBed.inject(HttpTestingController);
  const fixture = TestBed.createComponent(MasterDataPage);
  fixture.detectChanges();

  httpMock.expectOne('/i18n/vi.json').flush(I18N_VI);
  httpMock.expectOne('/api/master-data/sites').flush([{ id: 'site-1', name: 'Site A' }]);
  await flushMicrotasks();
  fixture.detectChanges();

  return { fixture, httpMock };
}

describe('MasterDataPage', () => {
  afterEach(() => {
    localStorage.clear();
  });

  it('shows the Add/Edit/Delete actions for an Admin', async () => {
    const { fixture, httpMock } = await setUp('Admin');

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="add-site-btn"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="edit-site-btn"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="delete-site-btn"]')).toBeTruthy();

    httpMock.verify();
  });

  it('hides the Add/Edit/Delete actions for a non-Admin role (AC #4, double-check UI+API)', async () => {
    const { fixture, httpMock } = await setUp('Manager');

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="add-site-btn"]')).toBeNull();
    expect(el.querySelector('[data-testid="edit-site-btn"]')).toBeNull();
    expect(el.querySelector('[data-testid="delete-site-btn"]')).toBeNull();
    // Read access is still available — the row itself renders.
    expect(el.textContent).toContain('Site A');

    httpMock.verify();
  });

  it('creating a Site calls the API and appends it to the list', async () => {
    const { fixture, httpMock } = await setUp('Admin');
    const component = fixture.componentInstance;

    component.openCreate('site');
    component.dialogName.set('Site B');
    const savePromise = component.save();

    const req = httpMock.expectOne('/api/master-data/sites');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Site B' });
    req.flush({ id: 'site-2', name: 'Site B' });
    await savePromise;
    fixture.detectChanges();

    expect(component.sites()).toContainEqual({ id: 'site-2', name: 'Site B' });
    expect(component.dialog().visible).toBe(false);

    httpMock.verify();
  });

  it('deleting a Site with dependent Lines shows the HAS_DEPENDENTS error (AC #5)', async () => {
    const { fixture, httpMock } = await setUp('Admin');
    const component = fixture.componentInstance;
    const originalConfirm = window.confirm;
    window.confirm = () => true;

    const deletePromise = component.deleteSite({ id: 'site-1', name: 'Site A' });

    const req = httpMock.expectOne('/api/master-data/sites/site-1');
    expect(req.request.method).toBe('DELETE');
    req.flush(
      { code: 'HAS_DEPENDENTS', message: "Site 'site-1' still has 1 dependent record(s).", details: { dependentNames: ['Line A'] } },
      { status: 409, statusText: 'Conflict' },
    );
    await deletePromise;
    fixture.detectChanges();

    expect(component.error()).toContain('Line A');
    expect(component.sites()).toContainEqual({ id: 'site-1', name: 'Site A' });

    window.confirm = originalConfirm;
    httpMock.verify();
  });

  it('selecting a Site loads its Lines and Shift Schedules', async () => {
    const { fixture, httpMock } = await setUp('Admin');
    const component = fixture.componentInstance;

    const selectPromise = component.selectSite({ id: 'site-1', name: 'Site A' });

    httpMock.expectOne('/api/master-data/sites/site-1/lines').flush([]);
    httpMock
      .expectOne('/api/master-data/sites/site-1/shift-schedules')
      .flush([{ id: 'shift-1', siteId: 'site-1', lineId: null, name: 'Day Shift', startTime: '08:00:00', endTime: '16:00:00' }]);
    await selectPromise;
    fixture.detectChanges();

    expect(component.shiftSchedules()).toEqual([
      { id: 'shift-1', siteId: 'site-1', lineId: null, name: 'Day Shift', startTime: '08:00:00', endTime: '16:00:00' },
    ]);

    httpMock.verify();
  });

  it('creating a Shift Schedule calls the API and appends it to the list', async () => {
    const { fixture, httpMock } = await setUp('Admin');
    const component = fixture.componentInstance;

    const selectPromise = component.selectSite({ id: 'site-1', name: 'Site A' });
    httpMock.expectOne('/api/master-data/sites/site-1/lines').flush([]);
    httpMock.expectOne('/api/master-data/sites/site-1/shift-schedules').flush([]);
    await selectPromise;
    fixture.detectChanges();

    component.openCreateShift();
    component.shiftDialogName.set('Day Shift');
    component.shiftDialogStartTime.set('08:00');
    component.shiftDialogEndTime.set('16:00');
    const savePromise = component.saveShift();

    const req = httpMock.expectOne('/api/master-data/sites/site-1/shift-schedules');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Day Shift', lineId: null, startTime: '08:00:00', endTime: '16:00:00' });
    req.flush({ id: 'shift-1', siteId: 'site-1', lineId: null, name: 'Day Shift', startTime: '08:00:00', endTime: '16:00:00' });
    await savePromise;
    fixture.detectChanges();

    expect(component.shiftSchedules()).toContainEqual({
      id: 'shift-1',
      siteId: 'site-1',
      lineId: null,
      name: 'Day Shift',
      startTime: '08:00:00',
      endTime: '16:00:00',
    });
    expect(component.shiftDialog().visible).toBe(false);

    httpMock.verify();
  });

  it('creating an overlapping Shift Schedule shows the SHIFT_OVERLAP error', async () => {
    const { fixture, httpMock } = await setUp('Admin');
    const component = fixture.componentInstance;

    const selectPromise = component.selectSite({ id: 'site-1', name: 'Site A' });
    httpMock.expectOne('/api/master-data/sites/site-1/lines').flush([]);
    httpMock.expectOne('/api/master-data/sites/site-1/shift-schedules').flush([]);
    await selectPromise;
    fixture.detectChanges();

    component.openCreateShift();
    component.shiftDialogName.set('Overlap');
    component.shiftDialogStartTime.set('10:00');
    component.shiftDialogEndTime.set('14:00');
    const savePromise = component.saveShift();

    const req = httpMock.expectOne('/api/master-data/sites/site-1/shift-schedules');
    req.flush({ code: 'SHIFT_OVERLAP', message: "Overlaps with existing shift 'Morning'." }, { status: 409, statusText: 'Conflict' });
    await savePromise;
    fixture.detectChanges();

    expect(component.error()).toContain('Chồng lấn');

    httpMock.verify();
  });

  it('renaming with an invalid name shows the VALIDATION_ERROR message', async () => {
    const { fixture, httpMock } = await setUp('Admin');
    const component = fixture.componentInstance;

    component.openEdit('site', 'site-1', 'Site A');
    component.dialogName.set('Site A');
    const savePromise = component.save();

    const req = httpMock.expectOne('/api/master-data/sites/site-1');
    expect(req.request.method).toBe('PUT');
    req.flush({ code: 'VALIDATION_ERROR', message: 'Site name must be at most 200 characters.' }, { status: 400, statusText: 'Bad Request' });
    await savePromise;
    fixture.detectChanges();

    expect(component.error()).toContain('Site name must be at most 200 characters.');

    httpMock.verify();
  });

  it('deleting the selected Site while its Lines/Shift fetch is still in flight does not let the stale fetch repopulate the panels (race guard)', async () => {
    const { fixture, httpMock } = await setUp('Admin');
    const component = fixture.componentInstance;
    const originalConfirm = window.confirm;
    window.confirm = () => true;

    const selectPromise = component.selectSite({ id: 'site-1', name: 'Site A' });

    const deletePromise = component.deleteSite({ id: 'site-1', name: 'Site A' });
    const deleteReq = httpMock.expectOne('/api/master-data/sites/site-1');
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush(null, { status: 204, statusText: 'No Content' });
    await deletePromise;

    // The Lines/Shift Schedules fetch that selectSite kicked off resolves only now, after the delete
    // already cleared the selection — it must not be allowed to repopulate the panels.
    httpMock.expectOne('/api/master-data/sites/site-1/lines').flush([{ id: 'line-1', name: 'Line A', siteId: 'site-1' }]);
    httpMock
      .expectOne('/api/master-data/sites/site-1/shift-schedules')
      .flush([{ id: 'shift-1', siteId: 'site-1', lineId: null, name: 'Day Shift', startTime: '08:00:00', endTime: '16:00:00' }]);
    await selectPromise;
    fixture.detectChanges();

    expect(component.selectedSite()).toBeNull();
    expect(component.lines()).toEqual([]);
    expect(component.shiftSchedules()).toEqual([]);

    window.confirm = originalConfirm;
    httpMock.verify();
  });
});
