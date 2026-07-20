import { Component, computed, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { AvatarModule } from 'primeng/avatar';
import { MenuModule } from 'primeng/menu';
import type { MenuItem } from 'primeng/api';
import { AuthService } from '../auth/auth.service';
import { getVisibleMenuItems } from './sidebar-menu';
import { LANG_STORAGE_KEY } from '../i18n/lang-storage';
import { SiteLineSelector } from './site-line-selector';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, TranslatePipe, ButtonModule, AvatarModule, MenuModule, SiteLineSelector],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  readonly menuItems = computed(() => getVisibleMenuItems(this.auth.role()));
  readonly currentLang = computed(() => this.translate.currentLang() ?? this.translate.fallbackLang() ?? 'vi');
  readonly sidebarCollapsed = signal(false);
  readonly username = computed(() => this.auth.claims()?.unique_name ?? '');
  readonly userInitial = computed(() => (this.username()[0] ?? '?').toUpperCase());

  readonly userMenuItems = computed<MenuItem[]>(() => {
    this.currentLang(); // re-evaluate translations when the language changes (AC #4)
    return [
      {
        label: this.translate.instant('nav.logout'),
        icon: 'pi pi-sign-out',
        automationId: 'logout-btn',
        command: () => this.logout(),
      },
    ];
  });

  constructor(
    private readonly auth: AuthService,
    private readonly translate: TranslateService,
  ) {}

  toggleSidebar(): void {
    this.sidebarCollapsed.update((collapsed) => !collapsed);
  }

  useLanguage(lang: 'vi' | 'en'): void {
    this.translate.use(lang);
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(LANG_STORAGE_KEY, lang);
    }
  }

  logout(): void {
    this.auth.logout();
  }
}
