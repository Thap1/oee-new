export const LANG_STORAGE_KEY = 'oee_lang';

/** Reads the user's persisted language choice, falling back to 'vi' (FR-007/UX-DR4). */
export function readStoredLang(): 'vi' | 'en' {
  if (typeof localStorage === 'undefined') {
    return 'vi';
  }
  const stored = localStorage.getItem(LANG_STORAGE_KEY);
  return stored === 'en' ? 'en' : 'vi';
}
