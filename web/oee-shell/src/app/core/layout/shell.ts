import { Component, computed } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { AuthService } from '../auth/auth.service';
import { getVisibleMenuItems } from './sidebar-menu';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, TranslatePipe, ButtonModule],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  readonly menuItems = computed(() => getVisibleMenuItems(this.auth.role()));
  readonly currentLang = computed(() => this.translate.currentLang() ?? this.translate.fallbackLang() ?? 'vi');

  constructor(
    private readonly auth: AuthService,
    private readonly translate: TranslateService,
  ) {}

  useLanguage(lang: 'vi' | 'en'): void {
    this.translate.use(lang);
  }

  logout(): void {
    this.auth.logout();
  }
}
