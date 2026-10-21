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
          <Eyebrow>ATLAS CAD / Checkout seguro</Eyebrow>
          <h1 className="page-title">
            Adicione <span>{library.name}</span> ao seu fluxo.
          </h1>
          <p className="page-intro">
            Entre com sua conta ou crie uma durante a compra. Depois, escolha o plano e conclua o
            pagamento no ambiente seguro do Asaas.
          </p>
          <div className="notice">
            <strong>Uma conta, todas as bibliotecas</strong>
            <p>
              Novas compras ficam reunidas no mesmo painel, com downloads, licenças e documentação
              centralizados.
            </p>
          </div>
          <div className="feature-card">
            <span className="eyebrow">O que acontece agora</span>
            <ol className="steps">
              <li>Identifique-se ou crie seu acesso.</li>
              <li>Escolha o plano e a forma de pagamento.</li>
              <li>Após a confirmação, o download será liberado em sua conta.</li>
            </ol>
          </div>
        </div>
        <CheckoutForm librarySlug={library.slug} plans={library.plans} />
      </div>
    </section>
  );
}
