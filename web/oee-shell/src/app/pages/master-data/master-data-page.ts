import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-master-data-page',
  standalone: true,
  imports: [TranslatePipe],
  template: `<h2>{{ 'nav.masterData' | translate }}</h2><p>{{ 'placeholder.comingLater' | translate }}</p>`,
})
export class MasterDataPage {}
