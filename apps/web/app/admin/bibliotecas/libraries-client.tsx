'use client';

import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Markdown, normalizeMarkdown } from '@/components/markdown';

type Plan = {
  id: string;
  code: string;
  name: string;
  price: number;
  cycle: string;
  active: boolean;
};
type Doc = {
  id: string;
  slug: string;
  title: string;
  summary: string;
  group: string;
  position: number;
  markdown: string;
  status: string;
};
type Release = {
  id: string;
  version: string;
  sha256: string;
  sizeBytes: number;
  isActive: boolean;
};
type Library = {
  id: string;
  slug: string;
  name: string;
  productId: string;
  moduleCode: string;
  catalogId?: string;
  summary: string;
  description: string;
  features: string[];
  requirements: string[];
  status: string;
  plans: Plan[];
  docs: Doc[];
  releases: Release[];
};
type Section = 'metadata' | 'packages' | 'docs';

function normalizeLibrary(library: Library): Library {
  return {
    ...library,
    description: normalizeMarkdown(library.description),
    features: (library.features || []).map(normalizeMarkdown),
    requirements: (library.requirements || []).map(normalizeMarkdown),
    plans: library.plans || [],
    docs: (library.docs || []).map((doc) => ({
      ...doc,
      markdown: normalizeMarkdown(doc.markdown),
    })),
    releases: library.releases || [],
  };
}

async function api(path: string, method = 'GET', body?: unknown) {
  const response = await fetch(`/api/admin/libraries${path}`, {
    method,
    headers:
      body && !(body instanceof FormData) ? { 'content-type': 'application/json' } : undefined,
    body: body instanceof FormData ? body : body ? JSON.stringify(body) : undefined,
  });
  const result = await response.json();
  if (!response.ok)
    throw new Error(result.message || `Operação não concluída (${response.status}).`);
  return result as Library | Library[];
}

function formValues(form: HTMLFormElement) {
  return Object.fromEntries(new FormData(form));
}

export function AdminLibrariesClient() {
  const router = useRouter();
  const [items, setItems] = useState<Library[]>([]);
  const [selected, setSelected] = useState<Library>();
  const [section, setSection] = useState<Section>('metadata');
  const [editingDoc, setEditingDoc] = useState<Doc>();
  const [description, setDescription] = useState('');
  const [docMarkdown, setDocMarkdown] = useState('');
  const [query, setQuery] = useState('');
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [busy, setBusy] = useState(false);

  const load = useCallback(
    async (preferredId?: string) => {
      try {
        const list = ((await api('')) as Library[]).map(normalizeLibrary);
        setItems(list);
        setSelected((current) => {
          const next = list.find((item) => item.id === (preferredId || current?.id)) || list[0];
          setDescription(next?.description || '');
          return next;
        });
      } catch (reason) {
        const message = reason instanceof Error ? reason.message : String(reason);
        if (/401|não autorizad/i.test(message)) router.replace('/admin/entrar');
        else setError(message);
      }
    },
    [router],
  );

  useEffect(() => {
    void load();
  }, [load]);

  async function run<T>(action: () => Promise<T>, message: string) {
    setBusy(true);
    setError('');
    setSuccess('');
    try {
      const result = await action();
      setSuccess(message);
      return result;
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
      return undefined;
    } finally {
      setBusy(false);
    }
  }

  function chooseLibrary(library?: Library) {
    setSelected(library);
    setDescription(library?.description || '');
    setEditingDoc(undefined);
    setDocMarkdown('');
    setSection('metadata');
    setError('');
    setSuccess('');
  }

  async function saveLibrary(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = formValues(event.currentTarget);
    const payload = {
      ...data,
      description: normalizeMarkdown(String(data.description || '')),
      features: normalizeMarkdown(String(data.features || ''))
        .split('\n')
        .map((item) => item.trim())
        .filter(Boolean),
      requirements: normalizeMarkdown(String(data.requirements || ''))
        .split('\n')
        .map((item) => item.trim())
        .filter(Boolean),
    };
    const saved = await run(
      () => api(selected ? `/${selected.id}` : '', selected ? 'PUT' : 'POST', payload),
      selected
        ? 'Biblioteca atualizada.'
        : 'Biblioteca criada. Agora você pode adicionar planos, documentação e versões.',
    );
    if (saved && !Array.isArray(saved)) await load(saved.id);
  }

  async function savePlan(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected) return;
    const form = event.currentTarget;
    const data = formValues(form);
    const saved = await run(
      () =>
        api(`/${selected.id}/plans`, 'PUT', { ...data, price: Number(data.price), active: true }),
      'Plano salvo.',
    );
    if (saved) {
      form.reset();
      await load(selected.id);
    }
  }

  async function saveDoc(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected) return;
    const data = formValues(event.currentTarget);
    const saved = await run(
      () =>
        api(`/${selected.id}/docs`, 'PUT', {
          ...data,
          markdown: normalizeMarkdown(docMarkdown),
          position: Number(data.position),
        }),
      editingDoc ? 'Página atualizada.' : 'Página adicionada à documentação.',
    );
    if (saved) {
      setEditingDoc(undefined);
      setDocMarkdown('');
      await load(selected.id);
    }
  }

  async function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected) return;
    const form = event.currentTarget;
    const saved = await run(async () => {
      const response = await fetch(`/api/admin/libraries/${selected.id}/releases`, {
        method: 'POST',
        body: new FormData(form),
      });
      const result = await response.json();
      if (!response.ok) throw new Error(result.message || 'Upload não concluído.');
      return result;
    }, 'Pacote enviado com sucesso.');
    if (saved) {
      form.reset();
      await load(selected.id);
    }
  }

  const filtered = useMemo(() => {
    const term = query.trim().toLocaleLowerCase('pt-BR');
    return term
      ? items.filter((item) =>
          `${item.name} ${item.slug} ${item.moduleCode}`.toLocaleLowerCase('pt-BR').includes(term),
        )
      : items;
  }, [items, query]);
  const published = items.filter((item) => item.status === 'published').length;
  const draft = items.length - published;

  return (
    <section className="admin-library-page">
      <header className="admin-library-hero">
        <div>
          <span className="admin-breadcrumb">ADMIN CONSOLE / CATÁLOGO DE BIBLIOTECAS</span>
          <h1>Bibliotecas &amp; versões</h1>
          <p>Cadastre produtos, documentação em Markdown, planos comerciais e instaladores.</p>
        </div>
        <button type="button" className="button button-primary" onClick={() => chooseLibrary()}>
          <span aria-hidden="true">＋</span> Nova biblioteca
        </button>
      </header>

      <div className="library-kpis" aria-label="Resumo do catálogo">
        <div>
          <span>Total</span>
          <strong>{items.length}</strong>
          <small>bibliotecas cadastradas</small>
        </div>
        <div>
          <span>Publicadas</span>
          <strong>{published}</strong>
          <small>disponíveis no site</small>
        </div>
        <div>
          <span>Em preparação</span>
          <strong>{draft}</strong>
          <small>rascunhos ou arquivadas</small>
        </div>
        <div>
          <span>Versões</span>
          <strong>{items.reduce((total, item) => total + item.releases.length, 0)}</strong>
          <small>pacotes registrados</small>
        </div>
      </div>

      {error && (
        <div className="admin-feedback error" role="alert">
          {error}
        </div>
      )}
      {success && (
        <div className="admin-feedback success" role="status">
          {success}
        </div>
      )}

      <div className="library-manager">
        <aside className="library-catalog">
          <div className="library-catalog-heading">
            <div>
              <span>Repositório</span>
              <strong>Catálogo</strong>
            </div>
            <span>{filtered.length}</span>
          </div>
          <label className="library-search">
            <span>⌕</span>
            <input
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Buscar por nome, slug ou módulo"
            />
          </label>
          <div className="library-catalog-list">
            {filtered.map((library) => {
              const activeRelease = library.releases.find((release) => release.isActive);
              return (
                <button
                  type="button"
                  key={library.id}
                  className={selected?.id === library.id ? 'active' : ''}
                  onClick={() => chooseLibrary(library)}
                >
                  <span className={`catalog-status ${library.status}`} />
                  <span className="catalog-copy">
                    <strong>{library.name}</strong>
                    <small>
                      {library.moduleCode} · /{library.slug}
                    </small>
                  </span>
                  <span className="catalog-meta">
                    <small>{library.status === 'published' ? 'PUBLICADA' : 'RASCUNHO'}</small>
                    <b>{activeRelease ? `v${activeRelease.version}` : 'sem versão'}</b>
                  </span>
                </button>
              );
            })}
            {!filtered.length && <p className="library-empty">Nenhuma biblioteca encontrada.</p>}
          </div>
        </aside>

        <main className="library-editor">
          <div className="library-editor-head">
            <div className="library-avatar">
              {selected?.name?.slice(0, 2).toUpperCase() || 'NB'}
            </div>
            <div>
              <span>{selected ? 'EDITANDO BIBLIOTECA' : 'NOVO PRODUTO'}</span>
              <h2>{selected?.name || 'Nova biblioteca'}</h2>
              <small>
                {selected
                  ? `UUID: ${selected.id}`
                  : 'Preencha os metadados para iniciar o cadastro.'}
              </small>
            </div>
          </div>

          <nav className="library-editor-tabs" aria-label="Etapas da biblioteca">
            <EditorTab
              active={section === 'metadata'}
              onClick={() => setSection('metadata')}
              number="1"
              label="Metadados & conteúdo"
            />
            <EditorTab
              active={section === 'packages'}
              disabled={!selected}
              onClick={() => setSection('packages')}
              number="2"
              label="Planos & pacotes"
            />
            <EditorTab
              active={section === 'docs'}
              disabled={!selected}
              onClick={() => setSection('docs')}
              number="3"
              label="Documentação"
            />
          </nav>

          {section === 'metadata' && (
            <MetadataForm
              key={selected?.id || 'new'}
              selected={selected}
              description={description}
              onDescription={setDescription}
              busy={busy}
              onSubmit={saveLibrary}
            />
          )}
          {section === 'packages' && selected && (
            <PackagesPanel
              selected={selected}
              busy={busy}
              onPlan={savePlan}
              onUpload={upload}
              onPublish={async (releaseId) => {
                const result = await run(
                  () => api(`/${selected.id}/publish/${releaseId}`, 'POST'),
                  'Versão publicada.',
                );
                if (result) await load(selected.id);
              }}
              onArchive={async () => {
                if (!window.confirm('Arquivar esta biblioteca e removê-la do catálogo público?'))
                  return;
                const result = await run(
                  () => api(`/${selected.id}/archive`, 'POST'),
                  'Biblioteca arquivada.',
                );
                if (result) await load(selected.id);
              }}
            />
          )}
          {section === 'docs' && selected && (
            <DocsPanel
              key={`${selected.id}-${editingDoc?.id || 'new'}`}
              docs={selected.docs}
              editing={editingDoc}
              markdown={docMarkdown}
              busy={busy}
              onEdit={(doc) => {
                setEditingDoc(doc);
                setDocMarkdown(doc.markdown);
              }}
              onNew={() => {
                setEditingDoc(undefined);
                setDocMarkdown('');
              }}
              onMarkdown={setDocMarkdown}
              onSubmit={saveDoc}
            />
          )}
        </main>
      </div>
    </section>
  );
}

function EditorTab({
  active,
  disabled,
  number,
  label,
  onClick,
}: {
  active: boolean;
  disabled?: boolean;
  number: string;
  label: string;
  onClick: () => void;
}) {
  return (
    <button type="button" className={active ? 'active' : ''} disabled={disabled} onClick={onClick}>
      <span>{number}</span>
      {label}
    </button>
  );
}

function MetadataForm({
  selected,
  description,
  onDescription,
  busy,
  onSubmit,
}: {
  selected?: Library;
  description: string;
  onDescription: (value: string) => void;
  busy: boolean;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
}) {
  return (
    <form className="library-form" onSubmit={onSubmit}>
      <section className="library-form-section">
        <div className="library-section-title">
          <span>01</span>
          <div>
            <h3>Identificação</h3>
            <p>Dados usados no catálogo, licenciamento e URLs públicas.</p>
          </div>
        </div>
        <div className="library-fields columns-2">
          <Field label="Nome oficial" name="name" defaultValue={selected?.name} required />
          <Field
            label="Slug da URL"
            name="slug"
            prefix="/bibliotecas/"
            defaultValue={selected?.slug}
            required
            mono
          />
          <Field
            label="Product ID"
            name="productId"
            defaultValue={selected?.productId}
            required
            mono
            hint="Identificador imutável usado no licenciamento."
          />
          <Field
            label="Código do módulo"
            name="moduleCode"
            defaultValue={selected?.moduleCode}
            required
            mono
          />
          <Field label="ID do catálogo" name="catalogId" defaultValue={selected?.catalogId} mono />
          <Field
            label="Resumo comercial"
            name="summary"
            defaultValue={selected?.summary}
            required
            maxLength={180}
            hint="Texto curto exibido nos cards e resultados de busca."
          />
        </div>
      </section>
      <section className="library-form-section">
        <div className="library-section-title">
          <span>02</span>
          <div>
            <h3>Conteúdo público</h3>
            <p>A descrição aceita Markdown completo e mostra a aparência final em tempo real.</p>
          </div>
        </div>
        <MarkdownEditor
          name="description"
          value={description}
          onChange={onDescription}
          placeholder={
            '# Nome da biblioteca\n\nDescreva a proposta e destaque **benefícios importantes**.\n\n- Recurso principal\n- Outro recurso'
          }
        />
        <div className="library-fields columns-2">
          <TextAreaField
            label="Funcionalidades"
            name="features"
            defaultValue={selected?.features.join('\n')}
            hint="Uma funcionalidade por linha."
          />
          <TextAreaField
            label="Requisitos"
            name="requirements"
            defaultValue={selected?.requirements.join('\n')}
            hint="Um requisito por linha."
          />
        </div>
      </section>
      <footer className="library-form-actions">
        <span>
          {selected
            ? 'As alterações serão refletidas no catálogo após salvar.'
            : 'A biblioteca será criada inicialmente como rascunho.'}
        </span>
        <button className="button button-primary" disabled={busy}>
          {busy ? 'Salvando…' : selected ? 'Salvar alterações' : 'Criar biblioteca'}
        </button>
      </footer>
    </form>
  );
}

function PackagesPanel({
  selected,
  busy,
  onPlan,
  onUpload,
  onPublish,
  onArchive,
}: {
  selected: Library;
  busy: boolean;
  onPlan: (event: FormEvent<HTMLFormElement>) => void;
  onUpload: (event: FormEvent<HTMLFormElement>) => void;
  onPublish: (id: string) => void;
  onArchive: () => void;
}) {
  return (
    <div className="library-panel-stack">
      <section className="library-form-section">
        <div className="library-section-title">
          <span>01</span>
          <div>
            <h3>Planos comerciais</h3>
            <p>Configure preços e ciclos disponíveis no checkout.</p>
          </div>
        </div>
        {selected.plans.length > 0 && (
          <div className="plan-list">
            {selected.plans.map((plan) => (
              <div key={plan.id}>
                <span className={plan.active ? 'active' : ''}>
                  {plan.active ? 'ATIVO' : 'INATIVO'}
                </span>
                <div>
                  <strong>{plan.name}</strong>
                  <small>
                    {plan.code} · {plan.cycle === 'YEARLY' ? 'anual' : 'mensal'}
                  </small>
                </div>
                <b>
                  {new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(
                    plan.price,
                  )}
                </b>
              </div>
            ))}
          </div>
        )}
        <form className="library-inline-form" onSubmit={onPlan}>
          <Field label="Código" name="code" placeholder="monthly" required mono />
          <Field label="Nome" name="name" placeholder="Plano mensal" required />
          <Field label="Preço" name="price" type="number" min="0.01" step="0.01" required />
          <label className="library-field">
            <span>Ciclo</span>
            <select name="cycle">
              <option value="MONTHLY">Mensal</option>
              <option value="YEARLY">Anual</option>
            </select>
          </label>
          <button className="button button-primary" disabled={busy}>
            Adicionar plano
          </button>
        </form>
      </section>
      <section className="library-form-section">
        <div className="library-section-title">
          <span>02</span>
          <div>
            <h3>Pacotes de instalação</h3>
            <p>Envie o executável assinado e publique a versão pronta para distribuição.</p>
          </div>
        </div>
        <form className="release-upload" onSubmit={onUpload}>
          <Field label="Versão" name="version" placeholder="1.0.0" required mono />
          <label className="library-file-field">
            <input name="file" type="file" accept=".exe" required />
            <span>Selecionar instalador .exe</span>
            <small>O SHA-256 e o tamanho serão calculados no servidor.</small>
          </label>
          <button className="button button-primary" disabled={busy}>
            Enviar pacote
          </button>
        </form>
        <div className="release-list">
          {selected.releases.map((release) => (
            <div key={release.id}>
              <span className={release.isActive ? 'release-live' : 'release-draft'}>
                {release.isActive ? 'PUBLICADA' : 'AGUARDANDO'}
              </span>
              <div>
                <strong>v{release.version}</strong>
                <small>
                  {Math.ceil(release.sizeBytes / 1024 / 1024)} MB · SHA{' '}
                  {release.sha256.slice(0, 12)}…
                </small>
              </div>
              {!release.isActive && (
                <button
                  type="button"
                  className="text-link"
                  disabled={busy}
                  onClick={() => onPublish(release.id)}
                >
                  Publicar versão
                </button>
              )}
            </div>
          ))}
          {!selected.releases.length && <p className="library-empty">Nenhum pacote enviado.</p>}
        </div>
      </section>
      {selected.status === 'published' && (
        <section className="library-danger-zone">
          <div>
            <strong>Arquivar biblioteca</strong>
            <p>Remove o produto do catálogo público sem apagar seus dados.</p>
          </div>
          <button type="button" className="danger-link" disabled={busy} onClick={onArchive}>
            Arquivar
          </button>
        </section>
      )}
    </div>
  );
}

function DocsPanel({
  docs,
  editing,
  markdown,
  busy,
  onEdit,
  onNew,
  onMarkdown,
  onSubmit,
}: {
  docs: Doc[];
  editing?: Doc;
  markdown: string;
  busy: boolean;
  onEdit: (doc: Doc) => void;
  onNew: () => void;
  onMarkdown: (value: string) => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
}) {
  return (
    <div className="docs-manager">
      <aside className="docs-list">
        <div>
          <strong>Páginas</strong>
          <button type="button" onClick={onNew}>
            ＋ Nova
          </button>
        </div>
        {docs
          .slice()
          .sort((a, b) => a.position - b.position)
          .map((doc) => (
            <button
              type="button"
              key={doc.id}
              className={editing?.id === doc.id ? 'active' : ''}
              onClick={() => onEdit(doc)}
            >
              <span>{doc.status === 'published' ? '●' : '○'}</span>
              <div>
                <strong>{doc.title}</strong>
                <small>
                  {doc.group} · /{doc.slug}
                </small>
              </div>
            </button>
          ))}
        {!docs.length && <p className="library-empty">A documentação ainda não possui páginas.</p>}
      </aside>
      <form className="docs-editor-form" onSubmit={onSubmit}>
        <div className="library-section-title">
          <span>MD</span>
          <div>
            <h3>{editing ? `Editar ${editing.title}` : 'Nova página'}</h3>
            <p>Escreva em Markdown e confira o resultado antes de publicar.</p>
          </div>
        </div>
        <div className="library-fields columns-2">
          <Field label="Slug" name="slug" defaultValue={editing?.slug} required mono />
          <Field label="Título" name="title" defaultValue={editing?.title} required />
          <Field label="Resumo" name="summary" defaultValue={editing?.summary} required />
          <Field label="Grupo" name="group" defaultValue={editing?.group} required />
          <Field
            label="Ordem"
            name="position"
            type="number"
            defaultValue={editing?.position ?? 10}
            required
          />
          <label className="library-field">
            <span>Status</span>
            <select name="status" defaultValue={editing?.status || 'draft'}>
              <option value="draft">Rascunho</option>
              <option value="published">Publicada</option>
            </select>
          </label>
        </div>
        <MarkdownEditor
          name="markdown"
          value={markdown}
          onChange={onMarkdown}
          rows={22}
          placeholder={
            '# Título da página\n\nExplique o procedimento em etapas.\n\n```lisp\n(command "_.APPLOAD")\n```'
          }
        />
        <footer className="library-form-actions">
          <span>
            {editing
              ? 'Salvar mantém o mesmo slug como identificador da página.'
              : 'A página será adicionada à navegação da documentação.'}
          </span>
          <button className="button button-primary" disabled={busy}>
            {busy ? 'Salvando…' : editing ? 'Atualizar página' : 'Adicionar página'}
          </button>
        </footer>
      </form>
    </div>
  );
}

function Field({
  label,
  hint,
  prefix,
  mono,
  ...input
}: {
  label: string;
  hint?: string;
  prefix?: string;
  mono?: boolean;
} & React.InputHTMLAttributes<HTMLInputElement>) {
  return (
    <label className={`library-field${mono ? ' mono' : ''}`}>
      <span>{label}</span>
      <div className={prefix ? 'with-prefix' : ''}>
        {prefix && <small>{prefix}</small>}
        <input {...input} />
      </div>
      {hint && <em>{hint}</em>}
    </label>
  );
}
function TextAreaField({
  label,
  hint,
  ...textarea
}: { label: string; hint?: string } & React.TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <label className="library-field">
      <span>{label}</span>
      <textarea rows={6} {...textarea} />
      {hint && <em>{hint}</em>}
    </label>
  );
}

function MarkdownEditor({
  name,
  value,
  onChange,
  placeholder,
  rows = 16,
}: {
  name: string;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  rows?: number;
}) {
  const textarea = useRef<HTMLTextAreaElement>(null);
  function insert(before: string, after = '', fallback = 'texto') {
    const element = textarea.current;
    if (!element) return;
    const start = element.selectionStart;
    const end = element.selectionEnd;
    const selected = value.slice(start, end) || fallback;
    const next = `${value.slice(0, start)}${before}${selected}${after}${value.slice(end)}`;
    onChange(next);
    requestAnimationFrame(() => {
      element.focus();
      element.setSelectionRange(start + before.length, start + before.length + selected.length);
    });
  }
  return (
    <div className="markdown-editor">
      <div className="markdown-toolbar">
        <span>MARKDOWN</span>
        <div>
          <button type="button" onClick={() => insert('## ', '', 'Título')}>
            H2
          </button>
          <button type="button" onClick={() => insert('**', '**')}>
            B
          </button>
          <button type="button" onClick={() => insert('_', '_')}>
            I
          </button>
          <button type="button" onClick={() => insert('`', '`', 'código')}>
            Código
          </button>
          <button type="button" onClick={() => insert('- ', '', 'item')}>
            Lista
          </button>
          <button type="button" onClick={() => insert('[', '](https://)', 'link')}>
            Link
          </button>
          <button type="button" onClick={() => insert('```\n', '\n```', 'código')}>
            Bloco
          </button>
        </div>
        <small>GFM · prévia ao vivo</small>
      </div>
      <div className="markdown-split">
        <label>
          <span>CONTEÚDO</span>
          <textarea
            ref={textarea}
            name={name}
            rows={rows}
            spellCheck={false}
            value={value}
            placeholder={placeholder}
            onChange={(event) => onChange(event.target.value)}
            required
          />
        </label>
        <div className="markdown-preview">
          <span>PRÉ-VISUALIZAÇÃO</span>
          {value.trim() ? (
            <Markdown source={value} />
          ) : (
            <p>Comece a escrever para visualizar o conteúdo formatado.</p>
          )}
        </div>
      </div>
    </div>
  );
}
