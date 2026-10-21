import Link from 'next/link';
export function Header() {
  return (
    <header className="site-header">
      <div className="container header-inner">
        <Link href="/" className="brand" aria-label="ATLAS CAD início">
          <span className="brand-symbol">A</span>
          <span>
            ATLAS<span className="brand-light">CAD</span>
          </span>
        </Link>
        <nav className="nav" aria-label="Navegação principal">
          <Link href="/">Home</Link>
          <Link href="/bibliotecas">Bibliotecas</Link>
          <Link href="/documentacao">Documentação</Link>
          <Link href="/entrar">Minha conta</Link>
        </nav>
        <Link className="button button-small button-outline header-cta" href="/checkout/steelflow">
          Assinar SteelFlow <span aria-hidden>→</span>
        </Link>
      </div>
    </header>
  );
}
export function Footer() {
  return (
    <footer className="footer">
      <div className="container footer-grid">
        <div>
          <div className="brand">
            <span className="brand-symbol">A</span>
            <span>
              ATLAS<span className="brand-light">CAD</span>
            </span>
          </div>
          <p>Ferramentas para transformar trabalho repetitivo em fluxo de engenharia.</p>
        </div>
        <div>
          <strong>Explore</strong>
          <Link href="/bibliotecas">Bibliotecas</Link>
          <Link href="/documentacao">Documentação</Link>
        </div>
        <div>
          <strong>Produto</strong>
          <Link href="/bibliotecas/steelflow">SteelFlow</Link>
          <Link href="/documentacao/steelflow/licenciamento">Licenciamento</Link>
          <Link href="/entrar">Minha conta</Link>
        </div>
      </div>
      <div className="container footer-bottom">
        <span>© {new Date().getFullYear()} ATLAS CAD</span>
        <span>Construído para projetistas.</span>
      </div>
    </footer>
  );
}
