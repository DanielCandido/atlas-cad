import type { Metadata } from 'next';
import Link from 'next/link';
import { Eyebrow } from '@/components/ui';
export const metadata: Metadata = { title: 'Aquisição MLINES' };
export default function Checkout() {
  return (
    <section className="section page-top">
      <div className="container checkout-grid">
        <div>
          <Eyebrow>ATLAS CAD / AQUISIÇÃO</Eyebrow>
          <h1 className="page-title">
            Adquira a biblioteca <span>MLINES.</span>
          </h1>
          <p className="page-intro">
            O fluxo comercial está preparado para receber um provedor de pagamentos. A compra online
            ainda não está disponível nesta versão de demonstração.
          </p>
          <div className="notice">
            <strong>Integração pendente</strong>
            <p>
              Antes de aceitar pagamentos, é necessário definir preço, modelo de licença,
              autenticação e webhook do gateway. Nenhum pagamento é processado nesta página.
            </p>
          </div>
          <Link href="/bibliotecas/mlines" className="text-link">
            ← Voltar para MLINES
          </Link>
        </div>
        <aside className="checkout-summary">
          <div className="checkout-head">RESUMO DO PRODUTO</div>
          <h2>ATLAS CAD / MLINES</h2>
          <p>Biblioteca de automações para AutoCAD</p>
          <div className="checkout-divider" />
          <div className="checkout-row">
            <span>Produto</span>
            <strong>MLINES</strong>
          </div>
          <div className="checkout-row">
            <span>Preço</span>
            <strong>A definir</strong>
          </div>
          <div className="checkout-divider" />
          <button className="button button-disabled" disabled>
            Pagamento indisponível
          </button>
          <small>Não solicitamos dados de cartão nesta versão.</small>
        </aside>
      </div>
    </section>
  );
}
