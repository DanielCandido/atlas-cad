# Ambiente de teste com Cloudflare Tunnel

Esta é a topologia recomendada para testar o fluxo completo usando o domínio
`atlasengenharia.site` e containers executados na sua própria máquina:

```text
Internet
   |
Cloudflare (DNS, HTTPS, WAF)
   |
Cloudflare Tunnel — conexão iniciada de dentro para fora
   |
   +-- atlasengenharia.site     -> http://web:3000
   +-- api.atlasengenharia.site -> http://api:8080
                                         |
                                         +-- postgres:5432
```

Nenhuma porta precisa ser aberta no roteador. PostgreSQL, API e site não têm
portas públicas no Compose principal. Enquanto o computador ou o Tunnel estiver
desligado, os endereços públicos ficarão indisponíveis.

## 1. Pré-requisitos

- `atlasengenharia.site` ativo na sua conta Cloudflare e usando os
  nameservers da Cloudflare;
- Podman Desktop, uma Podman Machine em execução e um provedor Compose;
- conta Asaas Sandbox e sua chave de API;
- .NET SDK 10 para gerar as chaves do ATLAS;
- saída da rede local liberada para HTTPS e para a porta `7844` TCP/UDP usada
  pelo `cloudflared`.

Use o arquivo `infra/compose.tunnel.yaml`. O `compose.yaml` da raiz continua
sendo o ambiente mínimo de desenvolvimento do banco.

No Windows, valide a Podman Machine e o provedor Compose antes de continuar:

```powershell
podman machine start
podman info
podman compose version
```

`podman compose` é um wrapper para um provedor Compose externo. Se o último
comando falhar, abra **Podman Desktop > Settings > Resources > Compose** e
instale/configure o provedor antes de subir o projeto.

Para compilar o site e a API dentro da VM, recomenda-se pelo menos 6 GiB de RAM.
Se o build morrer por falta de memória, ajuste com a aplicação parada:

```powershell
podman machine stop
podman machine set --memory 6144 --cpus 4
podman machine start
```

## 2. Preparar os arquivos locais

Na raiz do repositório, execute no PowerShell:

```powershell
Copy-Item infra/.env.tunnel.example infra/.env.tunnel
New-Item -ItemType Directory -Force infra/secrets, infra/artifacts | Out-Null
```

Edite `infra/.env.tunnel`. Para este ambiente, mantenha:

```dotenv
ATLAS_PUBLIC_WEB_URL=https://atlasengenharia.site
ASAAS_BASE_URL=https://api-sandbox.asaas.com/v3
ASAAS_CHECKOUT_BASE_URL=https://sandbox.asaas.com/checkoutSession/show
```

O host da página de checkout também muda conforme o ambiente. No Sandbox use
`https://sandbox.asaas.com/checkoutSession/show`; em produção use
`https://asaas.com/checkoutSession/show`.

Preencha `POSTGRES_PASSWORD`, `ATLAS_LICENSE_ADMIN_KEY`, `ASAAS_API_KEY` e
`ASAAS_WEBHOOK_TOKEN`. Gere valores aleatórios diferentes para cada senha/token.
Esses arquivos já estão ignorados pelo Git.

Mantenha `ASAAS_API_KEY` entre aspas simples no arquivo `.env.tunnel`. As chaves
do Asaas contêm `$`; sem as aspas, o Compose tenta interpretar parte da chave
como outra variável e substitui esse trecho por vazio:

```dotenv
ASAAS_API_KEY='sua-chave-do-asaas'
```

## 3. Gerar as chaves de teste

Faça isso uma vez. Não regenere as chaves enquanto houver ativações que você
queira preservar.

```powershell
dotnet run --project apps/license-manager -- generate-signing-key `
  infra/secrets/server-signing-key.pk8 `
  infra/secrets/server-signing-public.json `
  atlas-test-2026-01

dotnet run --project apps/license-manager -- generate-catalog-key `
  infra/secrets/catalog-keys.json `
  catalog-test-2026-01
```

O valor `atlas-test-2026-01` precisa ser igual ao
`ATLAS_LICENSE_SIGNING_KEY_ID` de `infra/.env.tunnel`.

Os downloads só serão liberados se existirem:

```text
infra/artifacts/Instalador-AtlasCad.exe
infra/artifacts/Desinstalar-AtlasCad.exe
```

Durante a configuração inicial, você pode subir o sistema sem os executáveis.
A API retornará `download_unavailable` quando uma conta tentar baixá-los.

## 4. Criar o Tunnel na Cloudflare

No painel Cloudflare:

1. Abra **Networking > Tunnels**.
2. Selecione **Create tunnel** e escolha `cloudflared`.
3. Use o nome `atlas-local-test`.
4. Na etapa de instalação, selecione Docker apenas para visualizar o comando de
   referência do container; executaremos a mesma imagem OCI com Podman.
5. Copie somente o token longo que aparece depois de `--token`. Não salve o
   comando inteiro no repositório.

Grave o token sem quebra de linha:

```powershell
$tunnelToken = Read-Host 'Cole o token do Cloudflare Tunnel'
[IO.File]::WriteAllText(
  (Join-Path (Resolve-Path infra/secrets) 'cloudflare-tunnel-token.txt'),
  $tunnelToken
)
Remove-Variable tunnelToken
```

Quem possuir esse token pode executar uma réplica do seu Tunnel. Se ele vazar,
use **Refresh token** no painel e substitua o arquivo local.

## 5. Criar as rotas públicas

Dentro do Tunnel `atlas-local-test`, abra **Routes**, adicione rotas do tipo
**Published application** e use exatamente:

| Hostname público                      | Tipo | Service URL       |
| ------------------------------------- | ---- | ----------------- |
| `atlasengenharia.site`                | HTTP | `http://web:3000` |
| `api.atlasengenharia.site`            | HTTP | `http://api:8080` |
| `www.atlasengenharia.site` (opcional) | HTTP | `http://web:3000` |

`web` e `api` são nomes DNS da rede criada pelo Podman Compose. Não use `localhost`: dentro do
container `cloudflared`, `localhost` apontaria para o próprio `cloudflared`.

Ao criar cada rota pelo painel, a Cloudflare normalmente cria o CNAME para
`<TUNNEL_ID>.cfargotunnel.com`. Confirme em **DNS > Records**. Remova registros
`A`, `AAAA` ou CNAME antigos que disputem exatamente os mesmos hostnames.

Não coloque Cloudflare Access na frente desses dois hostnames durante o teste:
clientes do plugin e webhooks do Asaas não conseguem completar um login
interativo do Access.

## 6. Subir os containers

Execute a partir da raiz do repositório:

```powershell
podman compose --env-file infra/.env.tunnel -f infra/compose.tunnel.yaml config
podman compose --env-file infra/.env.tunnel -f infra/compose.tunnel.yaml up -d --build
podman compose --env-file infra/.env.tunnel -f infra/compose.tunnel.yaml ps
```

Veja os logs iniciais:

```powershell
podman compose --env-file infra/.env.tunnel -f infra/compose.tunnel.yaml logs -f api web cloudflared
```

O Tunnel deve aparecer como **Healthy** no painel Cloudflare. Esse estado prova
que `cloudflared` chegou à Cloudflare, mas não prova que `web` e `api` estão
respondendo; faça também os testes HTTP abaixo.

O Compose fixa o transporte em HTTP/2, compatível com redes que bloqueiam QUIC
na porta UDP 7844. A saída TCP continua sendo iniciada pelo `cloudflared`; não é
necessário abrir uma porta de entrada no roteador.

Se quiser acessar os serviços diretamente apenas da máquina local, use o
overlay de diagnóstico:

```powershell
podman compose --env-file infra/.env.tunnel `
  -f infra/compose.tunnel.yaml `
  -f infra/compose.debug.yaml `
  up -d
```

Ele abre somente em loopback: site `127.0.0.1:3000`, API `127.0.0.1:5080` e
PostgreSQL `127.0.0.1:5432`. Não encaminhe essas portas no roteador.

Se o relay do Podman/WSL aceitar a porta `5432` mas o DBeaver apresentar
timeout, use o procedimento em [Acesso ao PostgreSQL pelo DBeaver no
Windows](DBEAVER_PODMAN.md).

## 7. Verificar DNS, site e API

```powershell
Resolve-DnsName atlasengenharia.site
Resolve-DnsName api.atlasengenharia.site

curl.exe -I https://atlasengenharia.site
curl.exe https://api.atlasengenharia.site/health
curl.exe https://api.atlasengenharia.site/api/v1/plans
```

Resultados esperados:

- site: HTTP `200` ou redirecionamento válido;
- `/health`: JSON com `status: ok`;
- `/api/v1/plans`: planos mensal e anual.

Erro `1016` indica, em geral, DNS apontando para um Tunnel sem conexão. Erro
`502` com Tunnel saudável normalmente significa que o Service URL está errado
ou que o container de destino não iniciou.

## 8. Configurar o Asaas Sandbox

No Asaas, configure o webhook:

```text
https://api.atlasengenharia.site/api/v1/webhooks/asaas
```

O token de autenticação deve ser exatamente o valor de
`ASAAS_WEBHOOK_TOKEN`. Habilite pelo menos:

- `CHECKOUT_CREATED`;
- `CHECKOUT_PAID`;
- `CHECKOUT_CANCELED`;
- `CHECKOUT_EXPIRED`;
- `PAYMENT_CREATED`;
- `PAYMENT_CONFIRMED`;
- `PAYMENT_RECEIVED`;
- `PAYMENT_OVERDUE`;
- `PAYMENT_REFUNDED`;
- `PAYMENT_CHARGEBACK_REQUESTED`;
- `SUBSCRIPTION_CREATED`;
- `SUBSCRIPTION_INACTIVATED`;
- `SUBSCRIPTION_DELETED`.

O cartão usa um Checkout recorrente hospedado pelo Asaas. O retorno do
navegador para `/minha-conta` não confirma o pagamento: a API somente libera
download e ativação após `PAYMENT_CONFIRMED` ou `PAYMENT_RECEIVED`.

Não aplique Managed Challenge, Browser Integrity Check ou bloqueio de bots ao
endpoint do webhook. O token `asaas-access-token` é validado pela própria API.

## 9. Regras Cloudflare recomendadas para o teste

Crie regras de cache com ação **Bypass cache** para:

- hostname `api.atlasengenharia.site`;
- caminhos `/api/*`, `/checkout`, `/entrar` e `/minha-conta*` em
  `atlasengenharia.site`.

Não faça cache de respostas autenticadas nem de downloads da área do cliente.
Evite desafios de navegador em todo o hostname `api.atlasengenharia.site`,
pois ele é consumido pelo Asaas e pelo plugin AutoCAD, não por um navegador.

## 10. Gerar o plugin apontando para o Tunnel

O release entregue ao usuário precisa incorporar a API pública:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-AtlasRelease.ps1 `
  -Version 1.0.0 `
  -OutputRoot infra/artifacts `
  -PublicLicenseApiUrl https://api.atlasengenharia.site `
  -SigningPublicKeyPath infra/secrets/server-signing-public.json `
  -CertificateThumbprint SEU_CERTIFICADO_AUTHENTICODE
```

O release exige um certificado Authenticode confiável com chave privada no
repositório `Cert:\CurrentUser\My`. São publicados somente:

```text
infra/artifacts/Instalador-AtlasCad.exe
infra/artifacts/Desinstalar-AtlasCad.exe
```

## 11. Teste ponta a ponta

1. Acesse `https://atlasengenharia.site/checkout`.
2. Crie uma conta, selecione cartão, preencha telefone e endereço de cobrança
   e confirme o redirecionamento para o Checkout hospedado do Asaas. Esses
   campos são exigidos pelo Checkout e enviados ao cadastro do cliente no
   Asaas; os dados do cartão não passam pelo ATLAS CAD.
3. Confirme que antes do pagamento o download está bloqueado.
4. Efetue/confirme o pagamento no Asaas Sandbox.
5. Confira o recebimento do webhook nos logs da API.
6. Atualize a Área do Cliente e baixe o ZIP.
7. Gere o identificador da máquina e solicite o código de ativação.
8. Ative no AutoCAD e instale o catálogo.
9. Teste renovação, troca de plano, desativação da máquina e cancelamento.

Se já estiver autenticado, `/checkout` redireciona para `/minha-conta`. Uma
conta existente deve usar **Abrir cobrança**, **Abrir checkout seguro** ou
**Tentar pagamento novamente**; não deve realizar outro cadastro.

Para encerrar o ambiente sem apagar dados:

```powershell
podman compose --env-file infra/.env.tunnel -f infra/compose.tunnel.yaml down
```

Não use `down -v` se quiser preservar contas, pagamentos conciliados e
ativações do PostgreSQL.
