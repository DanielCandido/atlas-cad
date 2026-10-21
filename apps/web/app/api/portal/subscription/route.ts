import { apiRequest, forwardJson } from '@/lib/server-api';
export async function DELETE(request: Request) {
  const query = new URL(request.url).search;
  return forwardJson(
    await apiRequest('/api/v1/portal/subscription' + query, { method: 'DELETE' }, true, request),
  );
}
