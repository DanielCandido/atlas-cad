import type { Metadata } from 'next';
import Link from 'next/link';
import { ArrowLink, CadVisual, Eyebrow } from '@/components/ui';
import { product } from '@/lib/content';
export const metadata: Metadata = { title: 'Biblioteca MLINES' };
export default function Mlines() {
  return (
    <>
      <section className="section page-top">
        <div className="container product-hero">
          <div>
            <Eyebrow>BIBLIOTECA 001 / ATLAS CAD</Eyebrow>
            <h1 className="page-title">
              MLINES<span className="accent">.</span>
            </h1>
            <p className="page-intro">{product.description}</p>
            <div className="hero-actions">
              <ArrowLink href="/checkout">Adquirir biblioteca</ArrowLink>
              <ArrowLink href="/documentacao/primeiro-uso" secondary>
                Como funciona
              </ArrowLink>
            </div>
            <p className="fineprint">
              Pagamento ainda não habilitado. O fluxo de checkout está preparado para integração.
            </p>
          </div>
          <CadVisual />
        </div>
      </section>
      <section className="section section-dark">
        <div className="container">
          <div className="section-heading">
            <Eyebrow>FUNCIONALIDADES</Eyebrow>
            <h2>Do eixo à representação.</h2>
            <p>Uma experiência integrada ao desenho para reduzir etapas operacionais.</p>
          </div>
          <div className="capability-grid">
            {product.features.map((f, i) => (
              <div className="capability" key={f}>
                <span>0{i + 1}</span>
                <h3>{f}</h3>
              </div>
            ))}
          </div>
        </div>
      </section>
      <section className="section">
        <div className="container split">
          <div>
            <Eyebrow>COMPATIBILIDADE</Eyebrow>
            <h2>Projetado para seu ambiente de trabalho.</h2>
            <p>
              O objetivo é suportar AutoCAD 2022–2026 para Windows, com validação por versão antes
              da disponibilidade comercial.
            </p>
          </div>
          <div className="info-panel">
            <h3>Requisitos previstos</h3>
            {product.requirements.map((r) => (
              <div className="requirement" key={r}>
                <span>✓</span>
                <p>{r}</p>
              </div>
            ))}
            <Link href="/documentacao/requisitos" className="text-link">
              Ver requisitos completos ↗
            </Link>
          </div>
        </div>
      </section>
      <section className="section section-cta">
        <div className="container cta-content">
          <Eyebrow>MLINES / BIBLIOTECA 001</Eyebrow>
          <h2>Pronto para conhecer o fluxo?</h2>
          <p>Explore a documentação ou avance para a página de aquisição.</p>
          <div className="hero-actions center">
            <ArrowLink href="/checkout">Iniciar aquisição</ArrowLink>
            <ArrowLink href="/documentacao" secondary>
              Documentação
            </ArrowLink>
          </div>
        </div>
      </section>
    </>
  );
}
