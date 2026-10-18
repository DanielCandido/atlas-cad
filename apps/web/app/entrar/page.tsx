import type { Metadata } from 'next';
import { LoginForm } from './login-form';

export const metadata: Metadata = { title: 'Entrar' };

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<{ returnTo?: string }>;
}) {
  const requested = (await searchParams).returnTo || '';
  const returnTo =
    requested.startsWith('/') && !requested.startsWith('//') ? requested : '/minha-conta';
  return (
    <section className="section page-top">
      <div className="container narrow">
        <h1 className="page-title">
          Área do <span>cliente.</span>
        </h1>
        <p className="page-intro">Acesse sua assinatura, downloads e ativação.</p>
        <LoginForm returnTo={returnTo} />
      </div>
    </section>
  );
}
