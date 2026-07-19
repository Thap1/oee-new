/** Builds an unsigned JWT-shaped string for tests — the signature is never verified client-side. */
export function fakeJwt(payload: Record<string, unknown>): string {
  const base64UrlEncode = (obj: unknown) =>
    btoa(JSON.stringify(obj)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${base64UrlEncode({ alg: 'RS256' })}.${base64UrlEncode(payload)}.sig`;
}
