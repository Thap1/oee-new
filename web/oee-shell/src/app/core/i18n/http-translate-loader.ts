import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { TranslateLoader, TranslationObject } from '@ngx-translate/core';

/**
 * Loads `/i18n/{lang}.json` via HttpClient. @ngx-translate/core v18 (signals-based) does not yet
 * ship an official HTTP loader package compatible with this API — this is a deliberately small
 * (few lines) replacement rather than pulling in a separate loader dependency of uncertain version
 * compatibility (FR-007 / UX-DR4).
 */
@Injectable()
export class HttpTranslateLoader implements TranslateLoader {
  constructor(private readonly http: HttpClient) {}

  getTranslation(lang: string): Observable<TranslationObject> {
    return this.http.get<TranslationObject>(`/i18n/${lang}.json`);
  }
}
