# Publicação completa do ATLAS CAD

Para o ambiente atual com `atlasengenharia.site`, Cloudflare Tunnel e
containers locais, siga primeiro
[CLOUDFLARE_TUNNEL_LOCAL.md](CLOUDFLARE_TUNNEL_LOCAL.md). O restante deste
documento descreve a publicação tradicional em servidor com Caddy.

Este é o checklist operacional para colocar site, cobrança, licenciamento e download no ar. Faça primeiro no Sandbox do Asaas e só troque para produção depois do teste ponta a ponta.

## 1. Contas, domínio e servidor

Você precisa de uma conta Asaas aprovada, um domínio e um servidor Linux com Docker Compose, backup externo e portas 80/443 abertas. Crie dois registros DNS apontando para o servidor: `seudominio.com.br` e `api.seudominio.com.br`. O Caddy incluído em `infra/` obtém e renova os certificados HTTPS.

Copie `infra/compose.production.yaml`, `infra/Caddyfile` e o repositório para o servidor. Dentro de `infra/`, crie (sem versionar):

- `.env.production`, com as variáveis abaixo;
- `secrets/server-signing-key.pk8`;
- `secrets/server-signing-public.json` (necessário apenas para o build do plugin, não para a API);
- `secrets/catalog-keys.json`;
- `artifacts/AtlasCad-autocad-2022-2024-win64.zip`;
- `artifacts/AtlasCad-autocad-2025-2026-1-1-win64.zip`;
- `artifacts/AtlasCad-autocad-2026-1-2-plus-win64.zip`;
- `artifacts/AtlasCad-Identificador.exe`.

Proteja `secrets/` para leitura exclusiva do usuário que executa Docker e mantenha uma cópia criptografada fora do servidor. Perder a chave privada impede renovar licenças; vazá-la exige rotação e novo build do plugin.

## 2. Gerar as chaves uma única vez

Em uma máquina administrativa segura:

```powershell
dotnet run --project apps/license-manager -- generate-signing-key secrets/server-signing-key.pk8 secrets/server-signing-public.json atlas-prod-2026-01
dotnet run --project apps/license-manager -- generate-catalog-key secrets/catalog-keys.json catalog-2026-01
```

O `keyId` do JSON público deve ser igual a `ATLAS_LICENSE_SIGNING_KEY_ID`. Não envie a chave `.pk8`, o chaveiro do catálogo ou a chave administrativa para clientes.

## 3. Configurar o ambiente

Use senhas aleatórias longas. Valores mínimos de `.env.production`:

```dotenv
ATLAS_DOMAIN=seudominio.com.br
POSTGRES_DB=atlascad
POSTGRES_USER=atlas
POSTGRES_PASSWORD=troque-por-uma-senha-forte
ATLAS_LICENSE_SIGNING_KEY_ID=atlas-prod-2026-01
ATLAS_LICENSE_ADMIN_KEY=troque-por-32-ou-mais-caracteres
ASAAS_BASE_URL=https://api-sandbox.asaas.com/v3
ASAAS_CHECKOUT_BASE_URL=https://sandbox.asaas.com/checkoutSession/show
ASAAS_API_KEY=sua-chave-do-sandbox
ASAAS_WEBHOOK_TOKEN=outro-segredo-aleatorio-com-32-ou-mais-caracteres
ATLAS_PUBLIC_WEB_URL=https://seudominio.com.br
ATLAS_PLAN_MONTHLY_PRICE=79.90
ATLAS_PLAN_YEARLY_PRICE=799.00
```

Não exponha PostgreSQL, API administrativa ou arquivos de segredo na internet.

## 4. Configurar o Asaas

No painel do Asaas, crie um webhook para `https://api.seudominio.com.br/api/v1/webhooks/asaas`, informe exatamente o mesmo token de `ASAAS_WEBHOOK_TOKEN` e habilite eventos de Checkout, cobrança e assinatura. No mínimo: `CHECKOUT_CREATED`, `CHECKOUT_PAID`, `CHECKOUT_CANCELED`, `CHECKOUT_EXPIRED`, `PAYMENT_CREATED`, `PAYMENT_CONFIRMED`, `PAYMENT_RECEIVED`, `PAYMENT_OVERDUE`, `PAYMENT_REFUNDED`, `PAYMENT_CHARGEBACK_REQUESTED`, `SUBSCRIPTION_CREATED`, `SUBSCRIPTION_INACTIVATED` e `SUBSCRIPTION_DELETED`.

Para cartão, o site cria um Checkout recorrente hospedado pelo Asaas. As URLs de retorno apenas conduzem o navegador de volta à Área do Cliente; nunca libere a licença pelo callback. A liberação ocorre exclusivamente após os eventos financeiros confirmados.

O backend valida o header `asaas-access-token` e grava o ID de cada evento antes de processá-lo. Reentregas do mesmo evento são ignoradas.

## 5. Gerar o pacote comercial

Compile o catálogo protegido e gere o release apontando para a API pública. Em produção, use certificado Authenticode:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-AtlasRelease.ps1 `
  -Version 1.0.0 `
  -OutputRoot infra/artifacts `
  -PublicLicenseApiUrl https://api.seudominio.com.br `
  -SigningPublicKeyPath secrets/server-signing-public.json `
  -CertificateThumbprint SEU_CERTIFICADO_AUTHENTICODE `
  -RequireCodeSigning
```

Renomeie/copiei o ZIP final para `infra/artifacts/AtlasCad-latest-win64.zip`. O ZIP contém o instalador e `AtlasCad-Identificador.exe`; não contém fontes privados ou chaves do servidor.

O build publica três opções: AutoCAD 2022–2024 (.NET Framework 4.8), AutoCAD
2025/2026 até Update 1.1 (.NET 8) e AutoCAD 2026 Update 1.2+ (.NET 10). Cada
ZIP inclui `AtlasCad-Identificador.exe` e `Desinstalar-AtlasCad.exe`. No AutoCAD
2026, confira a atualização em `ABOUT` antes de escolher o download.

## 6. Subir e verificar

```bash
cd infra
podman compose --env-file .env.production -f compose.production.yaml up -d --build
podman compose --env-file .env.production -f compose.production.yaml ps
curl https://api.seudominio.com.br/health
```

O primeiro start cria as tabelas no PostgreSQL. Configure backup diário do volume/banco e teste restauração antes de vender.

## 7. Teste obrigatório no Sandbox

Crie uma compra real de teste pelo site. Confirme, nesta ordem: cliente e assinatura aparecem no Asaas; a conta entra no portal; antes do pagamento não há download; o webhook libera o download; o identificador é aceito; o código ativa apenas a máquina que o gerou; o catálogo instala; a renovação funciona; a troca de plano reflete no Asaas; atraso/estorno suspende novas renovações; cancelamento revoga a ativação.

Depois do teste, troque `ASAAS_BASE_URL` para `https://api.asaas.com/v3` e
`ASAAS_CHECKOUT_BASE_URL` para `https://asaas.com/checkoutSession/show`, use a
chave de produção, recrie o webhook no ambiente de produção e repita uma
compra de baixo valor. Sandbox e produção têm chaves, clientes, assinaturas e
webhooks separados.

## 8. Rotina operacional

Monitore HTTP 5xx, fila de webhook interrompida no Asaas, espaço em disco, expiração de backup e validade TLS. Antes de publicar uma versão nova, gere e assine o ZIP, execute `scripts/Test-AtlasBundle.ps1`, substitua o artefato e mantenha a versão anterior para rollback. Nunca substitua a chave de assinatura sem publicar um cliente que confie na nova chave e planejar a rotação.
