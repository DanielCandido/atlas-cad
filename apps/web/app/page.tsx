import Link from 'next/link';
import { ArrowLink, CadVisual, Eyebrow } from '@/components/ui';
export default function Home() {
  return (
    <>
      <section className="hero">
        <div className="container hero-grid">
          <div>
            <Eyebrow>ENGENHARIA + AUTOMAÇÃO</Eyebrow>
            <h1>
              Menos comandos repetitivos.
              <br />
              <span>Mais engenharia.</span>
            </h1>
            <p className="lead">
              O ATLAS CAD transforma tarefas manuais do AutoCAD em fluxos inteligentes, consistentes
              e feitos para quem projeta.
            </p>
            <div className="hero-actions">
              <ArrowLink href="/bibliotecas/steelflow">Explorar biblioteca SteelFlow</ArrowLink>
              <ArrowLink href="/documentacao" secondary>
                Ver documentação
              </ArrowLink>
            </div>
            <div className="hero-note">
              <span className="pulse" />
              SteelFlow disponível com planos mensal e anual
            </div>
          </div>
          <CadVisual />
        </div>
      </section>
      <section className="section section-dark">
        <div className="container">
          <div className="section-heading">
            <Eyebrow>O QUE MUDA NO SEU FLUXO</Eyebrow>
            <h2>
              Seu tempo vale mais que
              <br />
              tarefas repetitivas.
            </h2>
            <p>
              Ferramentas projetadas para reduzir etapas manuais e trazer consistência aos desenhos.
            </p>
          </div>
          <div className="feature-grid">
            <article className="feature-card">
              <span className="feature-icon">01</span>
              <h3>Automação prática</h3>
              <p>Transforme operações frequentes em comandos objetivos dentro do AutoCAD.</p>
            </article>
            <article className="feature-card">
              <span className="feature-icon">02</span>
              <h3>Padronização</h3>
              <p>Use estilos e regras consistentes para reduzir ajustes repetitivos.</p>
            </article>
            <article className="feature-card">
              <span className="feature-icon">03</span>
              <h3>Fluxo de engenharia</h3>
              <p>Bibliotecas pensadas para projetos e representações técnicas.</p>
            </article>
          </div>
        </div>
      </section>
      <section className="section">
        <div className="container spotlight">
          <div>
            <Eyebrow>BIBLIOTECA EM DESTAQUE</Eyebrow>
            <h2>
              Conheça o <span className="accent">SteelFlow.</span>
            </h2>
            <p>
              Automação para representação de perfis metálicos, geometria e estilos de planta e
              corte.
            </p>
            <Link className="text-link" href="/bibliotecas/steelflow">
              Explorar funcionalidades <span>→</span>
            </Link>
          </div>
          <div className="spotlight-panel">
            <div className="spotlight-label">ATLAS LIBRARY / 001</div>
            <div className="spotlight-mark">
              S<span>TEELFLOW</span>
            </div>
            <div className="spotlight-meta">
              <span>DESENHO TÉCNICO</span>
              <span>PERFIS METÁLICOS</span>
            </div>
          </div>
        </div>
      </section>
      <section className="section section-cta">
        <div className="container cta-content">
          <Eyebrow>CONHECIMENTO ABERTO</Eyebrow>
          <h2>Documentação por biblioteca.</h2>
          <p>Encontre instalação, uso e solução de problemas no contexto certo.</p>
          <ArrowLink href="/documentacao" secondary>
            Acessar documentação
          </ArrowLink>
        </div>
      </section>
    </>
  );
}
