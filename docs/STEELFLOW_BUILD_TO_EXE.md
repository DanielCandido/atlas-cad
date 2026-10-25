# SteelFlow: do LISP ao instalador EXE

Este documento descreve o pipeline real usado pelo SteelFlow. Ele cobre a
adaptação do LISP, a geração do catálogo protegido, a compilação dos runtimes do
AutoCAD, a criação dos bundles Autodesk e a publicação do instalador único.

## Resultado final

Uma release produz dois arquivos assinados:

- `Instalador-AtlasCad.exe`: instala o bundle compatível, vincula o dispositivo
  à conta e prepara a licença e o catálogo;
- `Desinstalar-AtlasCad.exe`: o mesmo binário, distribuído com outro nome, entra
  no fluxo de desinstalação.

O instalador contém três payloads, um para cada família de runtime:

| AutoCAD | Runtime do adaptador | Payload |
| --- | --- | --- |
| 2022–2024 | .NET Framework 4.8 | `autocad-2022-2024` |
| 2025 e 2026 até Update 1.1 | .NET 8 | `autocad-2025-2026-1-1` |
| 2026 Update 1.2 ou posterior | .NET 10 | `autocad-2026-1-2-plus` |

## Resumo do que foi feito no SteelFlow

| Marco | Transformação realizada |
| --- | --- |
| M0 — baseline | Inventário do LISP original, comandos, geometrias, estado do AutoCAD e regra de preservação da origem. |
| M1 — bridge | Contrato AutoLISP/C# por request ID e arquivos privados; criação transacional de MLINE. |
| M2 — estilos | Parser de MLN, normalização, overrides de colisão e criação idempotente de `MlineStyle`. |
| M3 — integração | O fluxo real do SteelFlow passou a usar o bridge sem perder seleção, UCS, layer, undo e resolução CORTE/PLANTA. |
| M4 — licença | Chave de dispositivo CNG, ativação, entitlement assinado e renovação offline de 72 horas. |
| M5 — catálogo protegido | Catálogo AES-256-GCM e liberação autenticada da chave somente para licença válida. |
| M6 — distribuição | Adaptadores por runtime, FAS, bundles Autodesk, hashes, Authenticode e instalador WPF single-file. |
| Interface atual | Comando principal `SF`, auxiliares `SF_*` e remoção dos comandos antigos. |

## Visão do pipeline

```text
LISP original + MLN privados
          |
          +--> inventário e testes de paridade
          |
          +--> LISP integrado ao bridge ----------> STEELFLOW.fas
          |
          +--> normalização dos MLN --> JSON privado --> catálogo criptografado
                                                       |
Código C# + configuração da API ------------------> adaptadores AutoCAD
                                                       |
                 FAS + catálogo + bridge + adaptador --+
                                                       v
                                             AtlasCad.bundle (3 variantes)
                                                       |
                                            hashes + Authenticode
                                                       |
                                          payload ZIP incorporado
                                                       |
                                                       v
                                            Instalador-AtlasCad.exe
```

## 1. Inventário do LISP original

O trabalho começou preservando o fonte original e criando uma cópia privada de
integração. O original nunca foi editado nem versionado. Foram inventariados:

- comandos públicos;
- seleção de entidades e associação com textos;
- resolução dos nomes de perfil e variantes CORTE/PLANTA;
- conversão de LINE, ARC e LWPOLYLINE;
- variáveis do AutoCAD, UCS, layers bloqueados e grupo de undo;
- condição de segurança: a origem só pode ser removida depois que a nova MLINE
  for confirmada.

O fonte integrado usado pelo build fica em
`fixtures/private/m3/STEELFLOW.m3.lsp`. Essa pasta é ignorada pelo Git.

## 2. Integração do LISP com o bridge C#

O LISP continuou responsável pela experiência do comando e pela geometria de
entrada. As operações sensíveis foram movidas para o adaptador C#:

- leitura do catálogo protegido;
- instalação idempotente de `MlineStyle`;
- validação de desenho, layer, estilo e entidade de origem;
- criação transacional da MLINE;
- licença e liberação da chave do catálogo.

O cliente público `cad/legacy-lisp/atlas-bridge.lsp` troca requests e responses
JSON por uma pasta privada em `%LOCALAPPDATA%\AtlasCad\bridge\v1`. O AutoLISP
passa apenas um request ID opaco para os comandos C#.

No fluxo de desenho:

1. o LISP calcula pontos e normal em WCS;
2. solicita a instalação do estilo escolhido;
3. solicita a criação da MLINE;
4. confirma `success`, handle e tipo da entidade retornada;
5. somente então remove a origem;
6. se a remoção falhar, remove a MLINE criada para evitar duplicidade.

O comando público atual é `SF`; os auxiliares usam o padrão `SF_*`. A referência
completa está em `docs/STEELFLOW_COMMANDS.md`.

## 3. Construção do catálogo de estilos

Os arquivos `.mln` não são distribuídos. A ferramenta
`packages/catalog-builder` lê todos os MLN de uma pasta, valida a estrutura,
consolida definições idênticas e rejeita colisões diferentes.

### 3.1 Normalizar os MLN

```powershell
dotnet run --project packages/catalog-builder/Atlas.CatalogBuilder.csproj -- `
  "C:\privado\steelflow\mln" `
  ".tmp\steelflow\steelflow.atlascatalog.json" `
  --overrides "C:\privado\steelflow\catalog-overrides.json"
```

O resultado `.atlascatalog.json` é texto claro e continua sendo material
privado. Ele não pode entrar no Git nem no instalador.

O arquivo de overrides é opcional. Ele registra decisões explícitas para
colisões de nomes; a ordem dos arquivos nunca é usada para escolher
silenciosamente uma definição.

### 3.2 Criar ou rotacionar a chave do catálogo

Na criação inicial de uma família de catálogo:

```powershell
dotnet run --project apps/license-manager/Atlas.LicenseManager.csproj -- `
  generate-catalog-key `
  "infra\secrets\catalog-keys.json" `
  "steelflow-2026-01"
```

O key ring é segredo de servidor. Uma atualização comum pode manter a chave
atual. Uma rotação deve preservar chaves anteriores enquanto existirem versões
publicadas que dependam delas.

### 3.3 Proteger o catálogo

```powershell
dotnet run --project packages/catalog-builder/Atlas.CatalogBuilder.csproj -- `
  protect `
  ".tmp\steelflow\steelflow.atlascatalog.json" `
  ".tmp\steelflow\steelflow.atlascatalog" `
  "infra\secrets\catalog-keys.json" `
  2
```

O último argumento é a versão monotônica do catálogo, não a versão SemVer do
aplicativo. O arquivo final usa envelope autenticado AES-256-GCM e pode ser
incluído no bundle; a chave não pode.

## 4. Configuração de confiança e licenciamento

O release precisa de:

- descritor público ECDSA usado para validar entitlements;
- chave privada correspondente disponível somente no servidor;
- key ring do catálogo disponível somente no servidor/build protegido;
- URL pública HTTPS da API;
- certificado Authenticode com chave privada no repositório de certificados do
  Windows e serviço RFC 3161 de timestamp.

O script cria temporariamente
`.tmp/release-runtime-configuration.json` com a URL pública e embute essa
configuração e o descritor público nos adaptadores. Nenhuma chave privada é
incorporada.

## 5. Build dos adaptadores AutoCAD

`scripts/Build-AtlasRelease.ps1` compila três alvos:

1. `cad/autocad-2022-2024` em `net48`;
2. `cad/autocad-2025-2026` em `net8.0-windows`;
3. `cad/autocad-2025-2026` em `net10.0-windows`.

Os adaptadores recebem a mesma versão SemVer, chave pública de assinatura e URL
da API. As DLLs da Autodesk são referências de build e não são redistribuídas.

## 6. Compilação do LISP para FAS

O script gera `.tmp/m6-compile/compile-steelflow.scr` com uma chamada a:

```lisp
(vlisp-compile 'st "caminho/STEELFLOW.m3.lsp" "caminho/STEELFLOW.fas")
```

O script é executado por `accoreconsole.exe`. A release falha se o FAS não for
gerado ou estiver vazio. O cliente recebe somente `STEELFLOW.fas`; o `.lsp`
privado não é copiado.

O Core Console precisa estar inicializado e licenciado no perfil de build. Se
ele ficar preso antes de executar o script, abra o AutoCAD nessa conta, conclua
diálogos de primeiro uso/licença e repita o build.

## 7. Montagem dos bundles Autodesk

Para cada runtime, o build cria um `AtlasCad.bundle` temporário contendo:

- adaptador C# específico do runtime;
- `Atlas.Cad.Core.dll` quando aplicável;
- `atlas-bridge.lsp` público;
- `atlas-loader.lsp` público;
- `STEELFLOW.fas` compilado;
- `steelflow.atlascatalog` protegido;
- `PackageContents.xml` preenchido;
- `SHA256SUMS.txt`.

O manifest recebe versão, ProductCode determinístico, faixa do AutoCAD, pasta de
conteúdo e nome do adaptador. O loader é carregado no início do AutoCAD e, por
sua vez, carrega o bridge e o FAS.

O adaptador de cada bundle é assinado com Authenticode e verificado com
`signtool verify /pa` antes da próxima etapa.

## 8. Validação do bundle

`scripts/Test-AtlasBundle.ps1` rejeita uma release quando encontra:

- manifest ausente ou caminho que escape do bundle;
- runtime fora da matriz suportada;
- quantidade inválida de adaptadores;
- LSP privado, FAS de origem inesperado, MLN/MLNX, catálogo em texto claro,
  chave privada ou DLL Autodesk;
- FAS, catálogo ou adaptador ausente;
- nomes representativos de perfis vazando no catálogo criptografado;
- hash ausente ou divergente;
- assinatura Authenticode inválida quando exigida.

## 9. Geração do instalador único

Os três bundles são compactados em
`.tmp/atlas-installer-payload.zip`. O build também gera uma configuração com:

- URL da API;
- slug `steelflow`;
- product ID `atlas-steelflow`;
- ID e coordenadas da chave pública do servidor.

O projeto `apps/installer` publica um WPF `win-x64`, self-contained e
single-file. O payload ZIP e a configuração são embedded resources dentro do
EXE. O executável é assinado e verificado, depois copiado duas vezes para a
pasta de saída com os nomes de instalação e desinstalação.

Durante a instalação, o programa:

1. detecta as versões instaladas do AutoCAD;
2. escolhe um payload por família de runtime;
3. valida os hashes do bundle;
4. ajusta a faixa do manifest para a instalação detectada;
5. instala de forma transacional em
   `%APPDATA%\Autodesk\ApplicationPlugins`;
6. cria ou reutiliza a chave CNG não exportável do dispositivo;
7. realiza o pareamento pelo navegador;
8. valida o entitlement retornado;
9. obtém e protege localmente a chave do catálogo;
10. instala o catálogo e registra a desinstalação no Windows.

## 10. Comando completo de release

O parâmetro `CertificateThumbprint` é obrigatório no script atual:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-AtlasRelease.ps1 `
  -Version 1.2.0 `
  -AutodeskAutoCADRoot "C:\Program Files\Autodesk\AutoCAD 2026" `
  -SteelFlowSourcePath "C:\privado\steelflow\STEELFLOW.release.lsp" `
  -CatalogPackagePath ".tmp\steelflow\steelflow.atlascatalog" `
  -CatalogKeyRingPath "infra\secrets\catalog-keys.json" `
  -SigningPublicKeyPath "infra\secrets\server-signing-public.json" `
  -PublicLicenseApiUrl "https://api.exemplo.com" `
  -CertificateThumbprint "CERTIFICATE_THUMBPRINT"
```

Saída atual:

```text
.tmp/release/
  Instalador-AtlasCad.exe
  Desinstalar-AtlasCad.exe
```

## 11. Publicação comercial

O build local não ativa automaticamente a versão no portal. A publicação atual
é feita na administração de bibliotecas:

1. abrir o SteelFlow no painel administrativo;
2. enviar `Instalador-AtlasCad.exe` informando a mesma versão SemVer;
3. a API valida PE, tamanho e presença de Authenticode;
4. revisar hash e tamanho apresentados;
5. publicar a release escolhida;
6. testar o download autenticado pela Área do Cliente;
7. manter o instalador anterior disponível para rollback operacional.

## 12. Checklist de uma nova versão do SteelFlow

- [ ] Incrementar a versão SemVer sem reutilizar uma versão já enviada.
- [ ] Atualizar o LISP privado e preservar o original/baseline.
- [ ] Executar testes de paridade no AutoCAD.
- [ ] Regenerar o catálogo quando MLN ou overrides mudarem.
- [ ] Incrementar a versão do catálogo quando seu conteúdo mudar.
- [ ] Confirmar que a chave atual existe no key ring do servidor.
- [ ] Rodar testes gerenciados e o verificador de ativos privados.
- [ ] Gerar FAS, adaptadores, bundles e instalador.
- [ ] Verificar Authenticode e SHA-256.
- [ ] Fazer instalação limpa e upgrade sobre a versão anterior.
- [ ] Validar `SF`, ativação, catálogo, modo offline e desinstalação.
- [ ] Enviar e publicar a release no painel.
- [ ] Registrar hash, certificado, catálogo, data e evidências da release.

