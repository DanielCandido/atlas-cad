import { apiRequest } from '@/lib/server-api';

export async function GET(request: Request) {
  const upstream = await apiRequest('/api/v1/portal/machine-identifier', {}, true, request);
  const headers = new Headers();
  for (const name of ['content-type', 'content-length', 'content-disposition', 'accept-ranges']) {
    const value = upstream.headers.get(name);
    if (value) headers.set(name, value);
  }
  return new Response(upstream.body, { status: upstream.status, headers });
}
