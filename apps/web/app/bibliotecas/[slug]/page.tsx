import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound, permanentRedirect } from 'next/navigation';
import { Markdown } from '@/components/markdown';
import { ProductVideo } from '@/components/product-video';
import { getLibrary } from '@/lib/libraries';

export const dynamic = 'force-dynamic';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  try {
    const library = await getLibrary(slug === 'mlines' ? 'steelflow' : slug);
    return { title: library.name, description: library.summary };
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
    <div className="hm-page hm-library-detail">
      <section className="hm-hero hm-library-hero">
        <div className="hm-container hm-hero__grid">
          <div className="hm-hero__copy">
            <Link className="hm-back-link" href="/bibliotecas">
              Todas as bibliotecas
            </Link>
            <h1>{library.name}</h1>
            <p>{library.summary}</p>
            <div className="hm-actions">
              <Link className="hm-button hm-button--primary" href={`/checkout/${library.slug}`}>
                Assinar {library.name}
              </Link>
              <Link className="hm-button hm-button--quiet" href={`/documentacao/${library.slug}`}>
                Consultar documentação
              </Link>
            </div>
          </div>

          {library.slug === 'steelflow' ? (
            <ProductVideo />
          ) : (
            <div className="hm-library-mark" aria-label={`${library.name}, biblioteca ATLAS CAD`}>
              <span>{library.name}</span>
            </div>
          )}
        </div>
      </section>

      <section className="hm-section hm-library-about">
        <div className="hm-container hm-library-about__grid">
          <h2>O que o {library.name} resolve.</h2>
          <Markdown source={library.description} className="hm-library-description" />
        </div>
      </section>

      <section className="hm-section hm-capabilities">
        <div className="hm-container">
          <div className="hm-section-head hm-section-head--split">
            <h2>Trabalho técnico, direto no desenho.</h2>
            <p>Recursos disponíveis nesta biblioteca.</p>
          </div>
          <ul className="hm-capability-rows">
            {library.features.map((feature) => (
              <li key={feature}>
                <span>{feature}</span>
              </li>
            ))}
          </ul>
        </div>
      </section>

      <section className="hm-section hm-requirements">
        <div className="hm-container hm-requirements__grid">
          <div className="hm-section-head">
            <h2>Pronto para o seu ambiente.</h2>
            <p>Confira os requisitos antes de instalar.</p>
          </div>
          <div>
            <ul className="hm-requirement-list">
              {library.requirements.map((requirement) => (
                <li key={requirement}>
                  <svg viewBox="0 0 24 24" width="20" height="20" fill="none" aria-hidden="true">
                    <path
                      d="m6 12 4 4 8-9"
                      stroke="currentColor"
                      strokeWidth="1.8"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                  </svg>
                  <span>{requirement}</span>
                </li>
              ))}
            </ul>
            <Link href={`/documentacao/${library.slug}/instalacao`} className="hm-text-link">
              Consultar instalação completa
            </Link>
          </div>
        </div>
      </section>

      <section className="hm-close">
        <div className="hm-container hm-close__grid">
          <h2>Leve o {library.name} para o próximo desenho.</h2>
          <Link className="hm-button hm-button--paper" href={`/checkout/${library.slug}`}>
            Escolher um plano
          </Link>
        </div>
      </section>
    </div>
  );
}
