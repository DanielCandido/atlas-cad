import type { Metadata } from 'next';
import { Header, Footer } from '@/components/header';
import './globals.css';
export const metadata: Metadata = {
  title: { default: 'ATLAS CAD | Automação para projetistas', template: '%s | ATLAS CAD' },
  description: 'Bibliotecas e automações para otimizar o fluxo de trabalho no AutoCAD.',
};
export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR">
      <body>
        <Header />
        <main>{children}</main>
        <Footer />
      </body>
    </html>
  );
}
