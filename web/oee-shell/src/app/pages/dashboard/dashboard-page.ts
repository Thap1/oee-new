import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [TranslatePipe],
  template: `<h2>{{ 'nav.dashboard' | translate }}</h2><p>{{ 'placeholder.comingLater' | translate }}</p>`,
})
export class DashboardPage {}
