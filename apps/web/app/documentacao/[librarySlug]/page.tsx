import Link from 'next/link';
import { notFound, permanentRedirect } from 'next/navigation';
import { getLibrary } from '@/lib/libraries';

const legacyDocs = new Set([
  'visao-geral',
  'compra-e-conta',
  'instalacao',
  'licenciamento',
  'primeiro-uso',
  'solucao-de-problemas',
]);
export const dynamic = 'force-dynamic';
export default async function LibraryDocumentation({
  params,
}: {
  params: Promise<{ librarySlug: string }>;
}) {
  const { librarySlug } = await params;
  if (legacyDocs.has(librarySlug)) permanentRedirect(`/documentacao/steelflow/${librarySlug}`);
  let library;
  try {
    library = await getLibrary(librarySlug);
  } catch {
    notFound();
  }
  return (
    <>
      <h1 className="docs-title">
        {library.name}
        <span className="accent">.</span>
      </h1>
      <p className="docs-intro">Documentação da biblioteca {library.name}.</p>
      <div className="doc-card-grid">
        {library.docs.map((doc) => (
          <Link
            href={`/documentacao/${library.slug}/${doc.slug}`}
            className="doc-card"
            key={doc.id}
          >
            <small>{doc.group}</small>
            <h2>
              {doc.title}
              <span>→</span>
            </h2>
            <p>{doc.summary}</p>
          </Link>
        ))}
      </div>
    </>
  );
}
