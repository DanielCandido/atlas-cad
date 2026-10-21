'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { usePathname } from 'next/navigation';

export function Header() {
  const pathname = usePathname();
  const [isLoggedOut, setIsLoggedOut] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    setIsLoggedOut(false);
    void fetch('/api/portal', { cache: 'no-store', signal: controller.signal })
      .then((response) => setIsLoggedOut(response.status === 401))
      .catch(() => undefined);
    return () => controller.abort();
  }, [pathname]);

  return (
    <header className="site-header">
      <div className="container header-inner">
        <Link href="/" className="brand" aria-label="Atlas CAD início">
          <span className="brand-symbol">CA</span>
          <span>
            ATLAS <span className="brand-light">CAD</span>
          </span>
        </Link>
        <nav className="nav" aria-label="Navegação principal">
          <Link href="/bibliotecas">Marketplace</Link>
          <Link href="/ferramentas">Ferramentas</Link>
          <Link href="/documentacao">Documentação</Link>
          <Link href="/minha-conta">Minhas assinaturas</Link>
        </nav>
        {isLoggedOut && (
          <Link className="button button-small button-outline header-cta" href="/entrar">
            Acessar conta <span aria-hidden>→</span>
          </Link>
        )}
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
            <span className="brand-symbol">CA</span>
            <span>
              ATLAS <span className="brand-light">CAD</span>
            </span>
          </div>
          <p>Automação de precisão para fluxos CAD profissionais.</p>
        </div>
        <div>
          <strong>Explore</strong>
          <Link href="/bibliotecas">Bibliotecas</Link>
          <Link href="/ferramentas">Ferramentas</Link>
          <Link href="/documentacao">Documentação</Link>
        </div>
        <div>
          <strong>Produto</strong>
          <Link href="/bibliotecas/steelflow">Marketplace</Link>
          <Link href="/documentacao/steelflow/licenciamento">Licenciamento</Link>
          <Link href="/entrar">Minha conta</Link>
        </div>
      </div>
      <div className="container footer-bottom">
        <span>© {new Date().getFullYear()} ATLAS CAD</span>
        <span>Precision Engineering System · AutoCAD 2021–2026</span>
      </div>
    </footer>
  );
}
