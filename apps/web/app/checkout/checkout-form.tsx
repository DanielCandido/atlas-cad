'use client';

import { FormEvent, useState } from 'react';
import { useRouter } from 'next/navigation';

type AccountMode = 'new' | 'existing';
type SignedInAccount = { name: string; email: string };

export function CheckoutForm({
  librarySlug,
  plans,
}: {
  librarySlug: string;
  plans: { code: string; name: string; price: number; cycle: string }[];
}) {
  const router = useRouter();
  const [mode, setMode] = useState<AccountMode>('new');
  const [account, setAccount] = useState<SignedInAccount | null>(null);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [paymentMethod, setPaymentMethod] = useState('CREDIT_CARD');

  function changeMode(next: AccountMode) {
    setMode(next);
    setAccount(null);
    setError('');
  }

  async function signIn(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError('');
    const response = await fetch('/api/auth/login', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });
    const result = await response.json();
    setBusy(false);
    if (!response.ok) {
      setError(result.message || 'Não foi possível acessar sua conta.');
      return;
    }
    setAccount({ name: result.portal?.customerName || 'Cliente ATLAS CAD', email });
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError('');
    const data = Object.fromEntries(new FormData(event.currentTarget));
    const response = await fetch('/api/auth/checkout', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(data),
    });
    const result = await response.json();
    setBusy(false);
    if (!response.ok) {
      setError(result.message || 'Não foi possível iniciar a assinatura.');
      return;
    }
    if (result.paymentUrl) {
      window.location.assign(result.paymentUrl);
      return;
    }
    router.push('/minha-conta');
    router.refresh();
  }

  const accountTabs = (
    <div className="account-tabs" role="tablist" aria-label="Tipo de conta">
      <button
        type="button"
        role="tab"
        aria-selected={mode === 'new'}
        className={mode === 'new' ? 'active' : ''}
        onClick={() => changeMode('new')}
      >
        Criar uma conta
      </button>
      <button
        type="button"
        role="tab"
        aria-selected={mode === 'existing'}
        className={mode === 'existing' ? 'active' : ''}
        onClick={() => changeMode('existing')}
      >
        Já tenho conta
      </button>
    </div>
  );

  if (mode === 'existing' && !account) {
    return (
      <div className="checkout-summary">
        <div className="checkout-head">CONTA ATLAS CAD</div>
        {accountTabs}
        <form className="form-stack checkout-login" onSubmit={signIn}>
          <div className="checkout-step">
            <span>01</span>
            <div>
              <strong>Acesse sua conta</strong>
              <small>Suas bibliotecas atuais serão preservadas.</small>
            </div>
          </div>
          <label>
            E-mail
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="email"
              required
            />
          </label>
          <label>
            Senha
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
              required
              minLength={10}
            />
          </label>
          {error && (
            <p className="form-error" role="alert">
              {error}
            </p>
          )}
          <button className="button button-primary" disabled={busy}>
            {busy ? 'Entrando…' : 'Entrar e continuar'}
          </button>
          <small>Depois do login você escolherá o plano e a forma de pagamento.</small>
        </form>
      </div>
    );
  }

  return (
    <form className="checkout-summary form-stack" onSubmit={submit}>
      <input type="hidden" name="librarySlug" value={librarySlug} />
      <div className="checkout-head">DADOS DA ASSINATURA</div>
      {accountTabs}
      {account ? (
        <>
          <input type="hidden" name="name" value={account.name} />
          <input type="hidden" name="email" value={account.email} />
          <input type="hidden" name="password" value={password} />
          <div className="signed-account">
            <span className="signed-account-icon">✓</span>
            <div>
              <strong>{account.name}</strong>
              <small>{account.email}</small>
            </div>
            <button type="button" onClick={() => setAccount(null)}>
              Trocar
            </button>
          </div>
        </>
      ) : (
        <>
          <div className="checkout-step">
            <span>01</span>
            <div>
              <strong>Crie seu acesso</strong>
              <small>A conta será criada junto com a compra.</small>
            </div>
          </div>
          <label>
            Nome completo
            <input name="name" autoComplete="name" required minLength={3} />
          </label>
          <label>
            E-mail
            <input name="email" type="email" autoComplete="email" required />
          </label>
          <label>
            Crie uma senha
            <input
              name="password"
              type="password"
              autoComplete="new-password"
              required
              minLength={10}
            />
            <small>Mínimo de 10 caracteres.</small>
          </label>
        </>
      )}
      <div className="checkout-step">
        <span>02</span>
        <div>
          <strong>Faturamento</strong>
          <small>Dados necessários para emitir sua cobrança.</small>
        </div>
      </div>
      <label>
        CPF ou CNPJ
        <input name="taxId" inputMode="numeric" required minLength={11} />
      </label>
      <label>
        Plano
        <select name="planCode" defaultValue={plans[0]?.code}>
          {plans.map((plan) => (
            <option value={plan.code} key={plan.code}>
              {plan.name} — R${' '}
              {Number(plan.price).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
            </option>
          ))}
        </select>
      </label>
      <label>
        Pagamento
        <select
          name="billingType"
          value={paymentMethod}
          onChange={(event) => setPaymentMethod(event.target.value)}
        >
          <option value="CREDIT_CARD">Cartão de crédito — checkout seguro do Asaas</option>
          <option value="PIX">PIX</option>
          <option value="BOLETO">Boleto</option>
        </select>
      </label>
      {paymentMethod === 'CREDIT_CARD' && (
        <div className="billing-grid">
          <label>
            Telefone com DDD
            <input name="phone" type="tel" required />
          </label>
          <label>
            CEP
            <input name="postalCode" inputMode="numeric" required minLength={8} />
          </label>
          <label className="billing-wide">
            Logradouro
            <input name="address" required />
          </label>
          <label>
            Número
            <input name="addressNumber" required />
          </label>
          <label>
            Bairro
            <input name="province" required />
          </label>
        </div>
      )}
      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}
      <button className="button button-primary" disabled={busy}>
        {busy ? 'Preparando checkout…' : 'Continuar para pagamento →'}
      </button>
      <small>
        Pagamento processado no ambiente seguro do Asaas. Seus dados bancários não passam pelo ATLAS
        CAD.
      </small>
    </form>
  );
}
