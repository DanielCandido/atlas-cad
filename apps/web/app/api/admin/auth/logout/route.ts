import { adminApiRequest, clearAdminSession, forwardJson } from '@/lib/server-api';

export async function POST(request: Request) {
  const response = await adminApiRequest('/api/v1/admin/auth/logout', { method: 'POST' }, request);
  await clearAdminSession();
  return forwardJson(response);
}
