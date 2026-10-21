import type { Metadata } from 'next';
import { Eyebrow } from '@/components/ui';
import { getLibrary } from '@/lib/libraries';
import { notFound } from 'next/navigation';
import { CheckoutForm } from '../checkout-form';

export const dynamic = 'force-dynamic';
export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  try {
    return { title: `Assinar ${(await getLibrary(slug)).name}` };
  } catch {
    return {};
  }
}
export default async function Checkout({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  let library;
  try {
    library = await getLibrary(slug);
  } catch {
    notFound();
  }
  return (
    <section className="section page-top">
      <div className="container checkout-grid">
        <div>
          <Eyebrow>ATLAS CAD / ASSINATURA</Eyebrow>
          <h1 className="page-title">
            Comece com <span>{library.name}.</span>
          </h1>
          <p className="page-intro">
            Crie ou acesse sua conta, escolha o plano e pague no ambiente seguro do Asaas.
          </p>
          <div className="notice">
            <strong>Compra segura</strong>
            <p>O ATLAS CAD não recebe nem armazena dados bancários.</p>
          </div>
        </div>
        <CheckoutForm librarySlug={library.slug} plans={library.plans} />
      </div>
    </section>
  );
}
