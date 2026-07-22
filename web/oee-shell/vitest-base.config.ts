import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    pool: 'forks',
    fileParallelism: false,
    testTimeout: 10_000,
    hookTimeout: 10_000,
    teardownTimeout: 10_000,
  },
});
