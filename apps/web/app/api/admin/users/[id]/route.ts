import { adminApiRequest, forwardJson } from '@/lib/server-api';
export async function GET(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return forwardJson(
    await adminApiRequest(`/api/v1/admin/users/${encodeURIComponent(id)}`, {}, request),
  );
}
