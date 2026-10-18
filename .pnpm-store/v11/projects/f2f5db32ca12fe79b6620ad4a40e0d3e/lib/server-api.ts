import { cookies } from 'next/headers';
import { NextResponse } from 'next/server';

const SESSION_COOKIE = 'atlas_session';
const ADMIN_SESSION_COOKIE = 'atlas_admin_session';

function apiBase() {
  const value = process.env.ATLAS_LICENSE_API;
  if (!value) throw new Error('ATLAS_LICENSE_API não configurada no servidor web.');
  return value.replace(/\/$/, '');
}

export async function apiRequest(
  path: string,
  init: RequestInit = {},
  authenticated = false,
  incoming?: Request,
) {
  const headers = new Headers(init.headers);
  headers.set('accept', 'application/json');
  if (init.body) headers.set('content-type', 'application/json');
  const cloudflareIp = incoming?.headers.get('cf-connecting-ip');
  if (cloudflareIp) headers.set('cf-connecting-ip', cloudflareIp);
  if (authenticated) {
    const token = (await cookies()).get(SESSION_COOKIE)?.value;
    if (!token)
      return new Response(
        JSON.stringify({ errorCode: 'authentication_required', message: 'Entre na sua conta.' }),
        { status: 401 },
      );
    headers.set('authorization', `Bearer ${token}`);
  }
  return fetch(`${apiBase()}${path}`, { ...init, headers, cache: 'no-store' });
}

export async function forwardJson(response: Response) {
  if (response.status === 204) return new NextResponse(null, { status: 204 });
  const body = await response.text();
  return new NextResponse(body, {
    status: response.status,
    headers: { 'content-type': response.headers.get('content-type') || 'application/json' },
  });
}

export async function persistSession(payload: { sessionToken: string; expiresAt: string }) {
  (await cookies()).set(SESSION_COOKIE, payload.sessionToken, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    path: '/',
    expires: new Date(payload.expiresAt),
  });
}

export async function clearSession() {
  (await cookies()).delete(SESSION_COOKIE);
}

export async function adminApiRequest(path: string, init: RequestInit = {}, incoming?: Request) {
  const headers = new Headers(init.headers);
  headers.set('accept', 'application/json');
  if (init.body) headers.set('content-type', 'application/json');
  const cloudflareIp = incoming?.headers.get('cf-connecting-ip');
  if (cloudflareIp) headers.set('cf-connecting-ip', cloudflareIp);
  const token = (await cookies()).get(ADMIN_SESSION_COOKIE)?.value;
  if (!token)
    return new Response(
      JSON.stringify({
        errorCode: 'admin_authentication_required',
        message: 'Entre como administrador.',
      }),
      { status: 401 },
    );
  headers.set('authorization', `Bearer ${token}`);
  return fetch(`${apiBase()}${path}`, { ...init, headers, cache: 'no-store' });
}

export async function persistAdminSession(payload: { sessionToken: string; expiresAt: string }) {
  (await cookies()).set(ADMIN_SESSION_COOKIE, payload.sessionToken, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'strict',
    path: '/',
    expires: new Date(payload.expiresAt),
  });
}

export async function clearAdminSession() {
  (await cookies()).delete(ADMIN_SESSION_COOKIE);
}
