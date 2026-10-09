# ATLAS CAD — protótipo incremental

Scaffold para comercialização futura de automações AutoLISP/MLN por plugin AutoCAD com licenciamento. **Não é produto pronto e não protege arquivos ainda.**

## Pré-requisitos
- Node.js 20+ e npm
- .NET SDK 10 para core/AutoCAD 2026; .NET Framework 4.8 Developer Pack para o adaptador 2022
- Docker + Docker Compose (opcional para banco)
- AutoCAD instalado e referências da API Autodesk para compilar/testar plugins; **não incluídas**

## Iniciar
1. `cp .env.example .env` e altere a senha local.
2. `docker compose up -d` para PostgreSQL. Banco está reservado para implementação futura; API v0.1 usa dados demonstrativos em memória.
3. `cd apps/licensing-api && dotnet run` (porta HTTP definida no launchSettings).
4. `cd apps/web && npm install && npm run dev` (http://localhost:3000).
5. `dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj` (quando SDK disponível).

## Estado atual
- API: `/health`, `/api/products`, `/api/licenses/demo-status` (somente demonstração, sem emissão de licença).
- Web: landing page inicial sem checkout nem autenticação.
- Core: contrato seguro do bridge e parser/normalizador MLN do M2.
- AutoCAD 2026: `ATLAS_DRAW_MLINE` validado no caminho feliz e `ATLAS_ENSURE_STYLE` implementado, aguardando validação de runtime.
- Catálogo: ferramenta offline valida MLN e bloqueia nomes duplicados/conflitantes; catálogo protegido ainda não foi implementado.
- License Manager: CLI demonstrativa, **não gera fingerprint real**.

## Segurança
Não colocar segredos, MLN, LSP proprietários ou chaves privadas no Git. Nunca utilizar `/api/licenses/demo-status` como autorização de produto. Ver `docs/security.md`.

## Próximos passos
1. Validar `ATLAS_ENSURE_STYLE` no AutoCAD 2026.
2. Resolver explicitamente as colisões do catálogo MLN privado.
3. Concluir o M2 e integrar as fronteiras do STEELFLOW no M3.
4. Tratar licenciamento e proteção somente nos marcos M4/M5.


## Opção B — protótipo incremental
Veja `docs/option-b-implementation.md` e `cad/atlas-cad-bridge/README.md`. O pacote não contém os arquivos proprietários recebidos.
