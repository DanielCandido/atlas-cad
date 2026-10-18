import { adminApiRequest, forwardJson } from '@/lib/server-api';
export async function GET(request: Request) {
  const url = new URL(request.url);
  return forwardJson(await adminApiRequest(`/api/v1/admin/users${url.search}`, {}, request));
}
