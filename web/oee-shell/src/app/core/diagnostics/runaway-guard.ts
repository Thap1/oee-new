/**
 * Circuit breaker for state updates that could re-enter unboundedly.
 *
 * Motivating incident (2026-08-27): `DashboardPage`'s SignalR `effect()` read `machinesSignal()`
 * and then wrote it back. In Angular a signal read inside an effect registers as a dependency, so
 * the write invalidated the effect and re-ran it immediately — an unbounded microtask loop that
 * allocated a fresh array/Set/timer on every pass and exhausted the browser tab's memory in about
 * a minute ("Aw, Snap! Out of Memory").
 *
 * Neither `try`/`catch` nor a timeout can contain that: the loop never throws, and it starves the
 * task queue so timers never fire. The only thing that stops it from inside is refusing to do the
 * work — once `check()` returns false the caller bails out without writing, the dependency stops
 * being invalidated, and the loop settles.
 *
 * This is a last-resort net, not a licence to leave such a loop in place: tripping it logs an
 * error naming the site so the real cause gets fixed (normally by wrapping the mutation in
 * `untracked()`).
 */
export class RunawayGuard {
  private windowStartMs = 0;
  private countInWindow = 0;
  private tripped = false;

  /**
   * @param label identifies the guarded site in the console error.
   * @param maxPerWindow calls tolerated per window before tripping. Set well above any legitimate
   *   burst — a whole fleet updating at once is normal; thousands per second is not.
   * @param windowMs sliding window length.
   */
  constructor(
    private readonly label: string,
    private readonly maxPerWindow = 200,
    private readonly windowMs = 1000,
  ) {}

  /** True while the caller may proceed. False once tripped — the caller MUST return without mutating state. */
  check(): boolean {
    if (this.tripped) {
      return false;
    }

    const nowMs = Date.now();
    if (nowMs - this.windowStartMs > this.windowMs) {
      this.windowStartMs = nowMs;
      this.countInWindow = 0;
    }

    this.countInWindow += 1;
    if (this.countInWindow > this.maxPerWindow) {
      this.tripped = true;
      console.error(
        `[RunawayGuard] "${this.label}" ran more than ${this.maxPerWindow} times within ${this.windowMs}ms — ` +
          `treating this as a runaway update loop and disabling further updates to keep the tab alive. ` +
          `The usual cause is an effect() that reads and writes the same signal; wrap the mutation in untracked().`,
      );
      return false;
    }

    return true;
  }

  /** True once the breaker has opened — lets a caller surface the condition in the UI. */
  get hasTripped(): boolean {
    return this.tripped;
  }
}
