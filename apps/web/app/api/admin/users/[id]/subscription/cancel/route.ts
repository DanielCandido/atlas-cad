import { adminApiRequest, forwardJson } from '@/lib/server-api';
export async function POST(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const query = new URL(request.url).search;
  return forwardJson(
    await adminApiRequest(
      `/api/v1/admin/users/${encodeURIComponent(id)}/subscription/cancel${query}`,
      { method: 'POST', body: await request.text() },
      request,
    ),
  );
}
