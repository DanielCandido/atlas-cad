import type { Metadata } from 'next';
import Link from 'next/link';
import { Eyebrow } from '@/components/ui';
export const metadata: Metadata = { title: 'Bibliotecas' };
export default function Libraries() {
  return (
    <section className="section page-top">
      <div className="container">
        <Eyebrow>ECOSSISTEMA ATLAS</Eyebrow>
        <h1 className="page-title">
          Bibliotecas para <span>produzir melhor.</span>
        </h1>
        <p className="page-intro">
          Cada biblioteca reúne ferramentas para uma etapa específica do seu fluxo de trabalho.
          Começamos pelo universo MLINE.
        </p>
        <div className="library-grid">
          <Link href="/bibliotecas/mlines" className="library-card">
            <div className="library-art">
              <span className="library-monogram">
                M<span>LINES</span>
              </span>
              <span className="library-art-detail">ATLAS / 001</span>
            </div>
            <div className="library-content">
              <span className="chip">PRIMEIRA BIBLIOTECA</span>
              <h2>
                MLINES <span>↗</span>
              </h2>
              <p>
                Automação de representação de perfis metálicos, estilos de planta e corte e
                conversão de entidades.
              </p>
              <div className="library-bottom">
                AutoCAD · Windows <span>Saiba mais →</span>
              </div>
            </div>
          </Link>
          <div className="library-card future-card">
            <div className="future-icon">+</div>
            <h2>Novas bibliotecas</h2>
            <p>
              O catálogo ATLAS CAD foi pensado para receber novos módulos de automação no futuro.
            </p>
            <span className="chip muted">EM PLANEJAMENTO</span>
          </div>
        </div>
      </div>
    </section>
  );
}
