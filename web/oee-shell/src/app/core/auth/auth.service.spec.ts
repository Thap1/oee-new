import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { beforeEach, afterEach, describe, it, expect } from 'vitest';
import { AuthService } from './auth.service';
import { fakeJwt } from '../../../testing/fake-jwt';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('starts unauthenticated when there is no stored token', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.role()).toBeNull();
  });

  it('login stores the token and exposes decoded claims', async () => {
    const token = fakeJwt({ sub: '1', role: 'Admin' });
    const loginPromise = service.login('admin', 'ChangeMe123!');

    const req = httpMock.expectOne('/api/auth/login');
    expect(req.request.method).toBe('POST');
    req.flush({ accessToken: token, expiresAtUtc: new Date().toISOString() });

    await loginPromise;

    expect(service.isAuthenticated()).toBe(true);
    expect(service.role()).toBe('Admin');
    expect(localStorage.getItem('oee_access_token')).toBe(token);
  });

  it('logout clears the token', async () => {
    const token = fakeJwt({ sub: '1', role: 'Admin' });
    const loginPromise = service.login('admin', 'ChangeMe123!');
    httpMock.expectOne('/api/auth/login').flush({ accessToken: token, expiresAtUtc: new Date().toISOString() });
    await loginPromise;

    service.logout();

    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('oee_access_token')).toBeNull();
  });
});
