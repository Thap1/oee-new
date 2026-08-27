import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { RunawayGuard } from './runaway-guard';

describe('RunawayGuard', () => {
  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it('allows calls up to the limit', () => {
    const guard = new RunawayGuard('test', 5);
    for (let i = 0; i < 5; i++) {
      expect(guard.check()).toBe(true);
    }
    expect(guard.hasTripped).toBe(false);
  });

  it('trips once the limit is exceeded within the window, and stays tripped', () => {
    const guard = new RunawayGuard('test', 5);
    for (let i = 0; i < 5; i++) {
      guard.check();
    }

    expect(guard.check()).toBe(false);
    expect(guard.hasTripped).toBe(true);
    expect(console.error).toHaveBeenCalledOnce();

    // Stays open even after the window would have rolled over — a tripped breaker is not self-healing,
    // because the underlying loop would immediately resume.
    vi.advanceTimersByTime(10_000);
    expect(guard.check()).toBe(false);
  });

  it('does not trip on sustained legitimate traffic spread across windows', () => {
    const guard = new RunawayGuard('test', 5, 1000);

    for (let window = 0; window < 10; window++) {
      for (let i = 0; i < 5; i++) {
        expect(guard.check()).toBe(true);
      }
      vi.advanceTimersByTime(1001);
    }

    expect(guard.hasTripped).toBe(false);
    expect(console.error).not.toHaveBeenCalled();
  });

  it('bounds an unbounded caller: a runaway loop is cut off after the limit', () => {
    const guard = new RunawayGuard('test', 200);
    let workDone = 0;

    // Simulates the incident: a caller that would otherwise re-enter forever.
    for (let i = 0; i < 1_000_000; i++) {
      if (!guard.check()) {
        break;
      }
      workDone += 1;
    }

    expect(workDone).toBe(200);
  });
});
