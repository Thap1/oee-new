import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { fakeJwt } from '../../../testing/fake-jwt';
import { AuthService } from './auth.service';
import { roleGuard } from './role.guard';

describe('roleGuard', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
  });

  afterEach(() => localStorage.clear());

  it('allows navigation when the caller role is in the allowed list', () => {
    localStorage.setItem('oee_access_token', fakeJwt({ sub: '1', role: 'Manager' }));
    TestBed.inject(AuthService);

    const result = TestBed.runInInjectionContext(() => roleGuard(['Admin', 'Manager', 'Viewer'])({} as never, {} as never));

    expect(result).toBe(true);
  });

  it('redirects to /dashboard when the caller role (Operator) is not in the allowed list', () => {
    localStorage.setItem('oee_access_token', fakeJwt({ sub: '1', role: 'Operator' }));
    TestBed.inject(AuthService);

    const result = TestBed.runInInjectionContext(() => roleGuard(['Admin', 'Manager', 'Viewer'])({} as never, {} as never));

    const router = TestBed.inject(Router);
    expect(result).toEqual(router.parseUrl('/dashboard'));
  });

  it('redirects to /dashboard when there is no authenticated caller at all', () => {
    const result = TestBed.runInInjectionContext(() => roleGuard(['Admin', 'Manager', 'Viewer'])({} as never, {} as never));

    const router = TestBed.inject(Router);
    expect(result).toEqual(router.parseUrl('/dashboard'));
  });
});
