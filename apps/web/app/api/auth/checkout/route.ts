import { apiRequest, forwardJson, persistSession } from '@/lib/server-api';
export async function POST(request: Request) {
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
