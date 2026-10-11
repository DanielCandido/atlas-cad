import type { Metadata } from 'next';
import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';
import { Eyebrow } from '@/components/ui';
import { CheckoutForm } from './checkout-form';

export const metadata: Metadata = { title: 'Assinar MLINES' };
export const dynamic = 'force-dynamic';

export default async function Checkout() {
  if ((await cookies()).has('atlas_session')) redirect('/minha-conta');
  const monthlyPrice = process.env.ATLAS_PLAN_MONTHLY_PRICE || '79.90';
  const yearlyPrice = process.env.ATLAS_PLAN_YEARLY_PRICE || '799.00';
  return (
    <section className="section page-top">
      <div className="container checkout-grid">
        <div>
          <Eyebrow>ATLAS CAD / ASSINATURA</Eyebrow>
          <h1 className="page-title">
            Comece com o <span>MLINES.</span>
          </h1>
          <p className="page-intro">
            Crie sua conta, escolha o plano e pague no ambiente seguro do Asaas. O download e a
            ativação são liberados automaticamente após a confirmação.
          </p>
          <div className="notice">
            <strong>Compra segura</strong>
            <p>
              O ATLAS CAD não recebe nem armazena dados bancários. PIX e boleto são emitidos pelo
              Asaas.
            </p>
          </div>
        </div>
        <CheckoutForm monthlyPrice={monthlyPrice} yearlyPrice={yearlyPrice} />
      </div>
    </section>
  );
}
