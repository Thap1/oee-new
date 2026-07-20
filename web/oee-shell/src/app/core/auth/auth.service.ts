import { HttpClient } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { JwtClaims, decodeJwtClaims } from './jwt.util';

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
}

const STORAGE_KEY = 'oee_access_token';

/**
 * Session state for the currently logged-in user. Claims are decoded client-side purely to drive
 * UI (sidebar-by-role, UX-DR2/3) — every API request still re-validates the token server-side, so
 * nothing here is an authorization boundary (NFR-5, see Story 1.6 Dev Notes).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly tokenSignal = signal<string | null>(this.readStoredToken());

  readonly claims = computed<JwtClaims | null>(() => {
    const token = this.tokenSignal();
    return token ? decodeJwtClaims(token) : null;
  });

  readonly isAuthenticated = computed(() => {
    const claims = this.claims();
    if (!claims) {
      return false;
    }
    // exp is in seconds since epoch (JWT standard); treat a stored-but-expired token as logged out
    // so the route guard sends the user back to /login instead of a shell that 401s on every call.
    return claims.exp === undefined || claims.exp * 1000 > Date.now();
  });
  readonly role = computed(() => this.claims()?.role ?? null);

  constructor(private readonly http: HttpClient) {
    // Keep tabs in sync: a logout (or token change) in another tab must not leave this tab
    // believing it's still authenticated until the next reload.
    if (typeof window !== 'undefined') {
      window.addEventListener('storage', (event: StorageEvent) => {
        if (event.key === STORAGE_KEY) {
          this.tokenSignal.set(event.newValue);
        }
      });
    }
  }

  get accessToken(): string | null {
    return this.tokenSignal();
  }

  async login(username: string, password: string): Promise<void> {
    const response = await firstValueFrom(this.http.post<LoginResponse>('/api/auth/login', { username, password }));
    this.tokenSignal.set(response.accessToken);
    localStorage.setItem(STORAGE_KEY, response.accessToken);
  }

  logout(): void {
    this.tokenSignal.set(null);
    localStorage.removeItem(STORAGE_KEY);
  }

  private readStoredToken(): string | null {
    if (typeof localStorage === 'undefined') {
      return null;
    }
    return localStorage.getItem(STORAGE_KEY);
  }
}
