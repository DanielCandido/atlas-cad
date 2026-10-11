import type { Metadata } from 'next';
import { PortalClient } from './portal-client';
export const metadata: Metadata = { title: 'Minha conta' };
export default function AccountPage() { return <PortalClient />; }
