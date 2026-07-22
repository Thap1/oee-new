import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/**
 * Denies navigation to a route unless the caller's role is in `allowedRoles` — scoped narrowly to
 * `/reports` (Story 4.1 AC #3), which explicitly requires screen-level denial for Operator, not just
 * a hidden sidebar link. `deferred-work.md`'s accepted "sidebar-only" gap for `/master-data` stands;
 * this guard doesn't generalize beyond what this story's AC demands.
 */
export function roleGuard(allowedRoles: string[]): CanActivateFn {
  return () => {
    const authService = inject(AuthService);
    const router = inject(Router);

    const role = authService.role();
    if (role && allowedRoles.includes(role)) {
      return true;
    }

    return router.parseUrl('/dashboard');
  };
}
