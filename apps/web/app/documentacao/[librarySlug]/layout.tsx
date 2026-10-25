import Link from 'next/link';
import { notFound, permanentRedirect } from 'next/navigation';
import { getLibrary } from '@/lib/libraries';

export const dynamic = 'force-dynamic';
export default async function LibraryDocsLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ librarySlug: string }>;
}) {
  const { librarySlug } = await params;
  if (
    [
      'visao-geral',
      'compra-e-conta',
      'instalacao',
      'licenciamento',
      'primeiro-uso',
      'comandos',
      'solucao-de-problemas',
    ].includes(librarySlug)
  )
    permanentRedirect(`/documentacao/steelflow/${librarySlug}`);
  let library;
  try {
    library = await getLibrary(librarySlug);
  } catch {
    notFound();
  }
  const groups = [...new Set(library.docs.map((doc) => doc.group))];
  return (
    <section className="docs-shell container">
      <aside className="docs-sidebar">
        <Link className="docs-side-title" href={`/bibliotecas/${library.slug}`}>
          ATLAS / {library.name.toUpperCase()}
        </Link>
        {groups.map((group) => (
          <div className="docs-group" key={group}>
            <div className="docs-group-title">{group}</div>
            {library.docs
              .filter((doc) => doc.group === group)
              .map((doc) => (
                <Link key={doc.slug} href={`/documentacao/${library.slug}/${doc.slug}`}>
                  {doc.title}
                </Link>
              ))}
          </div>
        ))}
      </aside>
      <div className="docs-content">{children}</div>
    </section>
  );
}
