# M1 — ponte mínima AutoLISP ↔ C#

## Estado

O contrato, os dois projetos de adaptador, o transporte por arquivo privado e os
testes automatizados do contrato estão implementados. A instalação detectada é
AutoCAD 2026.1.2 (`25.1.179.0.0`), que usa .NET 10. O build oficial do adaptador
2026 está validado com o SDK .NET 10.0.401 e o caminho feliz já passou no AutoCAD;
falta concluir a matriz manual de propriedades e falhas seguras.

O adaptador 2022–2024 está preparado para `net48`, porém permanece sem validação
porque AutoCAD 2022 e o .NET Framework 4.8 Developer Pack não estão instalados.

### Validações executadas em 2026-10-09

- O build oficial do core e do adaptador 2026 concluiu com zero warnings e zero
  erros usando o SDK 10.0.401 e as DLLs Autodesk `25.1.179.0.0`.
- O output contém apenas `Atlas.AutoCAD.Adapter.2026` e `Atlas.Cad.Core`; as DLLs
  Autodesk não foram copiadas.
- O assembly oficial do core passou um smoke test de contrato, serialização,
  request ID inseguro e geometria fora do plano.
- O Core Console iniciou sem `/isolate`, mas permaneceu aguardando a inicialização
  do perfil/licença e não executou o script. A sessão foi encerrada sem alterar o
  AutoCAD instalado; esse ensaio headless não foi contado como sucesso.
- A suíte xUnit permanece pendente nesta máquina porque os pacotes de teste não
  estão disponíveis no cache offline. O job de CI executa `dotnet test` com restore.
- O teste manual no AutoCAD 2026 carregou a DLL e o cliente AutoLISP, percorreu o
  transporte request/response e criou uma MLINE com `ATLAS_TEST_MLINE`, retornando
  sucesso ao comando `ATLASBRIDGETEST`.

## Contrato v1

`DrawMlineRequest` contém:

- `protocolVersion`: deve ser `1`;
- `requestId`: 32 caracteres hexadecimais minúsculos;
- `styleName`: o M1 foi validado com `ATLAS_TEST_MLINE`; a evolução M3 aceita
  qualquer estilo válido já instalado no desenho;
- `pointsWcs`: entre 2 e 10.000 pontos finitos e ao menos dois distintos;
- `normalWcs`: normal finita, não nula, em WCS;
- `layerId` e/ou `layerName`: layer existente, com consistência quando ambos existem;
- `closed`, `sourceHandle` e `expectedDrawingIdentity`;
- `justification`: `0=Top`, `1=Zero`, `2=Bottom`;
- `scale`: valor finito maior que zero.

Os pontos devem pertencer ao plano definido pelo primeiro ponto e `normalWcs`.
Não há conversão implícita de UCS no C#: o chamador transforma tudo para WCS. O
cliente público `atlas-bridge.lsp` demonstra a transformação correta com `trans`.

`DrawMlineResponse` devolve a versão, o mesmo request ID, sucesso/falha, handle da
nova entidade ou código de erro. O C# cria a MLINE em uma única transação; qualquer
erro anterior ao commit aborta a criação. O M1 nunca apaga a geometria de origem.

## Transporte e segurança

O cliente chama duas funções expostas à AutoLISP:

- `(ATLAS_NEW_REQUEST_ID)` gera o ID opaco no C#;
- `(ATLAS_BRIDGE_ROOT)` cria e retorna a pasta privada.

Payloads ficam em `%LOCALAPPDATA%\AtlasCad\bridge\v1`. A pasta recebe uma DACL
explícita para o usuário atual, SYSTEM e Administrators, não pode ser reparse point
e não aceita nomes livres. O comando recebe apenas o request ID validado; nenhum
JSON ou lista de pontos passa pela linha de comando. O limite do request é 1 MiB.

O LISP publica primeiro um `.tmp` e o renomeia atomicamente para `.request.json`.
O C# abre sem compartilhamento, remove o request mesmo em falha e publica a
resposta também por rename. O cliente lê e remove a resposta.

Esta proteção evita modificação por outros usuários locais sob ACLs normais. Não
protege contra processos maliciosos executados como o mesmo usuário ou como
administrador.

## Projetos e referências Autodesk

| Projeto | Target | Referências |
| --- | --- | --- |
| `cad/atlas-core` | `net10.0` | contrato independente do AutoCAD; compartilhado pelos testes e pelo adaptador moderno |
| `cad/autocad-2022-2024` | `net48` | caminho informado por `AutodeskAutoCADRoot` ou `AUTOCAD_2022_ROOT` |
| `cad/autocad-2025-2026` | `net10.0-windows` | caminho informado por `AutodeskAutoCADRoot` ou `AUTOCAD_2026_ROOT` |

`AcMgd`, `AcDbMgd` e `AcCoreMgd` usam `Private=false`; as DLLs Autodesk não são
copiadas para o output nem redistribuídas.

O adaptador legado inclui os mesmos fontes do core como arquivos vinculados,
pois `net48` não pode referenciar os targets modernos. Isso evita depender do
pacote legado `NETStandard.Library` durante builds offline.

## Build

Pré-requisitos do AutoCAD 2026.1.2:

1. SDK .NET 10 x64 (validado localmente com 10.0.401);
2. AutoCAD aberto ao menos uma vez e atualizado para 2026.1.2;
3. assemblies em `F:\Softwares\AutoCad2026\AutoCAD 2026`.

```powershell
dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj
pwsh -File scripts/build-autocad-2026.ps1
```

Para 2022:

```powershell
dotnet build cad/autocad-2022-2024/Atlas.AutoCAD.Adapter.csproj `
  -p:AutodeskAutoCADRoot="C:\Program Files\Autodesk\AutoCAD 2022"
```

O segundo comando exige o AutoCAD 2022 e o .NET Framework 4.8 Developer Pack.

## Execução no AutoCAD

1. Crie manualmente, em um DWG descartável, um MLINESTYLE não proprietário chamado
   `ATLAS_TEST_MLINE`, baseado em `STANDARD` e com duas linhas simples.
2. Adicione a pasta de build a `TRUSTEDPATHS`.
3. Use `NETLOAD` para carregar `Atlas.AutoCAD.Adapter.2026.dll`.
4. Use `APPLOAD` para carregar `cad/legacy-lisp/atlas-bridge.lsp`.
5. Execute `ATLASBRIDGETEST`.

O checklist completo está em `../checklists/M1_MANUAL_CHECKLIST.md`.

## Limitações deliberadas

- O estilo sintético continua sendo o fixture público; importação MLN pertence ao M2.
- Não existe licenciamento de produção; este comando é um protótipo de M1.
- O bridge não exclui nem altera a origem.
- A evolução M3 marca MLINEs criadas com o handle da origem. Se a resposta se perder,
  uma nova tentativa recupera a entidade já criada em vez de duplicá-la.
- Não há afirmação de sucesso em runtime até o checklist ser executado.
