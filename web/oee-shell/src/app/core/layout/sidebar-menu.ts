export interface SidebarMenuItem {
  labelKey: string;
  icon: string;
  route: string;
  /** Roles allowed to see this item (UX-DR2 role/sidebar matrix from EXPERIENCE.md). */
  roles: ReadonlyArray<'Admin' | 'Manager' | 'Operator' | 'Viewer'>;
}

export const SIDEBAR_MENU: readonly SidebarMenuItem[] = [
  { labelKey: 'nav.dashboard', icon: 'pi pi-th-large', route: '/dashboard', roles: ['Admin', 'Manager', 'Operator', 'Viewer'] },
  { labelKey: 'nav.downtime', icon: 'pi pi-history', route: '/downtime', roles: ['Admin', 'Manager', 'Operator', 'Viewer'] },
  { labelKey: 'nav.reports', icon: 'pi pi-chart-bar', route: '/reports', roles: ['Admin', 'Manager', 'Viewer'] },
  { labelKey: 'nav.masterData', icon: 'pi pi-cog', route: '/master-data', roles: ['Admin'] },
];

export function getVisibleMenuItems(role: string | null): SidebarMenuItem[] {
  if (!role) {
    return [];
  }
  return SIDEBAR_MENU.filter((item) => (item.roles as readonly string[]).includes(role));
}
