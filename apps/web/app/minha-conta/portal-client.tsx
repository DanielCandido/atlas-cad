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
  provisioning: 'A integração está criando sua assinatura no Asaas.',
  provisioning_failed: 'Não foi possível criar a cobrança. Confira os dados e tente novamente.',
  pending_checkout: 'Conclua o pagamento no ambiente seguro do Asaas.',
  checkout_cancelled: 'O checkout foi cancelado. Você pode iniciar uma nova tentativa.',
  checkout_expired: 'O checkout expirou. Você pode gerar um novo link de pagamento.',
  cancelled: 'A assinatura foi cancelada. O histórico continua disponível nesta conta.',
  replaced: 'Esta assinatura foi substituída por uma assinatura mais recente.',
};

export function PortalClient() {
  const router = useRouter();
  const [portal, setPortal] = useState<Portal | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
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
    setError('');
    const body = JSON.stringify(Object.fromEntries(new FormData(event.currentTarget)));
    const response = await fetch('/api/portal/plan', {
      method: 'PUT',
      headers: { 'content-type': 'application/json' },
      body,
    });
    const result = await response.json();
    if (!response.ok) return setError(result.message);
    setPortal(result);
  }

  async function retry(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError('');
    const body = JSON.stringify(Object.fromEntries(new FormData(event.currentTarget)));
    const response = await fetch('/api/portal/subscription/retry', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body,
    });
    const result = await response.json();
    setBusy(false);
    if (!response.ok)
      return setError(result.message || 'Não foi possível criar uma nova cobrança.');
    setPortal(result);
    if (result.paymentUrl) window.location.assign(result.paymentUrl);
  }

  async function cancel(librarySlug = portal?.librarySlug || 'steelflow') {
    if (!confirm('Cancelar a assinatura e revogar a ativação atual?')) return;
    const response = await fetch(
      `/api/portal/subscription?librarySlug=${encodeURIComponent(librarySlug)}`,
      { method: 'DELETE' },
    );
    if (!response.ok) {
      const result = await response.json();
      return setError(result.message);
    }
    await load();
  }

  async function deactivate() {
    if (!confirm('Desativar a máquina atual? Ela deixará de renovar a licença.')) return;
    const response = await fetch(
      `/api/portal/machines?librarySlug=${encodeURIComponent(portal?.librarySlug || 'steelflow')}`,
      { method: 'DELETE' },
    );
    if (!response.ok) {
      const result = await response.json();
      return setError(result.message);
    }
    await load();
  }

  if (loading)
    return (
      <section className="section page-top">
        <div className="container">Carregando sua conta…</div>
      </section>
    );
  if (!portal)
    return (
      <section className="section page-top">
        <div className="container">
          <p className="form-error">{error}</p>
          <Link href="/entrar">Voltar</Link>
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
  const canRetry = portal.subscriptions.some(
    (subscription) => subscription.isCurrent && subscription.canRetry,
  );

  return (
    <section className="section page-top">
      <div className="container">
        <div className="account-heading">
          <div>
            <span className="eyebrow">ÁREA DO CLIENTE</span>
            <h1 className="page-title">
              Olá, <span>{portal.customerName.split(' ')[0]}.</span>
            </h1>
            <p>{portal.email}</p>
          </div>
          <span className={`status status-${portal.subscriptionStatus}`}>
            {labels[portal.subscriptionStatus] || portal.subscriptionStatus}
          </span>
        </div>

        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}

        <div className="account-grid">
          <article className="account-card account-wide subscriptions-card">
            <div className="subscriptions-heading">
              <div>
                <span className="eyebrow">SUAS LICENÇAS</span>
                <h2>Assinaturas</h2>
              </div>
              <Link className="button button-outline" href="/bibliotecas">
                Adicionar biblioteca
              </Link>
            </div>
            <div className="subscription-list">
              {portal.subscriptions.map((subscription) => (
                <div className="subscription-row" key={subscription.id}>
                  <div className="subscription-main">
                    <div className="subscription-title">
                      <div>
                        <strong>{subscription.libraryName}</strong>
                        <small>{subscription.planName}</small>
                      </div>
                      <span className={`status status-${subscription.status}`}>
                        {labels[subscription.status] || subscription.status}
                      </span>
                    </div>

                    <div className="subscription-details">
                      <div>
                        <span>Pagamento</span>
                        <strong>
                          {billingLabels[subscription.billingType] || subscription.billingType}
                        </strong>
                      </div>
                      <div>
                        <span>Início</span>
                        <strong>
                          {new Date(subscription.createdAt).toLocaleDateString('pt-BR')}
                        </strong>
                      </div>
                      <div>
                        <span>Validade</span>
                        <strong>
                          {subscription.paidThrough
                            ? new Date(subscription.paidThrough).toLocaleDateString('pt-BR')
                            : '—'}
                        </strong>
                      </div>
                    </div>

                    {subscription.isCurrent && (
                      <p className="subscription-explanation">
                        {explanations[subscription.status] ||
                          'Consulte o status da assinatura abaixo.'}
                      </p>
                    )}

                    {subscription.isCurrent && canRetry && (
                      <form className="form-stack retry-form subscription-retry" onSubmit={retry}>
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
                        <button className="button button-primary" disabled={busy}>
                          {busy ? 'Tentando novamente…' : 'Tentar pagamento novamente'}
                        </button>
                      </form>
                    )}

                    {subscription.isCurrent && portal.machineFingerprint && (
                      <div className="subscription-device">
                        <div>
                          <span>Computador ativado</span>
                          <strong>{portal.machineFingerprint.slice(0, 16)}…</strong>
                        </div>
                        <button className="danger-link" type="button" onClick={deactivate}>
                          Desativar computador
                        </button>
                      </div>
                    )}
                  </div>

                  <div className="subscription-actions">
                    {subscription.status === 'active' && (
                      <>
                        <a
                          className="button button-primary"
                          href={`/api/libraries/${subscription.librarySlug}/installer`}
                        >
                          Baixar instalador
                        </a>
                        <a
                          className="button button-outline"
                          href={`/api/libraries/${subscription.librarySlug}/uninstaller`}
                        >
                          Desinstalador
                        </a>
                      </>
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
                    {subscription.isCurrent && canChangePlan && (
                      <form className="subscription-plan" onSubmit={plan}>
                        <input type="hidden" name="librarySlug" value={subscription.librarySlug} />
                        <select
                          name="planCode"
                          defaultValue={subscription.planCode}
                          aria-label="Plano"
                        >
                          <option value="monthly">Plano mensal</option>
                          <option value="yearly">Plano anual</option>
                        </select>
                        <button className="button button-outline">Alterar plano</button>
                      </form>
                    )}
                    {!['cancelled', 'replaced'].includes(subscription.status) && (
                      <button
                        className="danger-link"
                        onClick={() => void cancel(subscription.librarySlug)}
                      >
                        Cancelar assinatura
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </article>
        </div>
      </div>
    </section>
  );
}
