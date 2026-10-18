import type { Metadata } from 'next';
import { PairingClient } from './pairing-client';

export const metadata: Metadata = { title: 'Vincular computador' };

export default async function PairingPage({
  searchParams,
}: {
  searchParams: Promise<{ codigo?: string }>;
}) {
  const code = (await searchParams).codigo?.trim().toUpperCase() || '';
  return <PairingClient code={code} />;
}
