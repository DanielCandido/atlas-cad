import { adminApiRequest, forwardJson } from '@/lib/server-api';

async function proxy(request: Request, context: { params: Promise<{ path?: string[] }> }) {
  const { path = [] } = await context.params;
  const suffix = path.length ? `/${path.map(encodeURIComponent).join('/')}` : '';
  const contentType = request.headers.get('content-type') || '';
  let body: BodyInit | undefined;
  if (!['GET', 'HEAD'].includes(request.method))
    body = contentType.includes('multipart/form-data')
      ? await request.formData()
      : await request.text();
  const response = await adminApiRequest(
    `/api/v1/admin/libraries${suffix}`,
    { method: request.method, body },
    request,
  );
  return forwardJson(response);
}
export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const DELETE = proxy;
