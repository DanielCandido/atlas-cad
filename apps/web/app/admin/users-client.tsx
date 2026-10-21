'use client';
import Link from 'next/link';
import { FormEvent, useCallback, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import type { AdminUser, AdminUserList } from '@/lib/admin-types';
import { formatDate, sourceLabels, statusLabels } from '@/lib/admin-types';

export function AdminUsersClient() {
  const router = useRouter();
  const [items, setItems] = useState<AdminUser[]>([]);
  const [nextCursor, setNextCursor] = useState<string>();
  const [query, setQuery] = useState('');
  const [filters, setFilters] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const load = useCallback(
    async (search: string, append = false) => {
      setLoading(true);
      setError('');
      const response = await fetch(`/api/admin/users?${search}`);
      if (response.status === 401) {
        router.replace('/admin/entrar');
        return;
      }
      const result = await response.json();
      setLoading(false);
      if (!response.ok) return setError(result.message || 'Não foi possível carregar os usuários.');
      const data = result as AdminUserList;
      setItems((current) => (append ? [...current, ...data.items] : data.items));
      setNextCursor(data.nextCursor);
    },
    [router],
  );
  useEffect(() => {
    void load('pageSize=50');
  }, [load]);
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const values = new FormData(event.currentTarget);
    const params = new URLSearchParams();
    for (const [key, value] of values) if (String(value)) params.set(key, String(value));
    params.set('pageSize', '50');
    const value = params.toString();
    setFilters(value);
    void load(value);
  }
  async function logout() {
    await fetch('/api/admin/auth/logout', { method: 'POST' });
    router.replace('/admin/entrar');
    router.refresh();
  }
  return (
    <section className="section page-top admin-page">
      <div className="container">
        <div className="admin-heading">
          <div>
            <span className="eyebrow">ADMINISTRAÇÃO</span>
            <h1 className="admin-title">Usuários e licenças</h1>
            <p>Consulte acesso, vencimentos e dispositivos em um só lugar.</p>
          </div>
          <Link className="button button-outline" href="/admin/bibliotecas">
            Bibliotecas
          </Link>
          <button className="button button-outline" onClick={logout}>
            Sair
          </button>
        </div>
        <form className="admin-filters" onSubmit={submit}>
          <input
            name="q"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Nome, e-mail, licença ou dispositivo"
          />
          <select name="plan">
            <option value="">Todos os planos</option>
            <option value="monthly">Mensal</option>
            <option value="yearly">Anual</option>
          </select>
          <select name="subscriptionStatus">
            <option value="">Toda assinatura</option>
            <option value="active">Ativa</option>
            <option value="past_due">Atrasada</option>
            <option value="suspended">Suspensa</option>
            <option value="cancelled">Cancelada</option>
            <option value="pending_payment">Pendente</option>
          </select>
          <select name="accessStatus">
            <option value="">Todo acesso</option>
            <option value="active">Ativo</option>
            <option value="expired">Vencido</option>
            <option value="revoked">Revogado</option>
            <option value="none">Sem licença</option>
          </select>
          <select name="device">
            <option value="">Todo dispositivo</option>
            <option value="active">Com dispositivo</option>
            <option value="none">Sem dispositivo</option>
          </select>
          <select name="expiry">
            <option value="">Todo vencimento</option>
            <option value="7d">Próximos 7 dias</option>
            <option value="30d">Próximos 30 dias</option>
            <option value="expired">Vencidos</option>
          </select>
          <button className="button button-primary">Filtrar</button>
        </form>
        {error && <p className="form-error">{error}</p>}
        <div className="admin-table-wrap">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Usuário</th>
                <th>Plano</th>
                <th>Assinatura</th>
                <th>Acesso</th>
                <th>Vencimento</th>
                <th>Dispositivo</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {items.map((user) => (
                <tr key={user.id}>
                  <td>
                    <strong>{user.name}</strong>
                    <small>{user.email}</small>
                  </td>
                  <td>{user.planName || '—'}</td>
                  <td>
                    <span className={`status status-${user.subscriptionStatus}`}>
                      {statusLabels[user.subscriptionStatus || ''] ||
                        user.subscriptionStatus ||
                        '—'}
                    </span>
                  </td>
                  <td>
                    <span className={`status status-${user.accessStatus}`}>
                      {statusLabels[user.accessStatus] || user.accessStatus}
                    </span>
                    <small>{user.accessSource ? sourceLabels[user.accessSource] : ''}</small>
                  </td>
                  <td>{formatDate(user.accessExpiresAt)}</td>
                  <td className="mono">
                    {user.deviceFingerprint ? `${user.deviceFingerprint.slice(0, 12)}…` : '—'}
                  </td>
                  <td>
                    <Link className="text-link" href={`/admin/usuarios/${user.id}`}>
                      Gerenciar →
                    </Link>
                  </td>
                </tr>
              ))}
              {!loading && items.length === 0 && (
                <tr>
                  <td colSpan={7} className="empty-state">
                    Nenhum usuário encontrado.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        {loading && <p className="admin-loading">Carregando…</p>}
        {nextCursor && !loading && (
          <div className="admin-more">
            <button
              className="button button-outline"
              onClick={() =>
                void load(
                  `${filters || 'pageSize=50'}&cursor=${encodeURIComponent(nextCursor)}`,
                  true,
                )
              }
            >
              Carregar mais
            </button>
          </div>
        )}
      </div>
    </section>
  );
}
