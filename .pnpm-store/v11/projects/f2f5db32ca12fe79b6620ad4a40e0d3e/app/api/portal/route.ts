import { apiRequest, forwardJson } from '@/lib/server-api';
export async function GET(request: Request) { return forwardJson(await apiRequest('/api/v1/portal', {}, true, request)); }
