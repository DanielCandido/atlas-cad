import type { Metadata } from 'next';
import { ToolFrame } from '@/components/tool-frame';

export const metadata: Metadata = { title: 'Conferência Estrutural' };

export default function StructuralReviewPage() {
  return (
    <ToolFrame
      src="/tools/conferencia-estrutural.html"
      title="Conferência Estrutural"
      description="Pré-dimensionamento e conferência de elementos estruturais em um fluxo técnico unificado."
    />
  );
}
