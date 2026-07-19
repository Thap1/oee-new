import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { Shell } from './core/layout/shell';
import { Login } from './pages/login/login';
import { DashboardPage } from './pages/dashboard/dashboard-page';
import { DowntimePage } from './pages/downtime/downtime-page';
import { MasterDataPage } from './pages/master-data/master-data-page';
import { ReportsPage } from './pages/reports/reports-page';

export const routes: Routes = [
  { path: 'login', component: Login },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', component: DashboardPage },
      { path: 'downtime', component: DowntimePage },
      { path: 'reports', component: ReportsPage },
      { path: 'master-data', component: MasterDataPage },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
