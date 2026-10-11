import { apiRequest, forwardJson } from '@/lib/server-api';
export async function PUT(request: Request) {
  return forwardJson(await apiRequest('/api/v1/portal/plan', { method: 'PUT', body: await request.text() }, true, request));
}
