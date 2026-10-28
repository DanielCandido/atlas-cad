import type { Metadata } from 'next';
import Link from 'next/link';
import { getLibraries } from '@/lib/libraries';

export const metadata: Metadata = {
  title: 'Bibliotecas para AutoCAD',
  description: 'Explore as bibliotecas e ferramentas disponíveis no hub ATLAS CAD.',
};

export const dynamic = 'force-dynamic';

export default async function Libraries() {
  const libraries = await getLibraries();

  return (
    <div className="hm-page hm-library-index">
      <header className="hm-index-hero">
        <div className="hm-container">
          <h1>Bibliotecas para trabalhar melhor no AutoCAD.</h1>
          <p>Escolha uma etapa do projeto. O ATLAS CAD organiza o restante.</p>
        </div>
      </header>

      <section className="hm-section hm-library-list" aria-labelledby="library-list-title">
        <div className="hm-container">
          <div className="hm-section-head hm-section-head--split">
            <h2 id="library-list-title">Catálogo disponível.</h2>
            <p>Cada biblioteca tem documentação, compatibilidade e instalação próprias.</p>
          </div>

          {libraries.length === 0 ? (
            <div className="hm-empty">
              <div>
                <h3>Novas bibliotecas estão sendo preparadas.</h3>
                <p>Consulte a documentação para conhecer os fluxos já disponíveis.</p>
              </div>
              <Link className="hm-button hm-button--quiet" href="/documentacao">
                Consultar documentação
              </Link>
            </div>
          ) : (
            <div className="hm-library-rows">
              {libraries.map((library) => (
                <Link
                  href={`/bibliotecas/${library.slug}`}
                  className="hm-library-row"
                  key={library.id}
                >
                  <span className="hm-library-row__name">{library.name}</span>
                  <span className="hm-library-row__summary">{library.summary}</span>
                  <span className="hm-library-row__meta">
                    <span>AutoCAD para Windows</span>
                    <strong>Ver biblioteca</strong>
                  </span>
                </Link>
              ))}
            </div>
          )}
        </div>
      </section>

      <section className="hm-section hm-library-guide">
        <div className="hm-container hm-library-guide__grid">
          <h2>O que acompanha cada biblioteca.</h2>
          <dl>
            <div>
              <dt>Antes de instalar</dt>
              <dd>Compatibilidade, requisitos e recursos explicados no catálogo.</dd>
            </div>
            <div>
              <dt>Durante o uso</dt>
              <dd>Documentação organizada por biblioteca e comandos disponíveis.</dd>
            </div>
            <div>
              <dt>Na sua conta</dt>
              <dd>Assinaturas, cobranças, downloads e computadores no mesmo lugar.</dd>
            </div>
          </dl>
        </div>
      </section>

      <section className="hm-close hm-close--light">
        <div className="hm-container hm-close__grid">
          <h2>Precisa entender um comando primeiro?</h2>
          <Link className="hm-button hm-button--primary" href="/documentacao">
            Abrir a documentação
          </Link>
        </div>
      </section>
    </div>
  );
}
