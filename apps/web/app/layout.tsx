import type { Metadata } from 'next';
import { SiteChrome } from '@/components/site-chrome';
import './globals.css';

export const metadata: Metadata = {
  title: {
    default: 'ATLAS CAD | Automação de precisão para AutoCAD',
    template: '%s | ATLAS CAD',
  },
  description: 'Bibliotecas, scripts e automações para otimizar fluxos profissionais no AutoCAD.',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR">
      <body>
        <SiteChrome>{children}</SiteChrome>
      </body>
    </html>
  );
}
