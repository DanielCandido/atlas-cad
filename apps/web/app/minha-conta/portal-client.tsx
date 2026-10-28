'use client';

import Link from 'next/link';
import { FormEvent, useCallback, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';

type Subscription = {
  id: string;
  librarySlug: string;
  libraryName: string;
  planCode: string;
  planName: string;
  status: string;
  billingType: string;
  paidThrough?: string;
  paymentUrl?: string;
  createdAt: string;
  isCurrent: boolean;
  canRetry: boolean;
};

type Portal = {
  customerName: string;
  email: string;
  librarySlug: string;
  libraryName: string;
  planCode: string;
  planName: string;
  subscriptionStatus: string;
  billingType?: string;
  paidThrough?: string;
  canDownload: boolean;
  canActivate: boolean;
  paymentUrl?: string;
  licenseId?: string;
  machineFingerprint?: string;
  subscriptions: Subscription[];
};

const labels: Record<string, string> = {
  active: 'Ativa',
  pending_payment: 'Aguardando pagamento',
  past_due: 'Pagamento atrasado',
  suspended: 'Suspensa',
  provisioning: 'Preparando',
  provisioning_failed: 'Falha na preparação',
  pending_checkout: 'Aguardando checkout',
  checkout_cancelled: 'Checkout cancelado',
  checkout_expired: 'Checkout expirado',
  cancelled: 'Cancelada',
  replaced: 'Substituída',
};

const billingLabels: Record<string, string> = {
  CREDIT_CARD: 'Cartão de crédito',
  PIX: 'PIX',
  BOLETO: 'Boleto',
};

const explanations: Record<string, string> = {
  active: 'Pagamento confirmado. Download e ativação estão disponíveis.',
  pending_payment: 'A cobrança foi criada e está aguardando pagamento.',
  past_due: 'Existe uma cobrança vencida. Abra a cobrança para regularizar.',
  suspended: 'A assinatura foi suspensa após estorno ou chargeback.',
  provisioning: 'Estamos preparando sua assinatura. Esta página será atualizada automaticamente.',
  provisioning_failed: 'Não foi possível criar a cobrança. Confira os dados e tente novamente.',
  pending_checkout: 'Conclua o pagamento no ambiente seguro do Asaas.',
  checkout_cancelled: 'O checkout foi cancelado. Você pode iniciar uma nova tentativa.',
  checkout_expired: 'O checkout expirou. Você pode gerar um novo link de pagamento.',
  cancelled: 'A assinatura foi cancelada. O histórico continua disponível nesta conta.',
  replaced: 'Esta assinatura foi substituída por uma assinatura mais recente.',
};

function MonitorIcon() {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" aria-hidden="true">
      <path
        d="M4 5.5h16v11H4zM9 20h6M12 16.5V20"
        stroke="currentColor"
        strokeWidth="1.7"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function DownloadIcon() {
  return (
    <svg viewBox="0 0 24 24" width="17" height="17" fill="none" aria-hidden="true">
      <path
        d="M12 3v12m0 0 4-4m-4 4-4-4M5 20h14"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function PortalClient() {
  const router = useRouter();
  const [portal, setPortal] = useState<Portal | null>(null);
  const [loading, setLoading] = useState(true);
  const [busyAction, setBusyAction] = useState('');
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    const response = await fetch('/api/portal');
    if (response.status === 401) {
      router.replace('/entrar');
      return;
    }
    const result = await response.json();
    if (!response.ok) setError(result.message);
    else setPortal(result);
    setLoading(false);
  }, [router]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (
      !portal ||
      !['provisioning', 'pending_checkout', 'pending_payment'].includes(portal.subscriptionStatus)
    )
      return;
    const timer = window.setInterval(() => void load(), 5000);
    return () => window.clearInterval(timer);
  }, [load, portal]);

  async function plan(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusyAction('plan');
    setError('');
    const body = JSON.stringify(Object.fromEntries(new FormData(event.currentTarget)));
    const response = await fetch('/api/portal/plan', {
      method: 'PUT',
      headers: { 'content-type': 'application/json' },
      body,
    });
    const result = await response.json();
    setBusyAction('');
    if (!response.ok) return setError(result.message);
    setPortal(result);
  }

  async function retry(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusyAction('retry');
    setError('');
    const body = JSON.stringify(Object.fromEntries(new FormData(event.currentTarget)));
    const response = await fetch('/api/portal/subscription/retry', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body,
    });
    const result = await response.json();
    setBusyAction('');
    if (!response.ok)
      return setError(result.message || 'Não foi possível criar uma nova cobrança.');
    setPortal(result);
    if (result.paymentUrl) window.location.assign(result.paymentUrl);
  }

  async function cancel(librarySlug = portal?.librarySlug || 'steelflow') {
    if (!confirm('Cancelar a assinatura e revogar a ativação atual?')) return;
    setBusyAction(`cancel-${librarySlug}`);
    const response = await fetch(
      `/api/portal/subscription?librarySlug=${encodeURIComponent(librarySlug)}`,
      { method: 'DELETE' },
    );
    setBusyAction('');
    if (!response.ok) {
      const result = await response.json();
      return setError(result.message);
    }
    await load();
  }

  async function deactivate() {
    if (!confirm('Desativar o computador atual? Ele deixará de renovar a licença.')) return;
    setBusyAction('deactivate');
    const response = await fetch(
      `/api/portal/machines?librarySlug=${encodeURIComponent(portal?.librarySlug || 'steelflow')}`,
      { method: 'DELETE' },
    );
    setBusyAction('');
    if (!response.ok) {
      const result = await response.json();
      return setError(result.message);
    }
    await load();
  }

  if (loading)
    return (
      <section className="section page-top account-page" aria-busy="true">
        <div className="container account-loading">
          <span className="account-loading-mark" aria-hidden="true" />
          <div>
            <strong>Carregando sua conta</strong>
            <span>Buscando assinaturas e licenças…</span>
          </div>
        </div>
      </section>
    );

  if (!portal)
    return (
      <section className="section page-top account-page">
        <div className="container account-empty">
          <span className="eyebrow">ÁREA DO CLIENTE</span>
          <h1>Não foi possível abrir sua conta.</h1>
          <p className="form-error" role="alert">
            {error}
          </p>
          <Link className="button button-outline" href="/entrar">
            Voltar para o acesso
          </Link>
        </div>
      </section>
    );

  const canChangePlan = ['active', 'pending_payment', 'past_due', 'suspended'].includes(
    portal.subscriptionStatus,
  );
  const canOpenPayment =
    Boolean(portal.paymentUrl) &&
    !['active', 'provisioning_failed', 'checkout_cancelled', 'checkout_expired'].includes(
      portal.subscriptionStatus,
    );
  return (
    <section className="section page-top account-page">
      <div className="container account-container">
        <header className="account-heading account-hero">
          <div>
            <span className="eyebrow">MINHA CONTA</span>
            <h1 className="page-title">Olá, {portal.customerName.split(' ')[0]}.</h1>
            <p>Gerencie suas assinaturas, instalações e pagamentos.</p>
          </div>
          <div className="account-identity">
            <span>Conta conectada</span>
            <strong>{portal.email}</strong>
          </div>
        </header>

        {error && (
          <p className="form-error account-alert" role="alert">
            {error}
          </p>
        )}

        <div className="account-section-heading">
          <div>
            <h2>Suas assinaturas</h2>
            <p>
              {portal.subscriptions.length}{' '}
              {portal.subscriptions.length === 1 ? 'produto vinculado' : 'produtos vinculados'} à
              sua conta
            </p>
          </div>
          <Link className="button button-outline" href="/bibliotecas">
            <span aria-hidden="true">+</span> Adicionar biblioteca
          </Link>
        </div>

        <div className="account-subscription-list">
          {portal.subscriptions.length === 0 && (
            <div className="account-no-subscriptions">
              <span className="subscription-product-mark" aria-hidden="true">
                +
              </span>
              <div>
                <h3>Nenhuma assinatura por aqui ainda.</h3>
                <p>Explore o catálogo e escolha a primeira biblioteca para sua conta.</p>
              </div>
              <Link className="button button-primary" href="/bibliotecas">
                Explorar bibliotecas
              </Link>
            </div>
          )}
          {portal.subscriptions.map((subscription) => (
            <article className="account-subscription" key={subscription.id}>
              <div className="account-subscription-head">
                <div className="subscription-product">
                  <span className="subscription-product-mark" aria-hidden="true">
                    {subscription.libraryName.slice(0, 2).toUpperCase()}
                  </span>
                  <div>
                    <h3>{subscription.libraryName}</h3>
                    <p>{subscription.planName}</p>
                  </div>
                </div>
                <span className={`status status-${subscription.status}`}>
                  <span className="status-dot" aria-hidden="true" />
                  {labels[subscription.status] || subscription.status}
                </span>
              </div>

              {subscription.isCurrent && (
                <div
                  className={`account-status-message status-message-${subscription.status}`}
                  role="status"
                >
                  <span className="subscription-explanation-icon" aria-hidden="true">
                    {subscription.status === 'active' ? '✓' : 'i'}
                  </span>
                  <p>
                    {explanations[subscription.status] || 'Consulte o status da assinatura abaixo.'}
                  </p>
                </div>
              )}

              <dl className="subscription-details account-subscription-details">
                <div>
                  <dt>Pagamento</dt>
                  <dd>{billingLabels[subscription.billingType] || subscription.billingType}</dd>
                </div>
                <div>
                  <dt>Início</dt>
                  <dd>{new Date(subscription.createdAt).toLocaleDateString('pt-BR')}</dd>
                </div>
                <div>
                  <dt>Validade</dt>
                  <dd>
                    {subscription.paidThrough
                      ? new Date(subscription.paidThrough).toLocaleDateString('pt-BR')
                      : '—'}
                  </dd>
                </div>
              </dl>

              {subscription.isCurrent && portal.machineFingerprint && (
                <div className="subscription-device">
                  <div className="subscription-device-info">
                    <span className="subscription-device-icon">
                      <MonitorIcon />
                    </span>
                    <div>
                      <span>Computador ativado</span>
                      <strong>{portal.machineFingerprint.slice(0, 16)}…</strong>
                    </div>
                  </div>
                  <button
                    className="danger-link"
                    type="button"
                    onClick={deactivate}
                    disabled={busyAction === 'deactivate'}
                  >
                    {busyAction === 'deactivate' ? 'Desativando…' : 'Desativar computador'}
                  </button>
                </div>
              )}

              {subscription.isCurrent && subscription.canRetry && (
                <form
                  className="form-stack retry-form subscription-retry account-retry"
                  onSubmit={retry}
                >
                  <div className="account-retry-heading">
                    <strong>Gerar uma nova cobrança</strong>
                    <span>Confirme seus dados para tentar o pagamento novamente.</span>
                  </div>
                  <input type="hidden" name="librarySlug" value={subscription.librarySlug} />
                  <label>
                    CPF ou CNPJ usado no cadastro
                    <input name="taxId" inputMode="numeric" required minLength={11} />
                  </label>
                  {portal.billingType === 'CREDIT_CARD' && (
                    <div className="subscription-retry-fields">
                      <label>
                        Telefone com DDD
                        <input
                          name="phone"
                          type="tel"
                          inputMode="tel"
                          autoComplete="tel"
                          required
                        />
                      </label>
                      <label>
                        CEP
                        <input
                          name="postalCode"
                          inputMode="numeric"
                          autoComplete="postal-code"
                          required
                          minLength={8}
                        />
                      </label>
                      <label>
                        Logradouro
                        <input name="address" autoComplete="street-address" required />
                      </label>
                      <label>
                        Número
                        <input name="addressNumber" autoComplete="address-line2" required />
                      </label>
                      <label>
                        Bairro
                        <input name="province" required />
                      </label>
                    </div>
                  )}
                  <button className="button button-primary" disabled={busyAction === 'retry'}>
                    {busyAction === 'retry' ? 'Gerando cobrança…' : 'Tentar pagamento novamente'}
                  </button>
                </form>
              )}

              <div className="account-subscription-actions">
                <div className="account-primary-actions">
                  {subscription.status === 'active' && (
                    <a
                      className="button button-primary"
                      href={`/api/libraries/${subscription.librarySlug}/installer`}
                    >
                      <DownloadIcon /> Baixar instalador
                    </a>
                  )}
                  {subscription.status === 'active' && (
                    <a
                      className="button button-outline"
                      href={`/api/libraries/${subscription.librarySlug}/uninstaller`}
                    >
                      Baixar desinstalador
                    </a>
                  )}
                  {subscription.isCurrent && canOpenPayment && portal.paymentUrl && (
                    <a
                      className="button button-primary"
                      href={portal.paymentUrl}
                      target="_blank"
                      rel="noreferrer"
                    >
                      {portal.billingType === 'CREDIT_CARD' ? 'Abrir checkout' : 'Abrir cobrança'}
                    </a>
                  )}
                </div>
                {subscription.isCurrent && canChangePlan && (
                  <form className="subscription-plan account-plan" onSubmit={plan}>
                    <label htmlFor={`plan-${subscription.id}`}>Plano</label>
                    <select
                      id={`plan-${subscription.id}`}
                      name="planCode"
                      defaultValue={subscription.planCode}
                    >
                      <option value="monthly">Plano mensal</option>
                      <option value="yearly">Plano anual</option>
                    </select>
                    <input type="hidden" name="librarySlug" value={subscription.librarySlug} />
                    <button
                      className="button button-outline"
                      type="submit"
                      disabled={busyAction === 'plan'}
                    >
                      {busyAction === 'plan' ? 'Salvando…' : 'Alterar plano'}
                    </button>
                  </form>
                )}
                {!['cancelled', 'replaced'].includes(subscription.status) && (
                  <button
                    className="danger-link account-cancel"
                    type="button"
                    onClick={() => void cancel(subscription.librarySlug)}
                    disabled={busyAction === `cancel-${subscription.librarySlug}`}
                  >
                    {busyAction === `cancel-${subscription.librarySlug}`
                      ? 'Cancelando…'
                      : 'Cancelar assinatura'}
                  </button>
                )}
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
