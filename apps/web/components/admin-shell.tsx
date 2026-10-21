'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';

export function AdminShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();

  if (pathname === '/admin/entrar') {
    return (
      <main className="admin-auth-shell">
        <div className="admin-auth-brand">
          <span className="brand-symbol">CA</span>
          <div>
            <strong>ATLAS CONSOLE</strong>
            <small>PLATFORM MANAGEMENT</small>
          </div>
        </div>
        {children}
      </main>
    );
  }

  async function logout() {
    await fetch('/api/admin/auth/logout', { method: 'POST' });
    router.replace('/admin/entrar');
    router.refresh();
  }

  const links = [
    { href: '/admin', label: 'Usuários & Licenças', icon: '◉' },
    { href: '/admin/bibliotecas', label: 'Bibliotecas & Versões', icon: '▦' },
  ];

  return (
    <div className="admin-shell">
      <aside className="admin-sidebar">
        <div className="admin-brand">
          <span className="brand-symbol">CA</span>
          <div>
            <strong>ATLAS CONSOLE</strong>
            <small>ADMINISTRATION</small>
          </div>
        </div>
        <div className="admin-nav-label">GESTÃO DA PLATAFORMA</div>
        <nav className="admin-nav" aria-label="Navegação administrativa">
          {links.map((link) => {
            const active =
              link.href === '/admin'
                ? pathname === link.href || pathname.startsWith('/admin/usuarios/')
                : pathname.startsWith(link.href);
            return (
              <Link key={link.href} href={link.href} className={active ? 'active' : ''}>
                <span>{link.icon}</span>
                {link.label}
              </Link>
            );
          })}
        </nav>
        <div className="admin-system-status">
          <span className="pulse" />
          <div>
            <strong>Sistema operacional</strong>
            <small>Serviços sincronizados</small>
          </div>
        </div>
        <button className="admin-logout" onClick={logout}>
          <span>↪</span> Encerrar sessão
        </button>
        <div className="admin-build">
          <span>v2026 Engine</span>
          <strong>BUILD 981</strong>
        </div>
      </aside>
      <div className="admin-workspace">
        <header className="admin-topbar">
          <div>
            <span className="admin-topbar-kicker">ATLAS CAD</span>
            <strong>Gestão da plataforma</strong>
          </div>
          <span className="status status-active">● Ambiente online</span>
        </header>
        <main className="admin-content">{children}</main>
      </div>
    </div>
  );
}
