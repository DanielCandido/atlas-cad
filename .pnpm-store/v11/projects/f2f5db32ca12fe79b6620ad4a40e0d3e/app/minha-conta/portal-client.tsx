'use client';

import Link from 'next/link';
import { FormEvent, useCallback, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';

type Subscription = {
  id: string;
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
  const [activation, setActivation] = useState('');

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
      !['provisioning', 'pending_checkout', 'pending_payment'].includes(
        portal.subscriptionStatus,
      )
    )
      return;

    const timer = window.setInterval(() => void load(), 5000);
    return () => window.clearInterval(timer);
  }, [load, portal]);

  async function machine(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');
    const body = JSON.stringify(Object.fromEntries(new FormData(event.currentTarget)));
    const response = await fetch('/api/portal/machines', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body,
    });
    const result = await response.json();
    if (!response.ok) return setError(result.message);
    setActivation(result.activationCode);
  }

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

  async function cancel() {
    if (!confirm('Cancelar a assinatura e revogar a ativação atual?')) return;
    const response = await fetch('/api/portal/subscription', { method: 'DELETE' });
    if (!response.ok) {
      const result = await response.json();
      return setError(result.message);
    }
    await load();
  }

  async function deactivate() {
    if (!confirm('Desativar a máquina atual? Ela deixará de renovar a licença.')) return;
    const response = await fetch('/api/portal/machines', { method: 'DELETE' });
    if (!response.ok) {
      const result = await response.json();
      return setError(result.message);
    }
    setActivation('');
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
  const canCancel = [
    'pending_checkout',
    'pending_payment',
    'active',
    'past_due',
    'suspended',
  ].includes(portal.subscriptionStatus);
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
          <article className="account-card">
            <h2>Sua assinatura</h2>
            <p>
              <strong>{portal.planName}</strong>
              <br />
              Pagamento:{' '}
              {portal.billingType ? billingLabels[portal.billingType] || portal.billingType : '—'}
              <br />
              Válida até:{' '}
              {portal.paidThrough
                ? new Date(portal.paidThrough).toLocaleDateString('pt-BR')
                : 'aguardando confirmação'}
            </p>
            <p>
              {explanations[portal.subscriptionStatus] || 'Consulte o status da assinatura abaixo.'}
            </p>

            {canOpenPayment && portal.paymentUrl && (
              <a
                className="button button-primary"
                href={portal.paymentUrl}
                target="_blank"
                rel="noreferrer"
              >
                {portal.billingType === 'CREDIT_CARD' ? 'Abrir checkout seguro' : 'Abrir cobrança'}
              </a>
            )}

            {canRetry && (
              <form className="form-stack retry-form" onSubmit={retry}>
                <label>
                  CPF ou CNPJ usado no cadastro
                  <input name="taxId" inputMode="numeric" required minLength={11} />
                </label>
                {portal.billingType === 'CREDIT_CARD' && (
                  <>
                    <label>
                      Telefone com DDD
                      <input name="phone" type="tel" inputMode="tel" autoComplete="tel" required />
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
                  </>
                )}
                <button className="button button-primary" disabled={busy}>
                  {busy ? 'Tentando novamente…' : 'Tentar pagamento novamente'}
                </button>
              </form>
            )}

            {canChangePlan && (
              <form className="inline-form" onSubmit={plan}>
                <select name="planCode" defaultValue={portal.planCode}>
                  <option value="monthly">Plano mensal</option>
                  <option value="yearly">Plano anual</option>
                </select>
                <button className="button button-outline">Alterar plano</button>
              </form>
            )}
          </article>

          <article className="account-card">
            <h2>Download</h2>
            <p>
              Pacote para Windows com instalador, biblioteca protegida e gerador do identificador da
              máquina.
            </p>
            {portal.canDownload ? (
              <div className="form-stack">
                <a className="button button-primary" href="/api/download/autocad-2022-2024">
                  Baixar para AutoCAD 2022–2024
                </a>
                <a className="button button-primary" href="/api/download/autocad-2025-2026-1-1">
                  Baixar para AutoCAD 2025 / 2026 até Update 1.1
                </a>
                <a className="button button-primary" href="/api/download/autocad-2026-1-2-plus">
                  Baixar para AutoCAD 2026 Update 1.2+
                </a>
                <a className="button button-outline" href="/api/machine-identifier">
                  Baixar Identificador
                </a>
                <small>
                  No AutoCAD 2026, consulte SOBRE/ABOUT para escolher o pacote conforme a atualização instalada.
                </small>
              </div>
            ) : (
              <button className="button button-disabled" disabled>
                Aguardando pagamento
              </button>
            )}
          </article>

          <article className="account-card account-wide">
            <h2>Histórico de assinaturas</h2>
            <div className="subscription-list">
              {portal.subscriptions.map((subscription) => (
                <div className="subscription-row" key={subscription.id}>
                  <div>
                    <strong>{subscription.planName}</strong>
                    <small>
                      Criada em {new Date(subscription.createdAt).toLocaleDateString('pt-BR')} ·{' '}
                      {billingLabels[subscription.billingType] || subscription.billingType}
                    </small>
                  </div>
                  <span className={`status status-${subscription.status}`}>
                    {labels[subscription.status] || subscription.status}
                  </span>
                </div>
              ))}
            </div>
          </article>

          <article className="account-card account-wide">
            <h2>Ativar esta máquina</h2>
            {portal.machineFingerprint && (
              <div className="notice">
                <strong>Uma máquina está ativa</strong>
                <p>Identificador: {portal.machineFingerprint.slice(0, 16)}…</p>
                <button className="danger-link" type="button" onClick={deactivate}>
                  Desativar para trocar de máquina
                </button>
              </div>
            )}
            <ol className="steps">
              <li>Baixe e extraia o pacote.</li>
              <li>
                Execute <code>AtlasCad-Identificador.exe</code>.
              </li>
              <li>Cole abaixo o conteúdo do arquivo criado na Área de Trabalho.</li>
              <li>
                Copie o código retornado e use <code>ATLAS_LICENSE_ACTIVATE</code> no AutoCAD.
              </li>
            </ol>
            <form className="form-stack" onSubmit={machine}>
              <label>
                Identificador da máquina
                <textarea
                  name="machineIdentifier"
                  rows={4}
                  required
                  disabled={!portal.canActivate || Boolean(portal.machineFingerprint)}
                />
              </label>
              <button
                className="button button-primary"
                disabled={!portal.canActivate || Boolean(portal.machineFingerprint)}
              >
                Gerar código de ativação
              </button>
            </form>
            {activation && (
              <div className="activation-code">
                <small>SEU CÓDIGO DE ATIVAÇÃO</small>
                <strong>{activation}</strong>
                <p>Uso único. Expira em 7 dias.</p>
              </div>
            )}
          </article>
        </div>
        {canCancel && (
          <button className="danger-link" onClick={cancel}>
            Cancelar assinatura
          </button>
        )}
      </div>
    </section>
  );
}
