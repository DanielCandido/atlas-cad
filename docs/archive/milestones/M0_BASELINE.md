# M0 — baseline e segurança

Data do inventário: 2026-10-08. Este documento registra o estado observado do
repositório antes do M1. Ele não afirma validação dentro do AutoCAD.

## Estado do marco

| Entrega | Estado | Evidência |
| --- | --- | --- |
| Inventário de projetos, frameworks e builds | concluído | seções abaixo |
| Armazenamento privado e exclusões | concluído | `.gitignore`, `fixtures/README.md` e verificador de CI |
| Inventário comportamental do STEELFLOW | concluído | seção abaixo, sem reprodução do fonte |
| Capturas de desenhos, telas e entidades | pendente | exige AutoCAD licenciado; protocolo em `tests/autocad/baseline/README.md` |

O M0 só pode ser encerrado integralmente depois que as capturas privadas forem
executadas em uma instalação licenciada do AutoCAD. Até lá, o resultado é um
baseline de código completo e um baseline de execução preparado, mas pendente.

## Inventário técnico

| Componente | Projeto/arquivo | Alvo observado | Estado em 2026-10-08 |
| --- | --- | --- | --- |
| Core CAD | `cad/atlas-core/Atlas.Cad.Core.csproj` | `netstandard2.0` | uma regra simples de normalização |
| Testes do core | `tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj` | `net8.0` | dois casos xUnit; não restaurados nesta sandbox |
| Adaptador AutoCAD legado | `cad/autocad-2022-2024/Atlas.AutoCAD.Adapter.csproj` | `net48` | placeholder; sem referências Autodesk |
| Adaptador AutoCAD moderno | `cad/autocad-2025-2026/Atlas.AutoCAD.Adapter.csproj` | `net8.0-windows` | placeholder; sem referências Autodesk |
| Bridge experimental | `cad/atlas-cad-bridge/AtlasCadCommands.cs` | não definido | fonte solto, sem `.csproj`; não participa de build |
| API de licenças | `apps/licensing-api/Atlas.Licensing.Api.csproj` | `net8.0` | endpoints demonstrativos em memória |
| Gerenciador de licença | `apps/license-manager/Atlas.LicenseManager.csproj` | `net8.0` | CLI demonstrativa |
| Web | `apps/web/package.json` | Next.js 15 / React 19 | sem lockfile e sem `node_modules` local |

Não há `.sln`/`.slnx`. A CI existente testa apenas o core e compila a aplicação
web; ela não compila os adaptadores AutoCAD.

### Dependências Autodesk e ambiente observado

- Nenhuma referência a `AcMgd.dll`, `AcDbMgd.dll` ou `AcCoreMgd.dll` existe nos
  dois projetos de adaptador.
- Não foi encontrada instalação do AutoCAD 2022, 2023, 2024, 2025 ou 2026 em
  `C:\Program Files\Autodesk\AutoCAD <ano>`.
- Não foi encontrado o .NET Framework 4.8 Developer Pack em
  `C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8`.
- Está instalado o .NET SDK 8.0.423. Node.js 24.16.0 e npm 11.13.0 também estão
  disponíveis, mas a CI declara Node.js 22.

Portanto, o código dependente do AutoCAD não é compilável nem executável neste
ambiente. O bridge experimental também não é compilável como parte do repositório
até receber um projeto e referências no M1.

### Resultado dos comandos de baseline

A primeira execução de todos os projetos .NET falhou antes da compilação porque
o ambiente gerenciado negou leitura de
`%APPDATA%\NuGet\NuGet.Config`. Uma segunda execução com `DOTNET_CLI_HOME` gravável
e outra com um `NuGet.Config` isolado chegaram ao mesmo bloqueio. Nenhum projeto
.NET chegou ao compilador e nenhum teste foi executado. Isso é uma limitação
desta sandbox, não evidência de erro ou sucesso de compilação C#.

Depois de liberar o NuGet, ainda são esperadas estas pendências reais:

- `net48`: instalar o Developer Pack 4.8;
- adaptadores: configurar as três assemblies Autodesk da versão alvo com
  `Private=false`/`Copy Local=false` no M1;
- testes: restaurar `Microsoft.NET.Test.Sdk`, `xunit` e runner;
- web: criar e versionar um lockfile para builds determinísticos (fora do M0).

Comandos reproduzíveis em uma máquina de desenvolvimento normal:

```powershell
dotnet --info
dotnet build cad/atlas-core/Atlas.Cad.Core.csproj
dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj
dotnet build apps/licensing-api/Atlas.Licensing.Api.csproj
dotnet build apps/license-manager/Atlas.LicenseManager.csproj
dotnet build cad/autocad-2022-2024/Atlas.AutoCAD.Adapter.csproj
dotnet build cad/autocad-2025-2026/Atlas.AutoCAD.Adapter.csproj
npm ci --prefix apps/web
npm run build --prefix apps/web
pwsh -File scripts/verify-private-assets.ps1
```

`npm ci` passará a ser aplicável depois que um lockfile for criado. Os dois builds
AutoCAD só serão representativos depois do M1 configurar as referências locais.

## Ativos privados observados

O material fornecido em `docs/knowledge` contém 1 LSP, 673 MLN, 673 MLNX, além
de arquivos auxiliares. O LSP inspecionado tinha 47.807 bytes e SHA-256
`28F4700CDBD41ECAD6B1E8BE31B13A512096DB030A237AD47204ED3091ED52AD` nesta data.
O hash serve apenas para identificar o baseline; nenhum conteúdo proprietário é
reproduzido aqui.

`docs/knowledge`, `fixtures/private` e a pasta de evidências do AutoCAD são
ignorados integralmente. O script `scripts/verify-private-assets.ps1` falha se
Git rastrear essas pastas ou extensões de fonte CAD privadas. Como esta cópia do
workspace não contém metadados `.git`, não foi possível provar se algum arquivo
já havia sido rastreado antes das novas regras. Em um clone com Git, execute o
verificador e, se necessário, remova os ativos apenas do índice com
`git rm --cached` antes de qualquer push.

## Inventário comportamental do STEELFLOW

Foram encontrados sete comandos públicos:

| Comando | Comportamento observado | Estado afetado |
| --- | --- | --- |
| `STEELFLOW` | fluxo principal de conversão de LINE/ARC/LWPOLYLINE em MLINE por texto/perfil e estilo | seleção, entidades, layer, UCS e grupo de undo |
| `SFCONFIG` | seleciona e persiste a pasta da biblioteca e força recarga | configuração local e cache da sessão |
| `SFDIST` | consulta/altera a distância máxima texto–curva | configuração local |
| `SFARCO` | consulta/altera a discretização de arcos | configuração local e geometria gerada |
| `SFLIST` | lista estilos carregados em arquivo temporário | leitura do catálogo e arquivo temporário |
| `SFRELOAD` | invalida o estado carregado e recarrega a biblioteca | estilos do desenho e diagnóstico |
| `SFACTIVATE` | ativa a trava local legada | variável de ambiente do usuário; não é licenciamento de produção |

Fronteiras que M1–M3 deverão preservar ou substituir de forma controlada:

| Função | Responsabilidade | Tratamento previsto |
| --- | --- | --- |
| `SF-LoadLibrary` | descobre arquivos da biblioteca e coordena carregamento/diagnóstico | fronteira de catálogo no M2/M3 |
| `SF-LoadStyleFile` | lê um arquivo, interpreta estilos e registra no desenho | substituir por importação suportada no M2/M3 |
| `SF-ResolveStyle` | resolve corte/planta e variantes do perfil | preservar inicialmente |
| `SF-CurvePoints` | extrai pontos WCS de LINE/ARC/LWPOLYLINE e discretiza arcos | preservar inicialmente; vira entrada do contrato |
| `SF-DrawMline` | transforma pontos para o UCS do comando e confirma criação da MLINE | substituir pela chamada segura ao bridge |
| `SF-SaveVars` / `SF-RestoreVars` | preserva variáveis do AutoCAD | preservar e testar em sucesso, erro e cancelamento |
| `SF-SetLayerLock` / `SF-Relock` | altera temporariamente e restaura lock de layer | preservar e testar |
| `SF-StartUndo` / `SF-EndUndo` | delimita o grupo de undo | preservar a experiência de undo |

O fluxo observado só remove a entidade de origem após detectar uma MLINE nova.
Essa propriedade é um contrato de segurança para M1–M3: falha, timeout,
cancelamento ou estilo ausente nunca podem apagar a geometria original.

## Decisões ainda abertas após M0

- edições suportadas: AutoCAD completo ou também LT;
- matriz final além dos anos-âncora 2022 e 2026;
- política de colisão de nomes de estilo;
- normal, projeção planar, justificação e escala da MLINE;
- fronteira transacional entre exclusão da origem e criação da MLINE.

Essas decisões não devem ser inferidas dos fixtures; serão contratos explícitos
do M1/M2.
