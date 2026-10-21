import { apiRequest } from '@/lib/server-api';
export async function GET(request: Request, { params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const upstream = await apiRequest(
    `/api/v1/portal/libraries/${encodeURIComponent(slug)}/uninstaller`,
    {},
    true,
    request,
  );
  return new Response(upstream.body, {
    status: upstream.status,
    headers: {
      'content-type': upstream.headers.get('content-type') || 'application/octet-stream',
      'content-disposition':
        upstream.headers.get('content-disposition') ||
        `attachment; filename="Desinstalar-${slug}.exe"`,
    },
  });
}
