import type { Metadata } from 'next';
import Link from 'next/link';
import { Eyebrow } from '@/components/ui';
import { getLibraries } from '@/lib/libraries';

export const metadata: Metadata = { title: 'Bibliotecas' };
export const dynamic = 'force-dynamic';
export default async function Libraries() {
  const libraries = await getLibraries();
  return (
    <section className="section page-top">
      <div className="container">
        <Eyebrow>ECOSSISTEMA ATLAS</Eyebrow>
        <h1 className="page-title">
          Bibliotecas para <span>produzir melhor.</span>
        </h1>
        <p className="page-intro">
          Cada biblioteca reúne ferramentas para uma etapa específica do seu fluxo de trabalho.
        </p>
        <div className="library-grid">
          {libraries.map((library, index) => (
            <Link href={`/bibliotecas/${library.slug}`} className="library-card" key={library.id}>
              <div className="library-art">
                <span className="library-monogram">
                  {library.name[0]}
                  <span>{library.name.slice(1).toUpperCase()}</span>
                </span>
                <span className="library-art-detail">
                  ATLAS / {String(index + 1).padStart(3, '0')}
                </span>
              </div>
              <div className="library-content">
                <span className="chip">BIBLIOTECA PUBLICADA</span>
                <h2>
                  {library.name} <span>→</span>
                </h2>
                <p>{library.summary}</p>
                <div className="library-bottom">
                  AutoCAD · Windows <span>Saiba mais →</span>
                </div>
              </div>
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}
