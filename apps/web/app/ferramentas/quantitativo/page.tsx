import type { Metadata } from 'next';
import { ToolFrame } from '@/components/tool-frame';

export const metadata: Metadata = { title: 'Quantitativo DXF' };

export default function QuantityPage() {
  return (
    <ToolFrame
      src="/tools/quantitativo.html"
      title="Quantitativo DXF"
      description="Extraia quantitativos de concreto, formas e complementares diretamente das pranchas estruturais."
    />
  );
}
