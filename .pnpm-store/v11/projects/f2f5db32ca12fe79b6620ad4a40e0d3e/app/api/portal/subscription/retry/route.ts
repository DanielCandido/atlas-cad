import { apiRequest, forwardJson } from '@/lib/server-api';

export async function POST(request: Request) {
  return forwardJson(
    await apiRequest(
      '/api/v1/portal/subscription/retry',
      { method: 'POST', body: await request.text() },
      true,
      request,
    ),
  );
}
