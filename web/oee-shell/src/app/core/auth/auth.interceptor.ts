import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const API_URL_PREFIX = '/api';

/**
 * Attaches the JWT Bearer token to outgoing requests to `OeeNew.Api` only (Story 1.1 AC #2) — not
 * to third-party/static requests (e.g. i18n JSON assets) that happen to share the same origin.
 * Also handles a 401 on an authenticated request as "session expired": logs out and redirects to
 * login, instead of leaving the user in a shell that silently fails on every subsequent call.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const isApiRequest = req.url.startsWith(API_URL_PREFIX);
  const token = auth.accessToken;

  const outgoing =
    isApiRequest && token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(outgoing).pipe(
    catchError((error: unknown) => {
      if (isApiRequest && token && error instanceof HttpErrorResponse && error.status === 401) {
        auth.logout();
        router.navigateByUrl('/login');
      }
      return throwError(() => error);
    }),
  );
};
