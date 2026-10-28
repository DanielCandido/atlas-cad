'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { usePathname } from 'next/navigation';

const navigation = [
  { href: '/bibliotecas', label: 'Bibliotecas' },
  { href: '/ferramentas', label: 'Ferramentas' },
  { href: '/documentacao', label: 'Documentação' },
  { href: '/minha-conta', label: 'Minhas assinaturas' },
];

export function Header() {
  const pathname = usePathname();
  const [isLoggedOut, setIsLoggedOut] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    setIsLoggedOut(false);
    setMenuOpen(false);
    void fetch('/api/portal', { cache: 'no-store', signal: controller.signal })
      .then((response) => setIsLoggedOut(response.status === 401))
      .catch(() => undefined);
    return () => controller.abort();
  }, [pathname]);

  return (
    <header className="site-header">
      <div className="container header-inner">
        <Link href="/" className="brand" aria-label="Atlas CAD — início">
          <span className="brand-symbol">CA</span>
          <span>
            ATLAS <span className="brand-light">CAD</span>
          </span>
        </Link>
        <button
          className="mobile-menu-button"
          type="button"
          aria-label={menuOpen ? 'Fechar menu' : 'Abrir menu'}
          aria-expanded={menuOpen}
          aria-controls="main-navigation"
          onClick={() => setMenuOpen((value) => !value)}
        >
          <svg viewBox="0 0 24 24" width="21" height="21" fill="none" aria-hidden="true">
            {menuOpen ? (
              <path
                d="m6 6 12 12M18 6 6 18"
                stroke="currentColor"
                strokeWidth="1.8"
                strokeLinecap="round"
              />
            ) : (
              <path
                d="M4 7h16M4 12h16M4 17h16"
                stroke="currentColor"
                strokeWidth="1.8"
                strokeLinecap="round"
              />
            )}
          </svg>
        </button>
        <nav
          id="main-navigation"
          className={`nav${menuOpen ? ' nav-open' : ''}`}
          aria-label="Navegação principal"
        >
          {navigation.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              aria-current={pathname === item.href ? 'page' : undefined}
            >
              {item.label}
            </Link>
          ))}
        </nav>
        {isLoggedOut && (
          <Link className="button button-small button-outline header-cta" href="/entrar">
            Acessar conta <span aria-hidden="true">→</span>
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
          <Link href="/minha-conta">Minha conta</Link>
        </div>
      </div>
      <div className="container footer-bottom">
        <span>© {new Date().getFullYear()} ATLAS CAD</span>
        <span>Precision Engineering System · AutoCAD 2021–2026</span>
      </div>
    </footer>
  );
}
