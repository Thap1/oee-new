import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslateService } from '@ngx-translate/core';
import { SelectModule } from 'primeng/select';
import { ScopeService } from '../scope/scope.service';

/**
 * Topbar Site/Line selector (Story 1.6, AC #1-#3, FR-015, UX-DR3 progressive disclosure).
 * Renders nothing when the caller only has one Site — see `ScopeService.showSelector`.
 */
@Component({
  selector: 'app-site-line-selector',
  standalone: true,
  imports: [FormsModule, SelectModule],
  template: `
    @if (scope.showSelector()) {
      <div class="site-line-selector" data-testid="site-line-selector">
        <p-select
          [options]="scope.sites()"
          optionLabel="name"
          optionValue="id"
          [ngModel]="scope.selectedSiteId()"
          (ngModelChange)="scope.selectSite($event)"
          data-testid="site-selector-site"
        />
        @if (scope.lines().length > 0) {
          <p-select
            [options]="lineOptions()"
            optionLabel="name"
            optionValue="id"
            [ngModel]="scope.selectedLineId()"
            (ngModelChange)="scope.selectLine($event)"
            data-testid="site-selector-line"
          />
        }
      </div>
    }
  `,
  styles: [
    `
      .site-line-selector {
        display: flex;
        gap: 0.5rem;
      }
    `,
  ],
})
export class SiteLineSelector implements OnInit {
  constructor(
    protected readonly scope: ScopeService,
    private readonly translate: TranslateService,
  ) {}

  ngOnInit(): void {
    void this.scope.loadSites();
  }

  lineOptions() {
    return [{ id: null, name: this.translate.instant('masterData.allLines') }, ...this.scope.lines()];
  }
}
