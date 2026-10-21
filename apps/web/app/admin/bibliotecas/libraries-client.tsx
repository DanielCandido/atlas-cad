'use client';
import Link from 'next/link';
import { FormEvent, useCallback, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';

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

async function json(path: string, method = 'GET', body?: unknown) {
  const response = await fetch(`/api/admin/libraries${path}`, {
    method,
    headers: body ? { 'content-type': 'application/json' } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  const result = await response.json();
  if (!response.ok) throw new Error(result.message || 'Operação não concluída.');
  return result as Library | Library[];
}

export function AdminLibrariesClient() {
  const router = useRouter();
  const [items, setItems] = useState<Library[]>([]);
  const [selected, setSelected] = useState<Library>();
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const load = useCallback(async () => {
    try {
      const list = (await json('')) as Library[];
      setItems(list);
      setSelected((current) => list.find((item) => item.id === current?.id) || list[0]);
    } catch (reason) {
      if (String(reason).includes('401')) router.replace('/admin/entrar');
      else setError(reason instanceof Error ? reason.message : String(reason));
    }
  }, [router]);
  useEffect(() => {
    void load();
  }, [load]);
  async function run(action: () => Promise<unknown>) {
    setBusy(true);
    setError('');
    try {
      await action();
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setBusy(false);
    }
  }
  function values(form: HTMLFormElement) {
    return Object.fromEntries(new FormData(form));
  }
  function saveLibrary(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = values(event.currentTarget);
    const payload = {
      ...data,
      features: String(data.features || '')
        .split('\n')
        .filter(Boolean),
      requirements: String(data.requirements || '')
        .split('\n')
        .filter(Boolean),
    };
    void run(() => json(selected ? `/${selected.id}` : '', selected ? 'PUT' : 'POST', payload));
  }
  function savePlan(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected) return;
    const data = values(event.currentTarget);
    void run(() =>
      json(`/${selected.id}/plans`, 'PUT', { ...data, price: Number(data.price), active: true }),
    );
  }
  function saveDoc(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected) return;
    const data = values(event.currentTarget);
    void run(() =>
      json(`/${selected.id}/docs`, 'PUT', { ...data, position: Number(data.position) }),
    );
  }
  function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected) return;
    const form = new FormData(event.currentTarget);
    void run(async () => {
      const response = await fetch(`/api/admin/libraries/${selected.id}/releases`, {
        method: 'POST',
        body: form,
      });
      const result = await response.json();
      if (!response.ok) throw new Error(result.message || 'Upload não concluído.');
    });
  }
  return (
    <section className="section page-top admin-page">
      <div className="container">
        <div className="admin-heading">
          <div>
            <span className="eyebrow">ADMINISTRAÇÃO</span>
            <h1 className="admin-title">Bibliotecas</h1>
            <p>Cadastre, documente e publique instaladores sem novo deploy.</p>
          </div>
          <Link className="button button-outline" href="/admin">
            Usuários
          </Link>
        </div>
        {error && <p className="form-error">{error}</p>}
        <div className="account-grid">
          <aside className="account-card">
            <button className="button button-primary" onClick={() => setSelected(undefined)}>
              Nova biblioteca
            </button>
            {items.map((item) => (
              <button className="text-link" key={item.id} onClick={() => setSelected(item)}>
                {item.name} · {item.status}
              </button>
            ))}
          </aside>
          <div>
            <form className="account-card form-stack" onSubmit={saveLibrary}>
              <h2>{selected ? 'Dados da biblioteca' : 'Nova biblioteca'}</h2>
              <label>
                Nome
                <input name="name" defaultValue={selected?.name} required />
              </label>
              <label>
                Slug
                <input name="slug" defaultValue={selected?.slug} required />
              </label>
              <label>
                Product ID
                <input name="productId" defaultValue={selected?.productId} required />
              </label>
              <label>
                Módulo
                <input name="moduleCode" defaultValue={selected?.moduleCode} required />
              </label>
              <label>
                Catálogo
                <input name="catalogId" defaultValue={selected?.catalogId} />
              </label>
              <label>
                Resumo
                <input name="summary" defaultValue={selected?.summary} required />
              </label>
              <label>
                Descrição
                <textarea name="description" defaultValue={selected?.description} required />
              </label>
              <label>
                Funcionalidades, uma por linha
                <textarea name="features" defaultValue={selected?.features.join('\n')} />
              </label>
              <label>
                Requisitos, um por linha
                <textarea name="requirements" defaultValue={selected?.requirements.join('\n')} />
              </label>
              <button className="button button-primary" disabled={busy}>
                Salvar rascunho
              </button>
            </form>
            {selected && (
              <>
                <form className="account-card form-stack" onSubmit={savePlan}>
                  <h2>Plano</h2>
                  <label>
                    Código
                    <input name="code" placeholder="monthly" required />
                  </label>
                  <label>
                    Nome
                    <input name="name" required />
                  </label>
                  <label>
                    Preço
                    <input name="price" type="number" min="0.01" step="0.01" required />
                  </label>
                  <label>
                    Ciclo
                    <select name="cycle">
                      <option value="MONTHLY">Mensal</option>
                      <option value="YEARLY">Anual</option>
                    </select>
                  </label>
                  <button className="button button-primary" disabled={busy}>
                    Salvar plano
                  </button>
                </form>
                <form className="account-card form-stack" onSubmit={saveDoc}>
                  <h2>Página Markdown</h2>
                  <label>
                    Slug
                    <input name="slug" required />
                  </label>
                  <label>
                    Título
                    <input name="title" required />
                  </label>
                  <label>
                    Resumo
                    <input name="summary" required />
                  </label>
                  <label>
                    Grupo
                    <input name="group" required />
                  </label>
                  <label>
                    Ordem
                    <input name="position" type="number" defaultValue="10" />
                  </label>
                  <label>
                    Status
                    <select name="status">
                      <option value="draft">Rascunho</option>
                      <option value="published">Publicada</option>
                    </select>
                  </label>
                  <label>
                    Markdown
                    <textarea name="markdown" rows={12} required />
                  </label>
                  <button className="button button-primary" disabled={busy}>
                    Salvar página
                  </button>
                </form>
                <form className="account-card form-stack" onSubmit={upload}>
                  <h2>Nova versão</h2>
                  <label>
                    Versão
                    <input name="version" placeholder="1.0.0" required />
                  </label>
                  <label>
                    Instalador assinado
                    <input name="file" type="file" accept=".exe" required />
                  </label>
                  <button className="button button-primary" disabled={busy}>
                    Enviar instalador
                  </button>
                </form>
                <div className="account-card">
                  <h2>Versões</h2>
                  {selected.releases.map((release) => (
                    <p key={release.id}>
                      <strong>{release.version}</strong> ·{' '}
                      {Math.ceil(release.sizeBytes / 1024 / 1024)} MB{' '}
                      {release.isActive ? (
                        '· ativa'
                      ) : (
                        <button
                          className="text-link"
                          disabled={busy}
                          onClick={() =>
                            void run(() => json(`/${selected.id}/publish/${release.id}`, 'POST'))
                          }
                        >
                          Publicar
                        </button>
                      )}
                    </p>
                  ))}
                  {selected.status === 'published' && (
                    <button
                      className="danger-link"
                      disabled={busy}
                      onClick={() => void run(() => json(`/${selected.id}/archive`, 'POST'))}
                    >
                      Arquivar biblioteca
                    </button>
                  )}
                </div>
              </>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}
