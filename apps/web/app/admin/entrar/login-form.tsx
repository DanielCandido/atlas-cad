'use client';
import { FormEvent, useState } from 'react';
import { useRouter } from 'next/navigation';

export function AdminLoginForm() {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError('');
    const response = await fetch('/api/admin/auth/login', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(Object.fromEntries(new FormData(event.currentTarget))),
    });
    const result = await response.json();
    setBusy(false);
    if (!response.ok) return setError(result.message || 'Não foi possível entrar.');
    router.replace('/admin');
    router.refresh();
  }
  return (
    <section className="section page-top">
      <div className="container narrow admin-login">
        <span className="eyebrow">ACESSO RESTRITO</span>
        <h1 className="page-title">
          Gerenciador <span>ATLAS.</span>
        </h1>
        <p className="page-intro">
          Entre com sua conta administrativa para gerenciar clientes, licenças e dispositivos.
        </p>
        <form className="form-stack account-card" onSubmit={submit}>
          {error && (
            <p className="form-error" role="alert">
              {error}
            </p>
          )}
          <label>
            E-mail
            <input type="email" name="email" autoComplete="username" required />
          </label>
          <label>
            Senha
            <input
              type="password"
              name="password"
              autoComplete="current-password"
              required
              minLength={12}
            />
          </label>
          <button className="button button-primary" disabled={busy}>
            {busy ? 'Entrando…' : 'Entrar'}
          </button>
        </form>
      </div>
    </section>
  );
}
