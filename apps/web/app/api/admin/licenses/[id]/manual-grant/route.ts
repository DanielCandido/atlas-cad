import { adminApiRequest, forwardJson } from '@/lib/server-api';
export async function PUT(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return forwardJson(
    await adminApiRequest(
      `/api/v1/admin/licenses/${encodeURIComponent(id)}/manual-grant`,
      { method: 'PUT', body: await request.text() },
      request,
    ),
  );
}
export async function DELETE(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return forwardJson(
    await adminApiRequest(
      `/api/v1/admin/licenses/${encodeURIComponent(id)}/manual-grant`,
      { method: 'DELETE', body: await request.text() },
      request,
    ),
  );
}
