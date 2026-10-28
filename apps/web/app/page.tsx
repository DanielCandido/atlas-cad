import Link from 'next/link';
import { ProductVideo } from '@/components/product-video';

export default function Home() {
  return (
    <div className="hm-page hm-home">
      <section className="hm-hero hm-home-hero">
        <div className="hm-container hm-hero__grid">
          <div className="hm-hero__copy">
            <h1>Automação que trabalha dentro do AutoCAD.</h1>
            <p>
              Bibliotecas para engenheiros e desenhistas produzirem com mais precisão e menos
              repetição.
            </p>
            <div className="hm-actions">
              <Link className="hm-button hm-button--primary" href="/bibliotecas">
                Explorar bibliotecas
              </Link>
              <Link className="hm-button hm-button--quiet" href="/documentacao">
                Consultar documentação
              </Link>
            </div>
          </div>
          <ProductVideo />
        </div>
      </section>

      <section className="hm-proof" aria-label="Indicadores informados pelo projeto">
        <dl className="hm-container hm-figures">
          <div>
            <dt>Tempo economizado</dt>
            <dd>+45.000 horas</dd>
            <p>em pranchas técnicas</p>
          </div>
          <div>
            <dt>Precisão declarada</dt>
            <dd>99,8%</dd>
            <p>em rotinas conforme a ABNT</p>
          </div>
          <div>
            <dt>Compatibilidade</dt>
            <dd>6 versões</dd>
            <p>do AutoCAD, de 2021 a 2026</p>
          </div>
        </dl>
      </section>

      <section className="hm-section hm-catalog-showcase">
        <div className="hm-container">
          <div className="hm-section-head hm-section-head--split">
            <h2>Uma biblioteca para cada processo.</h2>
            <p>Comece pelo trabalho que mais se repete no seu projeto.</p>
          </div>

          <div className="hm-product-index">
            <Link className="hm-product-row hm-product-row--lead" href="/bibliotecas/steelflow">
              <span className="hm-product-row__mark" aria-hidden="true">
                SF
              </span>
              <span className="hm-product-row__body">
                <strong>SteelFlow</strong>
                <span>Perfis metálicos, geometrias e padronização de desenhos.</span>
              </span>
              <span className="hm-product-row__action">Conhecer o SteelFlow</span>
            </Link>
            <Link className="hm-product-row" href="/bibliotecas">
              <span className="hm-product-row__mark" aria-hidden="true">
                +
              </span>
              <span className="hm-product-row__body">
                <strong>Catálogo ATLAS CAD</strong>
                <span>Compare bibliotecas, recursos e planos em uma única conta.</span>
              </span>
              <span className="hm-product-row__action">Abrir o catálogo</span>
            </Link>
          </div>
        </div>
      </section>

      <section className="hm-section hm-workflow">
        <div className="hm-container hm-workflow__grid">
          <div className="hm-section-head">
            <h2>Do download ao desenho.</h2>
            <p>O hub cuida do entorno para a biblioteca fazer o trabalho técnico.</p>
          </div>
          <ol className="hm-workflow__list">
            <li>
              <h3>Instalação guiada</h3>
              <p>Baixe e configure cada biblioteca sem procurar arquivos dispersos.</p>
            </li>
            <li>
              <h3>Padrões consistentes</h3>
              <p>Reduza revisão e divergências com regras técnicas centralizadas.</p>
            </li>
            <li>
              <h3>Atualização contínua</h3>
              <p>Acompanhe compatibilidade, documentação e novas versões pelo mesmo hub.</p>
            </li>
          </ol>
        </div>
      </section>

      <section className="hm-close">
        <div className="hm-container hm-close__grid">
          <h2>Escolha a próxima tarefa para automatizar.</h2>
          <Link className="hm-button hm-button--paper" href="/bibliotecas">
            Ver bibliotecas disponíveis
          </Link>
        </div>
      </section>
    </div>
  );
}
