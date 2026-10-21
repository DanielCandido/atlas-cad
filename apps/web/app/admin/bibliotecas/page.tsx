import type { Metadata } from 'next';
import { AdminLibrariesClient } from './libraries-client';
export const metadata: Metadata = { title: 'Bibliotecas | Gerenciador' };
export default function AdminLibraries() {
  return <AdminLibrariesClient />;
}
