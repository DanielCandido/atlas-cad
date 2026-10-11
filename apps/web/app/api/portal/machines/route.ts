import { apiRequest, forwardJson } from '@/lib/server-api';
export async function POST(request: Request) {
  return forwardJson(await apiRequest('/api/v1/portal/machines', { method: 'POST', body: await request.text() }, true, request));
}
export async function DELETE(request: Request) { return forwardJson(await apiRequest('/api/v1/portal/machines', { method: 'DELETE' }, true, request)); }
