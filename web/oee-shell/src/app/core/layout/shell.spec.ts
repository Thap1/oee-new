import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService, provideTranslateLoader, TranslateService } from '@ngx-translate/core';
import { beforeEach, afterEach, describe, it, expect } from 'vitest';
import { HttpTranslateLoader } from '../i18n/http-translate-loader';
import { Shell } from './shell';
import { fakeJwt } from '../../../testing/fake-jwt';

describe('Shell', () => {
  let httpMock: HttpTestingController;

  function setUp() {
    TestBed.configureTestingModule({
      imports: [Shell],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'vi', fallbackLang: 'vi' }),
        provideTranslateLoader(HttpTranslateLoader),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => httpMock?.verify());

  it('shows Dashboard/Downtime/Reports but hides Master Data for a Manager', () => {
    localStorage.setItem('oee_access_token', fakeJwt({ sub: '1', role: 'Manager' }));
    setUp();

    const fixture = TestBed.createComponent(Shell);
    httpMock.expectOne('/i18n/vi.json').flush({ nav: { dashboard: 'D', downtime: 'DT', reports: 'R', logout: 'L' } });
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="nav-/dashboard"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="nav-/downtime"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="nav-/reports"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="nav-/master-data"]')).toBeNull();

    localStorage.clear();
  });

  it('shows all four sidebar items for an Admin', () => {
    localStorage.setItem('oee_access_token', fakeJwt({ sub: '1', role: 'Admin' }));
    setUp();

    const fixture = TestBed.createComponent(Shell);
    httpMock.expectOne('/i18n/vi.json').flush({ nav: { dashboard: 'D', downtime: 'DT', reports: 'R', masterData: 'M', logout: 'L' } });
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="nav-/master-data"]')).toBeTruthy();

    localStorage.clear();
  });

  it('switching language updates the rendered sidebar labels without a reload (AC #4)', () => {
    localStorage.setItem('oee_access_token', fakeJwt({ sub: '1', role: 'Admin' }));
    setUp();

    const fixture = TestBed.createComponent(Shell);
    httpMock.expectOne('/i18n/vi.json').flush({ nav: { dashboard: 'Bảng điều khiển', downtime: 'DT', reports: 'R', masterData: 'M', logout: 'L' } });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Bảng điều khiển');

    const translate = TestBed.inject(TranslateService);
    translate.use('en');
    httpMock.expectOne('/i18n/en.json').flush({ nav: { dashboard: 'Dashboard', downtime: 'DT', reports: 'R', masterData: 'M', logout: 'Logout' } });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Dashboard');
    expect(fixture.nativeElement.textContent).not.toContain('Bảng điều khiển');

    localStorage.clear();
  });
});
