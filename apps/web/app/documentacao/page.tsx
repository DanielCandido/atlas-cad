import type { Metadata } from 'next';
import Link from 'next/link';
import { Eyebrow } from '@/components/ui';
import { getLibraries } from '@/lib/libraries';

export const metadata: Metadata = { title: 'Documentação' };
export const dynamic = 'force-dynamic';
export default async function Documentation() {
  const libraries = await getLibraries();
  return (
    <section className="section page-top">
      <div className="container">
        <Eyebrow>CENTRAL DE CONHECIMENTO</Eyebrow>
        <h1 className="docs-title">
          Documentação<span className="accent">.</span>
        </h1>
        <p className="docs-intro">Guias organizados por biblioteca.</p>
        <div className="doc-card-grid">
          {libraries.map((library) => (
            <Link href={`/documentacao/${library.slug}`} className="doc-card" key={library.id}>
              <small>BIBLIOTECA</small>
              <h2>
                {library.name}
                <span>→</span>
              </h2>
              <p>{library.summary}</p>
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}
