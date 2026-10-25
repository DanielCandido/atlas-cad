import type { Metadata } from 'next';
import { StructuralReview } from '@/components/tools/structural-review';

export const metadata: Metadata = { title: 'Conferência Estrutural' };

export default function StructuralReviewPage() {
  return <StructuralReview />;
}
