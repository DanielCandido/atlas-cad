import type { Metadata } from 'next';
import { QuantityDxf } from '@/components/tools/quantity-dxf';

export const metadata: Metadata = { title: 'Quantitativo DXF' };

export default function QuantityPage() {
  return <QuantityDxf />;
}
