# Arquitetura V1

- Web Next.js: catálogo, conta e compras (futuro).
- API ASP.NET Core: produtos, pedidos, ativação, revogação, refresh (somente endpoints demo nesta versão).
- PostgreSQL: persistência futura de usuários, pedidos, licenças, dispositivos e eventos.
- License Manager: par de chaves por instalação e ativação (futuro).
- Plugins AutoCAD: builds separados net48 / net8, adaptadores dependentes de Autodesk (futuro).
- Core netstandard2.0: normalização e regras independentes do AutoCAD.

## Plano incremental
1. Adicionar `MlineStyle` e converter uma LINE real.
2. Parser de MLN com testes contra estilos originais.
3. Assinaturas Ed25519 e prova de posse de chave do dispositivo.
4. Migrations e endpoints reais de licença.
5. Checkout, portal, instalador e atualização.
