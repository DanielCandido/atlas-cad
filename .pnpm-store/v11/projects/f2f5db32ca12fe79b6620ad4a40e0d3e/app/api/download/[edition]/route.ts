import { apiRequest } from '@/lib/server-api';

const editions = new Set([
  'autocad-2022-2024',
  'autocad-2025-2026-1-1',
  'autocad-2026-1-2-plus',
]);

export async function GET(request: Request, context: { params: Promise<{ edition: string }> }) {
  const { edition } = await context.params;
  if (!editions.has(edition)) return new Response('Versão inválida.', { status: 404 });
  const upstream = await apiRequest(`/api/v1/portal/download/${edition}`, {}, true, request);
  const headers = new Headers();
  for (const name of ['content-type', 'content-length', 'content-disposition', 'accept-ranges']) {
    const value = upstream.headers.get(name);
    if (value) headers.set(name, value);
  }
  return new Response(upstream.body, { status: upstream.status, headers });
}
