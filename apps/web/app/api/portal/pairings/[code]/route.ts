import { apiRequest, forwardJson } from '@/lib/server-api';

type Context = { params: Promise<{ code: string }> };

export async function GET(request: Request, context: Context) {
  const { code } = await context.params;
  return forwardJson(
    await apiRequest(`/api/v1/portal/pairings/${encodeURIComponent(code)}`, {}, true, request),
  );
}

export async function POST(request: Request, context: Context) {
  const { code } = await context.params;
  return forwardJson(
    await apiRequest(
      `/api/v1/portal/pairings/${encodeURIComponent(code)}/approve`,
      { method: 'POST', body: await request.text() },
      true,
      request,
    ),
  );
}

export async function DELETE(request: Request, context: Context) {
  const { code } = await context.params;
  return forwardJson(
    await apiRequest(
      `/api/v1/portal/pairings/${encodeURIComponent(code)}/reject`,
      { method: 'POST' },
      true,
      request,
    ),
  );
}
