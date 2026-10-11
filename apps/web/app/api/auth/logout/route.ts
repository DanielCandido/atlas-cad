import { clearSession } from '@/lib/server-api';
export async function POST() { await clearSession(); return Response.json({ ok: true }); }
