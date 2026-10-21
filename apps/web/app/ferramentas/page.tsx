import type { Metadata } from 'next';
import Link from 'next/link';
import { Eyebrow } from '@/components/ui';

export const metadata: Metadata = { title: 'Ferramentas' };

const tools = [
  {
    href: '/ferramentas/conferencia-estrutural',
    code: 'STR-01',
    title: 'Conferência Estrutural',
    description:
      'Pré-dimensionamento de sapatas, blocos, vigas, pilares e lajes conforme parâmetros da NBR 6118.',
    tags: ['NBR 6118', 'CA-50', 'Excel'],
  },
  {
    href: '/ferramentas/quantitativo',
    code: 'QDXF-02',
    title: 'Quantitativo DXF',
    description:
      'Leitura de pranchas DXF para quantitativos de concreto, formas e elementos complementares.',
    tags: ['DXF', 'Canvas CAD', 'Excel'],
  },
];

export default function ToolsPage() {
  return (
    <section className="section page-top tools-index">
      <div className="container">
        <Eyebrow>ATLAS LABS · ENGENHARIA APLICADA</Eyebrow>
        <h1 className="page-title">
          Ferramentas técnicas <span>em evolução.</span>
        </h1>
        <p className="page-intro">
          Utilitários experimentais integrados ao ecossistema Atlas CAD. Os cálculos devem ser
          conferidos por profissional habilitado antes de uso em projeto.
        </p>
        <div className="tools-grid">
          {tools.map((tool) => (
            <Link href={tool.href} className="tool-card" key={tool.href}>
              <div className="tool-card-top">
                <span className="tool-code">{tool.code}</span>
                <span className="status status-active">Em andamento</span>
              </div>
              <div className="tool-icon">⌁</div>
              <h2>{tool.title}</h2>
              <p>{tool.description}</p>
              <div className="tool-tags">
                {tool.tags.map((tag) => (
                  <span key={tag}>{tag}</span>
                ))}
              </div>
              <div className="tool-card-link">
                Abrir ferramenta <span>→</span>
              </div>
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}
