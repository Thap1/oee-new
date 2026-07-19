import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-reports-page',
  standalone: true,
  imports: [TranslatePipe],
  template: `<h2>{{ 'nav.reports' | translate }}</h2><p>{{ 'placeholder.comingLater' | translate }}</p>`,
})
export class ReportsPage {}
