'use client';
import Link from 'next/link';
import { FormEvent, useCallback, useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import type { AdminUserDetail } from '@/lib/admin-types';
import { formatDate, sourceLabels, statusLabels } from '@/lib/admin-types';

type Action = {
  title: string;
  description: string;
  path: string;
  method: 'POST' | 'PUT' | 'DELETE';
  needsDate?: boolean;
  danger?: boolean;
};
const auditLabels: Record<string, string> = {
  manual_grant_set: 'Validade manual concedida',
  manual_grant_removed: 'Validade manual removida',
  license_revoked: 'Licença revogada',
  license_restored: 'Licença restaurada',
  activation_revoked: 'Dispositivo revogado',
  subscription_cancelled: 'Assinatura cancelada',
};

export function AdminUserClient() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const [user, setUser] = useState<AdminUserDetail>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [action, setAction] = useState<Action>();
  const [busy, setBusy] = useState(false);
  const load = useCallback(async () => {
    setLoading(true);
    const response = await fetch(`/api/admin/users/${encodeURIComponent(id)}`);
    if (response.status === 401) {
      router.replace('/admin/entrar');
      return;
    }
    const result = await response.json();
    setLoading(false);
    if (!response.ok) return setError(result.message || 'Não foi possível carregar o usuário.');
    setUser(result);
  }, [id, router]);
  useEffect(() => {
    void load();
  }, [load]);
  async function execute(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!action) return;
    setBusy(true);
    setError('');
    const values = Object.fromEntries(new FormData(event.currentTarget));
    const body: Record<string, string> = { reason: String(values.reason) };
    if (action.needsDate) body.validUntil = new Date(String(values.validUntil)).toISOString();
    const response = await fetch(action.path, {
      method: action.method,
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(body),
    });
    setBusy(false);
    if (!response.ok) {
      const result = await response.json();
      setError(result.message || 'A operação falhou.');
      return;
    }
    setAction(undefined);
    await load();
  }
  if (loading && !user)
    return (
      <section className="section page-top">
        <div className="container">Carregando usuário…</div>
      </section>
    );
  if (!user)
    return (
      <section className="section page-top">
        <div className="container">
          <p className="form-error">{error}</p>
          <Link href="/admin">← Voltar</Link>
        </div>
      </section>
    );
  const license = user.license;
  const currentSubscription = user.subscriptions.find((item) => item.isCurrent);
  const dateDefault = new Date(Date.now() + 30 * 86400000).toISOString().slice(0, 16);
  return (
    <section className="section page-top admin-page">
      <div className="container">
        <Link className="text-link admin-back" href="/admin">
          ← Todos os usuários
        </Link>
        <div className="admin-heading">
          <div>
            <span className="eyebrow">USUÁRIO</span>
            <h1 className="admin-title">{user.name}</h1>
            <p>
              {user.email} · cliente desde {formatDate(user.createdAt)}
            </p>
          </div>
          {license && (
            <span className={`status status-${license.accessStatus}`}>
              {statusLabels[license.accessStatus]}
            </span>
          )}
        </div>
        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}
        <div className="account-grid">
          <article className="account-card">
            <h2>Assinatura atual</h2>
            {currentSubscription ? (
              <>
                <dl className="admin-facts">
                  <div>
                    <dt>Plano</dt>
                    <dd>{currentSubscription.planName}</dd>
                  </div>
                  <div>
                    <dt>Status</dt>
                    <dd>
                      {statusLabels[currentSubscription.status] || currentSubscription.status}
                    </dd>
                  </div>
                  <div>
                    <dt>Pago até</dt>
                    <dd>{formatDate(currentSubscription.paidThrough)}</dd>
                  </div>
                  <div>
                    <dt>Cobrança</dt>
                    <dd>{currentSubscription.billingType}</dd>
                  </div>
                </dl>
                {!['cancelled', 'replaced'].includes(currentSubscription.status) && (
                  <button
                    className="danger-link"
                    onClick={() =>
                      setAction({
                        title: 'Cancelar assinatura',
                        description:
                          'Cancela a cobrança no Asaas, remove a concessão manual e revoga a licença e os dispositivos.',
                        path: `/api/admin/users/${user.id}/subscription/cancel`,
                        method: 'POST',
                        danger: true,
                      })
                    }
                  >
                    Cancelar assinatura
                  </button>
                )}
              </>
            ) : (
              <p>Sem assinatura.</p>
            )}
          </article>
          <article className="account-card">
            <h2>Licença</h2>
            {license ? (
              <>
                <dl className="admin-facts">
                  <div>
                    <dt>ID</dt>
                    <dd className="mono">{license.id}</dd>
                  </div>
                  <div>
                    <dt>Acesso efetivo</dt>
                    <dd>{statusLabels[license.accessStatus]}</dd>
                  </div>
                  <div>
                    <dt>Origem</dt>
                    <dd>{license.accessSource ? sourceLabels[license.accessSource] : '—'}</dd>
                  </div>
                  <div>
                    <dt>Válido até</dt>
                    <dd>{formatDate(license.accessExpiresAt)}</dd>
                  </div>
                  <div>
                    <dt>Concessão manual</dt>
                    <dd>{formatDate(license.manualValidUntil)}</dd>
                  </div>
                </dl>
                <div className="button-row">
                  <button
                    className="button button-primary"
                    onClick={() =>
                      setAction({
                        title: 'Conceder validade manual',
                        description:
                          'Define acesso sem alterar a validade financeira registrada pelo Asaas.',
                        path: `/api/admin/licenses/${license.id}/manual-grant`,
                        method: 'PUT',
                        needsDate: true,
                      })
                    }
                  >
                    Renovar manualmente
                  </button>
                  {license.manualValidUntil && (
                    <button
                      className="button button-outline"
                      onClick={() =>
                        setAction({
                          title: 'Remover concessão manual',
                          description: 'Remove somente a validade concedida por um administrador.',
                          path: `/api/admin/licenses/${license.id}/manual-grant`,
                          method: 'DELETE',
                          danger: true,
                        })
                      }
                    >
                      Remover concessão
                    </button>
                  )}
                  {license.revokedAt ? (
                    <button
                      className="button button-outline"
                      onClick={() =>
                        setAction({
                          title: 'Restaurar licença',
                          description:
                            'Remove o bloqueio. O acesso dependerá de pagamento ou concessão manual vigente.',
                          path: `/api/admin/licenses/${license.id}/restore`,
                          method: 'POST',
                        })
                      }
                    >
                      Restaurar licença
                    </button>
                  ) : (
                    <button
                      className="danger-link compact-danger"
                      onClick={() =>
                        setAction({
                          title: 'Revogar licença',
                          description:
                            'Bloqueia novas ativações e renovações sem cancelar a cobrança. Dispositivos offline podem funcionar por até 72 horas.',
                          path: `/api/admin/licenses/${license.id}/revoke`,
                          method: 'POST',
                          danger: true,
                        })
                      }
                    >
                      Revogar licença
                    </button>
                  )}
                </div>
              </>
            ) : (
              <p>Este usuário ainda não possui licença emitida.</p>
            )}
          </article>
          <article className="account-card account-wide">
            <h2>Dispositivos</h2>
            <div className="admin-list">
              {user.activations.map((device) => (
                <div className="admin-list-row" key={device.id}>
                  <div>
                    <strong className="mono">{device.deviceFingerprint}</strong>
                    <small>
                      Ativado em {formatDate(device.activatedAt)} · última renovação{' '}
                      {formatDate(device.lastRenewedAt)}
                    </small>
                  </div>
                  <div>
                    {device.isCurrent ? (
                      <>
                        <span className="status status-active">Ativo</span>
                        <button
                          className="danger-link compact-danger"
                          onClick={() =>
                            setAction({
                              title: 'Revogar dispositivo',
                              description:
                                'A máquina deixará de renovar e o usuário poderá vincular outra. O cache offline pode durar até 72 horas.',
                              path: `/api/admin/activations/${device.id}/revoke`,
                              method: 'POST',
                              danger: true,
                            })
                          }
                        >
                          Revogar
                        </button>
                      </>
                    ) : (
                      <span className="status status-revoked">Revogado</span>
                    )}
                  </div>
                </div>
              ))}
              {user.activations.length === 0 && <p>Nenhum dispositivo ativado.</p>}
            </div>
          </article>
          <article className="account-card account-wide">
            <h2>Histórico de assinaturas</h2>
            <div className="admin-list">
              {user.subscriptions.map((subscription) => (
                <div className="admin-list-row" key={subscription.id}>
                  <div>
                    <strong>{subscription.planName}</strong>
                    <small>
                      Criada em {formatDate(subscription.createdAt)} · paga até{' '}
                      {formatDate(subscription.paidThrough)}
                    </small>
                  </div>
                  <span className={`status status-${subscription.status}`}>
                    {statusLabels[subscription.status] || subscription.status}
                  </span>
                </div>
              ))}
            </div>
          </article>
          <article className="account-card account-wide">
            <h2>Auditoria</h2>
            <div className="admin-list">
              {user.audit.map((entry) => (
                <div className="admin-list-row" key={entry.id}>
                  <div>
                    <strong>{auditLabels[entry.action] || entry.action}</strong>
                    <small>
                      {entry.adminEmail} · {formatDate(entry.createdAt)}
                      {entry.ipAddress ? ` · IP ${entry.ipAddress}` : ''}
                    </small>
                    <p>{entry.reason}</p>
                  </div>
                  <span className={`status status-${entry.outcome}`}>
                    {statusLabels[entry.outcome] || entry.outcome}
                  </span>
                </div>
              ))}
              {user.audit.length === 0 && <p>Nenhuma ação administrativa registrada.</p>}
            </div>
          </article>
        </div>
        {action && (
          <div className="admin-modal-backdrop" role="presentation">
            <div
              className="admin-modal"
              role="dialog"
              aria-modal="true"
              aria-labelledby="action-title"
            >
              <h2 id="action-title">{action.title}</h2>
              <p>{action.description}</p>
              <form className="form-stack" onSubmit={execute}>
                {action.needsDate && (
                  <label>
                    Válida até
                    <input
                      name="validUntil"
                      type="datetime-local"
                      defaultValue={dateDefault}
                      required
                    />
                  </label>
                )}
                <label>
                  Motivo
                  <textarea
                    name="reason"
                    rows={4}
                    minLength={3}
                    maxLength={500}
                    required
                    placeholder="Explique por que esta ação está sendo realizada."
                  />
                </label>
                <div className="button-row">
                  <button
                    type="button"
                    className="button button-outline"
                    onClick={() => setAction(undefined)}
                    disabled={busy}
                  >
                    Voltar
                  </button>
                  <button
                    className={`button ${action.danger ? 'button-danger' : 'button-primary'}`}
                    disabled={busy}
                  >
                    {busy ? 'Salvando…' : 'Confirmar'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    </section>
  );
}
