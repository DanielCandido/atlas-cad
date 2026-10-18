import { apiRequest } from '@/lib/server-api';

export async function GET(request: Request) {
  const upstream = await apiRequest('/api/v1/portal/installer', {}, true, request);
  return new Response(upstream.body, {
    status: upstream.status,
    headers: {
      'content-type': upstream.headers.get('content-type') || 'application/octet-stream',
      'content-disposition':
        upstream.headers.get('content-disposition') ||
        'attachment; filename="Instalador-AtlasCad.exe"',
    },
  });
}
