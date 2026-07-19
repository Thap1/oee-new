/**
 * Client-side JWT payload decoding — for UI display only (e.g. which sidebar items to show).
 * Never trust this for authorization: the API always re-validates the token server-side (NFR-5).
 */
export interface JwtClaims {
  sub?: string;
  unique_name?: string;
  role?: string;
  site_id?: string[];
  line_id?: string[];
  exp?: number;
}

export function decodeJwtClaims(token: string): JwtClaims | null {
  const parts = token.split('.');
  if (parts.length !== 3) {
    return null;
  }

  try {
    const payload = base64UrlDecode(parts[1]);
    const parsed = JSON.parse(payload) as Record<string, unknown>;

    const toArray = (value: unknown): string[] | undefined =>
      value === undefined ? undefined : Array.isArray(value) ? (value as string[]) : [value as string];

    return {
      sub: parsed['sub'] as string | undefined,
      unique_name: parsed['unique_name'] as string | undefined,
      role: parsed['role'] as string | undefined,
      site_id: toArray(parsed['site_id']),
      line_id: toArray(parsed['line_id']),
      exp: parsed['exp'] as number | undefined,
    };
  } catch {
    return null;
  }
}

function base64UrlDecode(input: string): string {
  const base64 = input.replace(/-/g, '+').replace(/_/g, '/').padEnd(input.length + ((4 - (input.length % 4)) % 4), '=');
  return decodeURIComponent(
    atob(base64)
      .split('')
      .map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
      .join(''),
  );
}
