import type { Metadata } from 'next';
import Link from 'next/link';
import { ArrowLink, CadVisual, Eyebrow } from '@/components/ui';
import { product } from '@/lib/content';
export const metadata: Metadata = { title: 'Biblioteca MLINES' };
export default function Mlines() {
  return <>
    <section className="section page-top"><div className="container product-hero"><div><Eyebrow>BIBLIOTECA 001 / ATLAS CAD</Eyebrow><h1 className="page-title">MLINES<span className="accent">.</span></h1><p className="page-intro">{product.description}</p><div className="hero-actions"><ArrowLink href="/checkout">Assinar agora</ArrowLink><ArrowLink href="/documentacao/primeiro-uso" secondary>Como funciona</ArrowLink></div><p className="fineprint">Planos mensal e anual. Pagamento seguro via Asaas.</p></div><CadVisual /></div></section>
    <section className="section section-dark"><div className="container"><div className="section-heading"><Eyebrow>FUNCIONALIDADES</Eyebrow><h2>Do eixo à representação.</h2><p>Uma experiência integrada ao desenho para reduzir etapas operacionais.</p></div><div className="capability-grid">{product.features.map((feature,index)=><div className="capability" key={feature}><span>0{index+1}</span><h3>{feature}</h3></div>)}</div></div></section>
    <section className="section"><div className="container split"><div><Eyebrow>COMPATIBILIDADE</Eyebrow><h2>Projetado para o seu ambiente de trabalho.</h2><p>A versão comercial atual foi validada para AutoCAD 2026.1.2 ou mais recente no Windows.</p></div><div className="info-panel"><h3>Requisitos</h3>{product.requirements.map(requirement=><div className="requirement" key={requirement}><span>✓</span><p>{requirement}</p></div>)}<Link href="/documentacao/instalacao" className="text-link">Ver instalação completa →</Link></div></div></section>
    <section className="section section-cta"><div className="container cta-content"><Eyebrow>MLINES / BIBLIOTECA 001</Eyebrow><h2>Pronto para automatizar?</h2><p>Assine, baixe e ative a sua máquina em um único fluxo.</p><div className="hero-actions center"><ArrowLink href="/checkout">Escolher plano</ArrowLink><ArrowLink href="/documentacao" secondary>Documentação</ArrowLink></div></div></section>
  </>;
}
