import { apiRequest, forwardJson } from '@/lib/server-api';
export async function DELETE(request: Request) { return forwardJson(await apiRequest('/api/v1/portal/subscription', { method: 'DELETE' }, true, request)); }
