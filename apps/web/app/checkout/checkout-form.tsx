'use client';

import { FormEvent, useState } from 'react';
import { useRouter } from 'next/navigation';

export function CheckoutForm({
  monthlyPrice,
  yearlyPrice,
}: {
  monthlyPrice: string;
  yearlyPrice: string;
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
        Crie uma senha
        <input
          name="password"
          type="password"
          autoComplete="new-password"
          required
          minLength={10}
        />
      </label>
      <label>
        Plano
        <select name="planCode" defaultValue="monthly">
          <option value="monthly">
            Mensal — R$ {Number(monthlyPrice).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
          </option>
          <option value="yearly">
            Anual — R$ {Number(yearlyPrice).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
          </option>
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
      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}
      <button className="button button-primary" disabled={busy}>
        {busy ? 'Criando assinatura…' : 'Continuar para o pagamento →'}
      </button>
      <small>
        No cartão, você será direcionado ao ambiente seguro do Asaas. O ATLAS CAD não recebe nem
        armazena os dados do seu cartão.
      </small>
    </form>
  );
}
