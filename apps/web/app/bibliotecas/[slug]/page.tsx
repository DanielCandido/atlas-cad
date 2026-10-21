import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound, permanentRedirect } from 'next/navigation';
import { ArrowLink, CadVisual, Eyebrow } from '@/components/ui';
import { Markdown } from '@/components/markdown';
import { getLibrary } from '@/lib/libraries';

export const dynamic = 'force-dynamic';
export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  try {
    return { title: (await getLibrary(slug === 'mlines' ? 'steelflow' : slug)).name };
  } catch {
    return {};
  }
}
export default async function LibraryPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  if (slug === 'mlines') permanentRedirect('/bibliotecas/steelflow');
  let library;
  try {
    library = await getLibrary(slug);
  } catch {
    notFound();
  }
  return (
    <>
      <section className="section page-top">
        <div className="container product-hero">
          <div>
            <Eyebrow>BIBLIOTECA / ATLAS CAD</Eyebrow>
            <h1 className="page-title">
              {library.name}
              <span className="accent">.</span>
            </h1>
            <div className="page-intro rich-intro">
              <Markdown source={library.description} />
            </div>
            <div className="hero-actions">
              <ArrowLink href={`/checkout/${library.slug}`}>Assinar agora</ArrowLink>
              <ArrowLink href={`/documentacao/${library.slug}`} secondary>
                Documentação
              </ArrowLink>
            </div>
          </div>
          <CadVisual />
        </div>
      </section>
      <section className="section section-dark">
        <div className="container">
          <div className="section-heading">
            <Eyebrow>FUNCIONALIDADES</Eyebrow>
            <h2>Produza melhor.</h2>
          </div>
          <div className="capability-grid">
            {library.features.map((feature, index) => (
              <div className="capability" key={feature}>
                <span>{String(index + 1).padStart(2, '0')}</span>
                <h3>{feature}</h3>
              </div>
            ))}
          </div>
        </div>
      </section>
      <section className="section">
        <div className="container split">
          <div>
            <Eyebrow>COMPATIBILIDADE</Eyebrow>
            <h2>Projetado para o seu ambiente.</h2>
          </div>
          <div className="info-panel">
            <h3>Requisitos</h3>
            {library.requirements.map((requirement) => (
              <div className="requirement" key={requirement}>
                <span>✓</span>
                <p>{requirement}</p>
              </div>
            ))}
            <Link href={`/documentacao/${library.slug}/instalacao`} className="text-link">
              Ver instalação completa →
            </Link>
          </div>
        </div>
      </section>
    </>
  );
}
