import Link from 'next/link';
import { notFound } from 'next/navigation';
import { Markdown } from '@/components/markdown';
import { getLibrary } from '@/lib/libraries';

export const dynamic = 'force-dynamic';
export default async function DocumentationPage({
  params,
}: {
  params: Promise<{ librarySlug: string; docSlug: string }>;
}) {
  const { librarySlug, docSlug } = await params;
  let library;
  try {
    library = await getLibrary(librarySlug);
  } catch {
    notFound();
  }
  const document = library.docs.find((doc) => doc.slug === docSlug);
  if (!document) notFound();
  return (
    <article className="doc-article">
      <div className="breadcrumbs">
        <Link href="/documentacao">Documentação</Link>
        <span>/</span>
        <Link href={`/documentacao/${library.slug}`}>{library.name}</Link>
        <span>/</span>
        {document.group}
      </div>
      <h1>{document.title}</h1>
      <p className="docs-intro">{document.summary}</p>
      <Markdown source={document.markdown} />
      <div className="doc-next">
        <Link href={`/documentacao/${library.slug}`}>← Voltar</Link>
        <Link href={`/checkout/${library.slug}`}>Assinar {library.name} →</Link>
      </div>
    </article>
  );
}
