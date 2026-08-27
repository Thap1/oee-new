import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, of } from 'rxjs';
import { TranslateLoader, TranslationObject } from '@ngx-translate/core';

/**
 * Loads `/i18n/{lang}.json` via HttpClient. @ngx-translate/core v18 (signals-based) does not yet
 * ship an official HTTP loader package compatible with this API — this is a deliberately small
 * (few lines) replacement rather than pulling in a separate loader dependency of uncertain version
 * compatibility (FR-007 / UX-DR4).
 *
 * `Cache-Control: no-cache` forces revalidation on every load instead of trusting a stale cached
 * copy — a browser/CDN silently serving yesterday's `en.json` after a deploy that added new keys
 * renders those keys as their raw dotted string (ngx-translate's missing-key fallback), not an
 * error, so this bug is otherwise invisible until someone notices the literal key text on screen.
 */
@Injectable()
export class HttpTranslateLoader implements TranslateLoader {
  constructor(private readonly http: HttpClient) {}

  getTranslation(lang: string): Observable<TranslationObject> {
    return this.http
      .get<TranslationObject>(`/i18n/${lang}.json`, { headers: { 'Cache-Control': 'no-cache' } })
      .pipe(catchError(() => of({} as TranslationObject)));
  }
}
