import { describe, it, expect } from 'vitest';
import { getVisibleMenuItems } from './sidebar-menu';

describe('getVisibleMenuItems', () => {
  it('returns empty array when role is null (not logged in)', () => {
    expect(getVisibleMenuItems(null)).toEqual([]);
  });

  it('shows Dashboard, Downtime, Reports but not Master Data for Operator', () => {
    const routes = getVisibleMenuItems('Operator').map((i) => i.route);
    expect(routes).toContain('/dashboard');
    expect(routes).toContain('/downtime');
    expect(routes).not.toContain('/reports');
    expect(routes).not.toContain('/master-data');
  });

  it('shows Dashboard, Downtime, Reports but not Master Data for Manager', () => {
    const routes = getVisibleMenuItems('Manager').map((i) => i.route);
    expect(routes).toEqual(['/dashboard', '/downtime', '/reports']);
  });

  it('shows Dashboard, Downtime, Reports but not Master Data for Viewer', () => {
    const routes = getVisibleMenuItems('Viewer').map((i) => i.route);
    expect(routes).toEqual(['/dashboard', '/downtime', '/reports']);
  });

  it('shows all four items for Admin', () => {
    const routes = getVisibleMenuItems('Admin').map((i) => i.route);
    expect(routes).toEqual(['/dashboard', '/downtime', '/reports', '/master-data']);
  });
});
