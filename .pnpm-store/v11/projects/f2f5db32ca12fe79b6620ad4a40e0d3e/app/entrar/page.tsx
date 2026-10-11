import type { Metadata } from 'next';
import { LoginForm } from './login-form';
export const metadata: Metadata = { title: 'Entrar' };
export default function LoginPage() { return <section className="section page-top"><div className="container narrow"><h1 className="page-title">Área do <span>cliente.</span></h1><p className="page-intro">Acesse sua assinatura, downloads e ativação.</p><LoginForm /></div></section>; }
