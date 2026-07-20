import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService, provideTranslateLoader } from '@ngx-translate/core';
import { beforeEach, afterEach, describe, it, expect } from 'vitest';
import { HttpTranslateLoader } from '../i18n/http-translate-loader';
import { Shell } from './shell';
import { fakeJwt } from '../../../testing/fake-jwt';

/** Flushes pending microtasks (e.g. the `await firstValueFrom(...)` chain inside ScopeService.loadSites()) so a second chained HTTP call becomes visible to the mock backend. */
function flushMicrotasks(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

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

  /** Mounts Shell, flushes i18n + the ScopeService's Sites/Lines calls (triggered by SiteLineSelector's ngOnInit), and returns the stabilized fixture. */
  async function createShell(siteResponse: Array<{ id: string; name: string }>, i18n: Record<string, unknown>) {
    const fixture = TestBed.createComponent(Shell);
    httpMock.expectOne('/i18n/vi.json').flush(i18n);
    fixture.detectChanges();

    httpMock.expectOne('/api/master-data/sites').flush(siteResponse);
    await flushMicrotasks();
    if (siteResponse.length > 0) {
      httpMock.expectOne(`/api/master-data/sites/${siteResponse[0].id}/lines`).flush([]);
      await flushMicrotasks();
    }
    fixture.detectChanges();

    return fixture;
  }

  it('shows Dashboard/Downtime/Reports but hides Master Data for a Manager', async () => {
    localStorage.setItem('oee_access_token', fakeJwt({ sub: '1', role: 'Manager' }));
    setUp();

    const fixture = await createShell([{ id: 'site-1', name: 'Site A' }], {
      nav: { dashboard: 'D', downtime: 'DT', reports: 'R', logout: 'L' },
    });

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="nav-/dashboard"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="nav-/downtime"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="nav-/reports"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="nav-/master-data"]')).toBeNull();

    localStorage.clear();
  });

  it('shows all four sidebar items for an Admin', async () => {
    localStorage.setItem('oee_access_token', fakeJwt({ sub: '1', role: 'Admin' }));
    setUp();

    const fixture = await createShell([{ id: 'site-1', name: 'Site A' }], {
      nav: { dashboard: 'D', downtime: 'DT', reports: 'R', masterData: 'M', logout: 'L' },
    });

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="nav-/master-data"]')).toBeTruthy();

    localStorage.clear();
  });

  it('switching language updates the rendered sidebar labels without a reload (AC #4)', async () => {
    localStorage.setItem('oee_access_token', fakeJwt({ sub: '1', role: 'Admin' }));
    setUp();

    const fixture = await createShell([{ id: 'site-1', name: 'Site A' }], {
      nav: { dashboard: 'Bảng điều khiển', downtime: 'DT', reports: 'R', masterData: 'M', logout: 'L' },
    });

    expect(fixture.nativeElement.textContent).toContain('Bảng điều khiển');

    const el: HTMLElement = fixture.nativeElement;
    (el.querySelector('[data-testid="lang-en"]') as HTMLButtonElement).click();
    httpMock.expectOne('/i18n/en.json').flush({ nav: { dashboard: 'Dashboard', downtime: 'DT', reports: 'R', masterData: 'M', logout: 'Logout' } });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Dashboard');
    expect(fixture.nativeElement.textContent).not.toContain('Bảng điều khiển');
    expect(localStorage.getItem('oee_lang')).toBe('en');

    localStorage.clear();
  });

  it('hides the Site/Line selector when the caller has only one Site (AC #1)', async () => {
    localStorage.setItem('oee_access_token', fakeJwt({ sub: '1', role: 'Manager' }));
    setUp();

    const fixture = await createShell([{ id: 'site-1', name: 'Site A' }], {
      nav: { dashboard: 'D', downtime: 'DT', reports: 'R', logout: 'L' },
    });

    expect(fixture.nativeElement.querySelector('[data-testid="site-line-selector"]')).toBeNull();

    localStorage.clear();
  });

  it('shows the Site/Line selector with only the scoped Sites when the caller has more than one (AC #2)', async () => {
    localStorage.setItem('oee_access_token', fakeJwt({ sub: '1', role: 'Manager' }));
    setUp();

    const fixture = await createShell(
      [
        { id: 'site-1', name: 'Site A' },
        { id: 'site-2', name: 'Site B' },
      ],
      { nav: { dashboard: 'D', downtime: 'DT', reports: 'R', logout: 'L' } },
    );

    expect(fixture.nativeElement.querySelector('[data-testid="site-line-selector"]')).toBeTruthy();

    localStorage.clear();
  });

  it('collapses the sidebar to an icon rail when the toggle button is clicked', async () => {
    localStorage.setItem('oee_access_token', fakeJwt({ sub: '1', role: 'Manager' }));
    setUp();

    const fixture = await createShell([{ id: 'site-1', name: 'Site A' }], {
      nav: { dashboard: 'D', downtime: 'DT', reports: 'R', logout: 'L' },
    });

    const el: HTMLElement = fixture.nativeElement;
    const sidebar = el.querySelector('[data-testid="sidebar"]') as HTMLElement;
    expect(sidebar.classList).not.toContain('shell__sidebar--collapsed');

    (el.querySelector('[data-testid="sidebar-toggle"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(sidebar.classList).toContain('shell__sidebar--collapsed');

    localStorage.clear();
  });

  it('shows the username in the topbar and logs out from the user menu', async () => {
    localStorage.setItem('oee_access_token', fakeJwt({ sub: '1', role: 'Manager', unique_name: 'manager1' }));
    setUp();

    const fixture = await createShell([{ id: 'site-1', name: 'Site A' }], {
      nav: { dashboard: 'D', downtime: 'DT', reports: 'R', logout: 'Log out' },
    });

    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('manager1');

    (el.querySelector('[data-testid="user-menu-trigger"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const logoutItem = el.querySelector('[data-automationid="logout-btn"]') as HTMLElement;
    expect(logoutItem).toBeTruthy();
    expect(logoutItem.textContent).toContain('Log out');
    logoutItem.click();
    fixture.detectChanges();

    expect(localStorage.getItem('oee_access_token')).toBeNull();

    localStorage.clear();
  });
});
