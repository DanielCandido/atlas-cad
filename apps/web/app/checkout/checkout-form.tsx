'use client';
import { FormEvent, useState } from 'react';
import { useRouter } from 'next/navigation';

export function CheckoutForm({
  librarySlug,
  plans,
}: {
  librarySlug: string;
  plans: { code: string; name: string; price: number; cycle: string }[];
}) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [paymentMethod, setPaymentMethod] = useState('CREDIT_CARD');
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
  return (
    <form className="checkout-summary form-stack" onSubmit={submit}>
      <input type="hidden" name="librarySlug" value={librarySlug} />
      <div className="checkout-head">DADOS DA ASSINATURA</div>
      <label>
        Nome completo
        <input name="name" autoComplete="name" required minLength={3} />
      </label>
      <label>
        E-mail
        <input name="email" type="email" autoComplete="email" required />
      </label>
      <label>
        CPF ou CNPJ
        <input name="taxId" inputMode="numeric" required minLength={11} />
      </label>
      <label>
        Senha da conta
        <input
          name="password"
          type="password"
          autoComplete="current-password"
          required
          minLength={10}
        />
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
        <>
          <label>
            Telefone com DDD
            <input name="phone" type="tel" required />
          </label>
          <label>
            CEP
            <input name="postalCode" inputMode="numeric" required minLength={8} />
          </label>
          <label>
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
        </>
      )}
      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}
      <button className="button button-primary" disabled={busy}>
        {busy ? 'Criando assinatura…' : 'Continuar para o pagamento →'}
      </button>
      <small>Use a mesma senha para adicionar outra biblioteca à sua conta existente.</small>
    </form>
  );
}
