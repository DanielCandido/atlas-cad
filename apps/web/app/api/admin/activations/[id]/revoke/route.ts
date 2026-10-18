import { adminApiRequest, forwardJson } from '@/lib/server-api';
export async function POST(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return forwardJson(
    await adminApiRequest(
      `/api/v1/admin/activations/${encodeURIComponent(id)}/revoke`,
      { method: 'POST', body: await request.text() },
      request,
    ),
  );
}
