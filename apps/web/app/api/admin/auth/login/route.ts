import { persistAdminSession } from '@/lib/server-api';

export async function POST(request: Request) {
  const base = process.env.ATLAS_LICENSE_API?.replace(/\/$/, '');
  if (!base) return Response.json({ message: 'API não configurada.' }, { status: 500 });
  const response = await fetch(`${base}/api/v1/admin/auth/login`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      accept: 'application/json',
      ...(request.headers.get('cf-connecting-ip')
        ? { 'cf-connecting-ip': request.headers.get('cf-connecting-ip')! }
        : {}),
    },
    body: await request.text(),
    cache: 'no-store',
  });
  const body = await response.text();
  if (!response.ok)
    return new Response(body, {
      status: response.status,
      headers: { 'content-type': 'application/json' },
    });
  const payload = JSON.parse(body) as {
    sessionToken: string;
    expiresAt: string;
    admin: { id: string; name: string; email: string };
  };
  await persistAdminSession(payload);
  return Response.json({ admin: payload.admin, expiresAt: payload.expiresAt });
}
