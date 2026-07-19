import { describe, it, expect } from 'vitest';
import { decodeJwtClaims } from './jwt.util';
import { fakeJwt } from '../../../testing/fake-jwt';

describe('decodeJwtClaims', () => {
  it('returns null for a malformed token', () => {
    expect(decodeJwtClaims('not-a-jwt')).toBeNull();
  });

  it('decodes role, siteId and lineId claims', () => {
    const token = fakeJwt({
      sub: 'user-1',
      unique_name: 'operator1',
      role: 'Operator',
      site_id: 'site-1',
      line_id: ['line-1', 'line-2'],
      exp: 9999999999,
    });

    const claims = decodeJwtClaims(token);

    expect(claims?.role).toBe('Operator');
    expect(claims?.site_id).toEqual(['site-1']);
    expect(claims?.line_id).toEqual(['line-1', 'line-2']);
  });

  it('decodes an Admin token with no site/line claims', () => {
    const token = fakeJwt({ sub: 'admin-1', unique_name: 'admin', role: 'Admin' });

    const claims = decodeJwtClaims(token);

    expect(claims?.role).toBe('Admin');
    expect(claims?.site_id).toBeUndefined();
    expect(claims?.line_id).toBeUndefined();
  });
});
