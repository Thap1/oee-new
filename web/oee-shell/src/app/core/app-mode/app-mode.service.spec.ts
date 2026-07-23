import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { beforeEach, afterEach, describe, it, expect } from 'vitest';
import { AppModeService } from './app-mode.service';

describe('AppModeService', () => {
  let service: AppModeService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AppModeService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('starts with no mode resolved yet', () => {
    expect(service.mode()).toBeNull();
    expect(service.isCentral()).toBe(false);
  });

  it('load() fetches the mode once and caches it', async () => {
    const firstLoad = service.load();
    httpMock.expectOne('/api/app-mode').flush({ mode: 'Central' });
    await firstLoad;

    expect(service.mode()).toBe('Central');
    expect(service.isCentral()).toBe(true);

    // A second load() must not issue another HTTP request — httpMock.verify() in afterEach
    // would fail if it did.
    await service.load();
  });

  it('isCentral() reflects Site mode as false', async () => {
    const loadPromise = service.load();
    httpMock.expectOne('/api/app-mode').flush({ mode: 'Site' });
    await loadPromise;

    expect(service.mode()).toBe('Site');
    expect(service.isCentral()).toBe(false);
  });
});
