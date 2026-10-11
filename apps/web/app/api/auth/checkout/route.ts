import { apiRequest, forwardJson, persistSession } from '@/lib/server-api';
import { cookies } from 'next/headers';
import { NextResponse } from 'next/server';

export async function POST(request: Request) {
  if ((await cookies()).has('atlas_session')) {
    return NextResponse.json(
      {
        errorCode: 'already_authenticated',
        message: 'Você já está autenticado. Gere uma nova cobrança pela Área do Cliente.',
      },
      { status: 409 },
    );
  }
  const response = await apiRequest(
    '/api/v1/commerce/checkout',
    { method: 'POST', body: await request.text() },
    false,
    request,
  );
  const body = await response.text();
  if (response.ok) await persistSession(JSON.parse(body));
  return forwardJson(new Response(body, { status: response.status, headers: response.headers }));
}
