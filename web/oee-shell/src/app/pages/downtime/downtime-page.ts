import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-downtime-page',
  standalone: true,
  imports: [TranslatePipe],
  template: `<h2>{{ 'nav.downtime' | translate }}</h2><p>{{ 'placeholder.comingLater' | translate }}</p>`,
})
export class DowntimePage {}
