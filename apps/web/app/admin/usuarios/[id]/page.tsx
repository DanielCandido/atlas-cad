import type { Metadata } from 'next';
import { AdminUserClient } from './user-client';
export const metadata: Metadata = { title: 'Gerenciar usuário' };
export default function AdminUserPage() {
  return <AdminUserClient />;
}
