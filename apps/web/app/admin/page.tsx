import type { Metadata } from 'next';
import { AdminUsersClient } from './users-client';
export const metadata: Metadata = { title: 'Gerenciador' };
export default function AdminPage() {
  return <AdminUsersClient />;
}
