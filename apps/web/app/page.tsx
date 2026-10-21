import Link from 'next/link';
import { ArrowLink, CadVisual, Eyebrow } from '@/components/ui';

export default function Home() {
  return (
    <>
      <section className="hero">
        <div className="container hero-grid">
          <div>
            <Eyebrow>LISP · .NET · ARX · AutoCAD 2021–2026</Eyebrow>
            <h1>
              Automatize horas de desenho repetitivo <span>em segundos.</span>
            </h1>
            <p className="lead">
              Bibliotecas prontas de rotinas AutoLISP, scripts DCL e plugins .NET para arquitetos e
              engenheiros. Zero retrabalho, precisão dimensional absoluta.
            </p>
            <div className="hero-actions">
              <ArrowLink href="/bibliotecas">Explorar bibliotecas</ArrowLink>
              <ArrowLink href="/documentacao" secondary>
                Ver documentação
              </ArrowLink>
            </div>
            <div className="hero-note">
              <span className="pulse" /> Bibliotecas verificadas e atualizadas continuamente
            </div>
          </div>
          <CadVisual />
        </div>
      </section>

      <div className="container metrics-strip" aria-label="Indicadores da plataforma">
        <div className="metric-card">
          <span className="metric-icon">↗</span>
          <div>
            <strong>+45.000h</strong>
            <small>economizadas em pranchas técnicas</small>
          </div>
        </div>
        <div className="metric-card">
          <span className="metric-icon">✓</span>
          <div>
            <strong>99,8%</strong>
            <small>de precisão conforme ABNT</small>
          </div>
        </div>
        <div className="metric-card">
          <span className="metric-icon">▣</span>
          <div>
            <strong>2021–2026</strong>
            <small>suporte nativo a versões AutoCAD</small>
          </div>
        </div>
      </div>

      <section className="section section-dark">
        <div className="container">
          <div className="section-heading">
            <Eyebrow>Catálogo verificado</Eyebrow>
            <h2>Bibliotecas para produzir melhor.</h2>
            <p>
              Ferramentas especializadas para eliminar etapas manuais sem alterar seu fluxo de
              engenharia.
            </p>
          </div>
          <div className="library-grid">
            <Link href="/bibliotecas/steelflow" className="library-card">
              <div className="library-art">
                <span className="library-monogram">
                  S<span>TEELFLOW</span>
                </span>
                <span className="library-art-detail">ATLAS / 001</span>
              </div>
              <div className="library-content">
                <span className="chip">PUBLICADA · AUTOCAD</span>
                <h2>
                  SteelFlow <span>→</span>
                </h2>
                <p>
                  Automação técnica para perfis metálicos, geometrias e padronização de desenhos.
                </p>
                <div className="library-bottom">
                  AutoCAD · Windows <span>Ver detalhes →</span>
                </div>
              </div>
            </Link>
            <Link href="/bibliotecas" className="library-card">
              <div className="library-art">
                <span className="library-monogram">+</span>
                <span className="library-art-detail">CATÁLOGO CAD</span>
              </div>
              <div className="library-content">
                <span className="chip">NOVOS MÓDULOS</span>
                <h2>
                  Marketplace <span>→</span>
                </h2>
                <p>
                  Explore todas as bibliotecas publicadas e encontre a automação certa para seu
                  escritório.
                </p>
                <div className="library-bottom">
                  Licenças flexíveis <span>Explorar catálogo →</span>
                </div>
              </div>
            </Link>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <div className="section-heading">
            <Eyebrow>Engenharia sem atrito</Eyebrow>
            <h2>Precisão técnica, sem perder o ritmo.</h2>
            <p>
              Da instalação à atualização, a plataforma foi desenhada para equipes que dependem do
              AutoCAD todos os dias.
            </p>
          </div>
          <div className="feature-grid">
            <article className="feature-card">
              <span className="feature-icon">01</span>
              <h3>Automação prática</h3>
              <p>Comandos objetivos dentro do AutoCAD para tarefas que antes consumiam horas.</p>
            </article>
            <article className="feature-card">
              <span className="feature-icon">02</span>
              <h3>Padronização ABNT</h3>
              <p>Estilos e regras consistentes para reduzir revisão, retrabalho e divergências.</p>
            </article>
            <article className="feature-card">
              <span className="feature-icon">03</span>
              <h3>Atualizações contínuas</h3>
              <p>
                Compatibilidade acompanhada de documentação clara e novas versões centralizadas.
              </p>
            </article>
          </div>
        </div>
      </section>

      <section className="section section-cta">
        <div className="container cta-content">
          <Eyebrow>Seu risco: 14 dias de teste</Eyebrow>
          <h2>Pronto para acelerar seus projetos?</h2>
          <p>
            Conheça as bibliotecas, compare planos e comece com uma conta segura em poucos minutos.
          </p>
          <ArrowLink href="/bibliotecas" secondary>
            Criar conta gratuita
          </ArrowLink>
        </div>
      </section>
    </>
  );
}
