# Plano de automação de build de bibliotecas

## Objetivo

Criar um pipeline único capaz de receber uma biblioteca AutoCAD descrita por um
manifesto e produzir uma nova versão assinada e publicável, sem duplicar o
pipeline do SteelFlow nem editar constantes espalhadas pelo código.

O comando pretendido é:

```powershell
./scripts/Build-AtlasLibrary.ps1 `
  -Manifest libraries/steelflow/library.build.json `
  -Version 1.2.0 `
  -PublicLicenseApiUrl https://api.exemplo.com `
  -CertificateThumbprint CERTIFICATE_THUMBPRINT
```

## Dois cenários diferentes

### Nova versão de uma biblioteca existente

Mantém `slug`, `productId`, `moduleCode`, `catalogId`, UpgradeCode e comandos.
Altera a versão SemVer e, quando necessário, LISP, catálogo e documentação.
Esse cenário já está próximo de ser automatizado.

### Nova biblioteca

Cria uma identidade comercial e criptográfica nova. Exige novos IDs, comandos,
loader, catálogo opcional, isolamento de estado local, instalador e registro na
API. O código atual ainda possui valores fixos do SteelFlow e precisa ser
generalizado antes que esse cenário seja seguro.

## Estado atual e bloqueios

| Área | Acoplamento atual |
| --- | --- |
| Script de release | parâmetros e nomes `SteelFlowSourcePath`, `STEELFLOW.fas` e `steelflow.atlascatalog` |
| Licenciamento compartilhado | `ProductId` e `ModuleCode` fixos em `LicensingProtocol` |
| Pacote de catálogo | `productId` e `catalogId` fixos em `CatalogPackage` |
| Adaptador AutoCAD | procura `steelflow.atlascatalog` e valida o produto fixo |
| Cache local | catálogo e licença usam caminhos compartilhados em `%LOCALAPPDATA%\AtlasCad` |
| Loader/manifest | descrição e nome do FAS fixos no SteelFlow |
| Instalador | nomes de bundle, catálogo, EXE, registro e textos fixos em `AtlasCad`/SteelFlow |
| API | cadastro comercial aceita várias bibliotecas, mas alguns endpoints e fallbacks ainda assumem `steelflow` |
| Validador | arquivos obrigatórios e verificações de vazamento são específicos do SteelFlow |

Conclusão: copiar `Build-AtlasRelease.ps1` para cada produto criaria releases
inconsistentes e aumentaria o risco de usar identidade, chave ou catálogo da
biblioteca errada.

## Arquitetura proposta

### 1. Manifesto público por biblioteca

Cada biblioteca terá um arquivo versionado sem segredos:

```json
{
  "schemaVersion": 1,
  "slug": "steelflow",
  "displayName": "SteelFlow",
  "productId": "atlas-steelflow",
  "moduleCode": "steelflow",
  "catalogId": "steelflow",
  "upgradeCode": "CC6867A2-63DD-4C96-98B9-A0D5A69E069E",
  "runtime": {
    "lispSourceEnv": "ATLAS_STEELFLOW_LISP",
    "fasName": "STEELFLOW.fas",
    "loaderTemplate": "installer/windows/templates/library-loader.lsp"
  },
  "commands": {
    "primary": "SF",
    "public": ["SF", "SF_CONFIG", "SF_DIST", "SF_ARCO", "SF_LIST", "SF_RELOAD", "SF_CATALOG", "SF_ACTIVATE"]
  },
  "catalog": {
    "enabled": true,
    "sourceDirectoryEnv": "ATLAS_STEELFLOW_MLN_ROOT",
    "overridesEnv": "ATLAS_STEELFLOW_CATALOG_OVERRIDES",
    "protectedFileName": "steelflow.atlascatalog",
    "leakMarkers": ["W150X13", "HP-200X53C"]
  },
  "installer": {
    "executableName": "Instalador-SteelFlow.exe",
    "uninstallerName": "Desinstalar-SteelFlow.exe",
    "publisher": "ATLAS CAD"
  }
}
```

O manifesto referencia fontes privadas por variável de ambiente. Caminhos,
chaves, certificados e conteúdos proprietários não entram no arquivo.

### 2. Workspace privado padronizado

```text
private-libraries/<slug>/
  source/<biblioteca>.lsp
  catalog/mln/*.mln
  catalog/catalog-overrides.json
  tests/*.lsp
  evidence/
```

Esse diretório deve ficar fora do Git ou em raiz já ignorada. O build resolve os
caminhos, impede escape para locais inesperados e nunca copia fontes privadas
para a saída.

### 3. Orquestrador genérico

`Build-AtlasLibrary.ps1` será responsável por:

1. validar manifesto e versão;
2. resolver os caminhos privados;
3. executar o baseline/testes definidos pela biblioteca;
4. normalizar e proteger o catálogo quando habilitado;
5. gerar uma configuração de runtime específica;
6. compilar adaptadores e LISP/FAS;
7. materializar loaders e manifests a partir de templates;
8. montar e validar bundles por runtime;
9. assinar adaptadores e instalador;
10. publicar o instalador single-file;
11. emitir um manifesto de proveniência da release;
12. opcionalmente enviar a release à API, sem publicá-la automaticamente.

O script atual do SteelFlow deve virar um wrapper temporário que chama o
orquestrador com o manifesto `steelflow`.

### 4. Configuração de runtime por produto

Substituir constantes de produto por um recurso embedded assinado contendo:

- slug, product ID, module code e catalog ID;
- nome do catálogo e namespace do cache local;
- comandos e capacidades habilitadas;
- URL da API e descritor público do servidor.

O adaptador e o instalador devem ler a mesma configuração. A API continua sendo
a autoridade para confirmar que produto, módulo, assinatura e catálogo
correspondem à assinatura comprada.

### 5. Capacidades opcionais

Nem toda biblioteca futura será baseada em MLINE/MLN. O manifesto precisa
declarar capacidades em vez de presumir o SteelFlow:

- `lispRuntime`: compilar e carregar FAS;
- `mlineCatalog`: normalizar MLN e habilitar comandos de estilo;
- `drawMlineBridge`: incluir o contrato de criação de MLINE;
- `deviceLicense`: exigir entitlement;
- `protectedCatalog`: solicitar chave de catálogo;
- `customFiles`: incluir arquivos públicos explicitamente permitidos.

O build falha quando uma capacidade exige uma entrada ausente.

### 6. Artefatos reproduzíveis

Estrutura proposta:

```text
.tmp/releases/<slug>/<version>/
  inputs.json
  catalog/
    <slug>.atlascatalog
  fas/
    <nome>.fas
  bundles/
    autocad-2022-2024/AtlasCad.<slug>.bundle/
    autocad-2025-2026-1-1/AtlasCad.<slug>.bundle/
    autocad-2026-1-2-plus/AtlasCad.<slug>.bundle/
  installer/
    Instalador-<Biblioteca>.exe
    Desinstalar-<Biblioteca>.exe
  release-manifest.json
```

`release-manifest.json` deve registrar hashes, versão do catálogo, key IDs,
commit, SDKs, versão do AutoCAD/Core Console, certificado e timestamp. Nunca
deve registrar material de chave ou caminhos privados completos.

## Plano de implementação

### Fase 0 — contrato de onboarding

- Definir quais bibliotecas são apenas LISP e quais usam catálogo/bridge.
- Formalizar os testes mínimos de segurança e paridade.
- Definir política SemVer, versão de catálogo e rotação de chaves.
- Criar schema JSON e validador do manifesto.

**Aceite:** um manifesto inválido falha antes de qualquer compilação ou escrita
de artefato.

### Fase 1 — extrair o build genérico

- Criar `Build-AtlasLibrary.ps1`.
- Extrair compilação FAS, matriz de runtimes, assinatura, hashes e montagem de
  bundle para funções reutilizáveis.
- Manter `Build-AtlasRelease.ps1` como wrapper do SteelFlow durante a migração.
- Tornar `Test-AtlasBundle.ps1` orientado pelo manifesto.

**Aceite:** o instalador do SteelFlow produzido pelo novo pipeline possui os
mesmos arquivos e passa os mesmos testes do pipeline atual.

### Fase 2 — remover identidade fixa do runtime

- Criar `Atlas.Library.Runtime.json` embedded.
- Parametrizar licenciamento, catálogo, cache e diagnóstico.
- Isolar estado local por slug/produto.
- Validar no servidor product ID, módulo e catalog ID vindos do cadastro da
  biblioteca, sem fallbacks implícitos para SteelFlow.

**Aceite:** dois produtos instalados na mesma conta Windows não compartilham
catálogo, entitlement ou arquivo de estado por engano.

### Fase 3 — generalizar loader, manifest e instalador

- Gerar loader a partir de template, incluindo somente o FAS da biblioteca.
- Parametrizar nome, descrição, comandos, UpgradeCode e nomes dos bundles.
- Remover lista fixa de bundles do instalador.
- Parametrizar nome do catálogo, registro de desinstalação e textos da UI.
- Gerar ProductCode determinístico a partir de produto, versão e runtime.

**Aceite:** um instalador de biblioteca A não remove nem substitui a biblioteca
B e upgrades preservam somente o UpgradeCode da própria família.

### Fase 4 — automatizar catálogos

- Integrar normalize/protect ao orquestrador.
- Exigir incremento da versão do catálogo quando o plaintext mudar.
- Comparar o key ID do pacote com o key ring do servidor.
- Gerar relatório de colisões e contagens sem expor nomes na saída pública.
- Suportar biblioteca sem catálogo.

**Aceite:** alterar MLN sem incrementar a versão do catálogo bloqueia a release;
catálogo adulterado ou chave divergente também bloqueia.

### Fase 5 — publicação controlada

- Adicionar comando de upload autenticado para a API administrativa.
- Verificar no servidor versão, assinatura e SHA-256 retornados.
- Manter a ativação da release como aprovação manual separada.
- Registrar auditoria de quem construiu, enviou e publicou.

**Aceite:** o CI pode criar e enviar um draft, mas não tornar uma versão pública
sem uma ação autorizada explícita.

### Fase 6 — CI/CD em runner Windows controlado

- Usar runner Windows privado com AutoCAD/Core Console licenciado, SDKs e
  certificado acessível por provedor seguro.
- Restaurar dependências em job sem acesso a fontes privadas.
- Montar fontes, catálogos e chaves somente no job protegido de release.
- Executar testes, build, Authenticode, timestamp, smoke test e upload.
- Exigir aprovação de ambiente para produção.
- Limpar workspace e material secreto ao final.

**Aceite:** tags de release geram drafts reproduzíveis; pull requests nunca têm
acesso a chaves, certificado, LISP ou MLN privados.

## Fluxo de nova versão após a automação

1. Atualizar o fonte privado e/ou catálogo.
2. Atualizar changelog e documentação.
3. Executar `Test-AtlasLibrary.ps1 -Manifest ...`.
4. Criar tag SemVer aprovada.
5. Runner gera e assina todos os artefatos.
6. Pipeline envia a versão como draft.
7. Responsável executa instalação limpa e upgrade.
8. Responsável publica a release no painel.

## Fluxo de uma nova biblioteca após a automação

1. Inventariar comandos, estado, arquivos e comportamento do LISP.
2. Criar baseline privado e testes de paridade.
3. Definir slug, product ID, module code, catalog ID e UpgradeCode únicos.
4. Criar o manifesto público.
5. Integrar operações privilegiadas ao bridge apenas quando necessário.
6. Configurar catálogo e overrides, ou declarar catálogo desabilitado.
7. Cadastrar biblioteca, documentação e planos na API.
8. Gerar uma release candidata sem publicação.
9. Validar a matriz AutoCAD, falhas seguras, upgrade e desinstalação.
10. Publicar somente depois da aprovação técnica e comercial.

## Política de versões

- **SemVer do instalador:** identifica código, LISP e experiência distribuída.
- **Versão do catálogo:** inteiro monotônico; incrementa quando o conteúdo
  normalizado muda.
- **Key ID do catálogo:** muda apenas em rotação planejada.
- **UpgradeCode:** permanente por biblioteca.
- **ProductCode:** determinístico e diferente por versão/runtime.
- Uma versão publicada nunca é sobrescrita; correções geram nova versão.

## Testes obrigatórios

- parser e normalizador com fixtures públicos;
- contrato de bridge e validações de request;
- ausência de fontes privadas no Git e nos artefatos;
- compilação FAS não vazia;
- manifest e matriz de runtime;
- inventário SHA-256;
- Authenticode e timestamp;
- instalação limpa, upgrade, rollback e desinstalação;
- licença válida, expirada e offline;
- catálogo ausente, adulterado, incompatível e com colisão;
- falha de criação preservando a geometria de origem;
- coexistência de duas bibliotecas na mesma máquina.

## Decisão recomendada

Começar pela Fase 0 e Fase 1, mantendo o SteelFlow como caso de referência. Não
iniciar uma segunda biblioteca comercial antes da Fase 2 e da Fase 3: enquanto
produto, catálogo e cache estiverem fixos, um segundo instalador pode validar a
licença errada ou substituir arquivos do SteelFlow.

