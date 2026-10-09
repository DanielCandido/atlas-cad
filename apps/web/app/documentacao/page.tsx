import Link from 'next/link';
import { docs } from '@/lib/content';
import { Eyebrow } from '@/components/ui';
import type { Metadata } from 'next';
export const metadata: Metadata = { title: 'Documentação' };
export default function Docs() {
  return (
    <>
      <Eyebrow>CENTRAL DE CONHECIMENTO</Eyebrow>
      <h1 className="docs-title">
        Documentação<span className="accent">.</span>
      </h1>
      <p className="docs-intro">
        Guias, referências e instruções de uso do ATLAS CAD. Uma base de conhecimento preparada para
        crescer com novas bibliotecas.
      </p>
      <div className="docs-search-note">⌕ &nbsp; Navegue pelos tópicos no menu lateral</div>
      <div className="doc-card-grid">
        {docs.map((d) => (
          <Link href={'/documentacao/' + d.slug} className="doc-card" key={d.slug}>
            <small>{d.group}</small>
            <h2>
              {d.title} <span>↗</span>
            </h2>
            <p>{d.summary}</p>
          </Link>
        ))}
      </div>
    </>
  );
}
