import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { beforeEach, afterEach, describe, it, expect, vi } from 'vitest';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('does not add an Authorization header when there is no token', () => {
    httpClient.get('/api/whatever').subscribe();

    const req = httpMock.expectOne('/api/whatever');
    expect(req.request.headers.has('Authorization')).toBe(false);
  });

  it('adds a Bearer Authorization header when a token is stored', () => {
    localStorage.setItem('oee_access_token', 'my-token');
    TestBed.inject(AuthService); // picks up the stored token on construction

    httpClient.get('/api/whatever').subscribe();

    const req = httpMock.expectOne('/api/whatever');
    expect(req.request.headers.get('Authorization')).toBe('Bearer my-token');
  });

  it('does not attach the token to a non-API request', () => {
    localStorage.setItem('oee_access_token', 'my-token');
    TestBed.inject(AuthService);

    httpClient.get('/i18n/vi.json').subscribe();

    const req = httpMock.expectOne('/i18n/vi.json');
    expect(req.request.headers.has('Authorization')).toBe(false);
  });

  it('logs out and redirects to /login on a 401 from an authenticated request', () => {
    localStorage.setItem('oee_access_token', 'my-token');
    const auth = TestBed.inject(AuthService);
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');
    const logoutSpy = vi.spyOn(auth, 'logout');

    httpClient.get('/api/protected').subscribe({ error: () => {} });

    httpMock.expectOne('/api/protected').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(logoutSpy).toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith('/login');
  });

  it('does not log out on a 401 from an unauthenticated (no-token) request', () => {
    const auth = TestBed.inject(AuthService);
    const logoutSpy = vi.spyOn(auth, 'logout');

    httpClient.post('/api/auth/login', {}).subscribe({ error: () => {} });

    httpMock.expectOne('/api/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(logoutSpy).not.toHaveBeenCalled();
  });
});
