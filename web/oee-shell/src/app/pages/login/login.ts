import { HttpErrorResponse } from '@angular/common/http';
import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, TranslatePipe, ButtonModule, InputTextModule, PasswordModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  readonly username = signal('');
  readonly password = signal('');
  readonly errorKey = signal<string | null>(null);
  readonly isSubmitting = signal(false);

  constructor(
    private readonly auth: AuthService,
    private readonly router: Router,
    private readonly route: ActivatedRoute,
  ) {}

  async submit(): Promise<void> {
    this.errorKey.set(null);
    this.isSubmitting.set(true);
    try {
      await this.auth.login(this.username(), this.password());
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
      await this.router.navigateByUrl(returnUrl || '/dashboard');
    } catch (err) {
      // Only a 401 from the API means the credentials were wrong; anything else (network down,
      // 5xx, CORS) is a different failure and telling the user "wrong password" would be misleading.
      const isInvalidCredentials = err instanceof HttpErrorResponse && err.status === 401;
      this.errorKey.set(isInvalidCredentials ? 'login.invalidCredentials' : 'login.error');
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
