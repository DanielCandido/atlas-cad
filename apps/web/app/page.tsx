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
              e feitos para o dia a dia de quem projeta.
            </p>
            <div className="hero-actions">
              <ArrowLink href="/bibliotecas/mlines">Explorar biblioteca MLINES</ArrowLink>
              <ArrowLink href="/documentacao" secondary>
                Ver documentação
              </ArrowLink>
            </div>
            <div className="hero-note">
              <span className="pulse" />
              MLINES disponível com planos mensal e anual
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
              <br /> tarefas repetitivas.
            </h2>
            <p>
              Um conjunto de ferramentas projetado para reduzir etapas manuais e trazer consistência
              aos desenhos.
            </p>
          </div>
          <div className="feature-grid">
            <article className="feature-card">
              <span className="feature-icon">⌁</span>
              <h3>Automação prática</h3>
              <p>
                Transforme operações frequentes em comandos objetivos, dentro do ambiente que você
                já usa.
              </p>
            </article>
            <article className="feature-card">
              <span className="feature-icon">▤</span>
              <h3>Padronização</h3>
              <p>
                Use estilos e regras consistentes para reduzir ajustes repetitivos durante o
                detalhamento.
              </p>
            </article>
            <article className="feature-card">
              <span className="feature-icon">◇</span>
              <h3>Fluxo de engenharia</h3>
              <p>
                Ferramentas pensadas para projetos e representações técnicas, não para tarefas
                genéricas.
              </p>
            </article>
          </div>
        </div>
      </section>
      <section className="section">
        <div className="container spotlight">
          <div>
            <Eyebrow>PRIMEIRA BIBLIOTECA</Eyebrow>
            <h2>
              Conheça o <span className="accent">MLINES.</span>
            </h2>
            <p>
              Uma biblioteca voltada à representação de perfis metálicos em AutoCAD, com automações
              de geometria e estilos para planta e corte.
            </p>
            <Link className="text-link" href="/bibliotecas/mlines">
              Explorar funcionalidades <span>↗</span>
            </Link>
          </div>
          <div className="spotlight-panel">
            <div className="spotlight-label">ATLAS LIBRARY / 001</div>
            <div className="spotlight-mark">
              M<span>LINE</span>
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
          <h2>Aprenda no seu ritmo.</h2>
          <p>
            Uma documentação organizada como wiki, preparada para crescer com cada nova biblioteca.
          </p>
          <ArrowLink href="/documentacao" secondary>
            Acessar documentação
          </ArrowLink>
        </div>
      </section>
    </>
  );
}
