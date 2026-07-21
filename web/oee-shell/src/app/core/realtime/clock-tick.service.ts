import { DestroyRef, Injectable, inject, signal } from '@angular/core';

const TICK_INTERVAL_MS = 10_000;

/**
 * One shared ticking clock (Story 2.3) so no-signal detection across many Machine Status Cards
 * re-evaluates periodically without each card running its own `setInterval`.
 */
@Injectable({ providedIn: 'root' })
export class ClockTickService {
  readonly nowMs = signal(Date.now());

  constructor() {
    const intervalId = setInterval(() => this.nowMs.set(Date.now()), TICK_INTERVAL_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(intervalId));
  }
}
