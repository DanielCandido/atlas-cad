# M2 — importação de estilos MLINE

## Estado

O parser/normalizador offline, a ferramenta de catálogo e o comando
`ATLAS_ENSURE_STYLE` estão implementados. O core e o adaptador AutoCAD 2026
compilam com zero warnings e zero erros. A criação do estilo pelo comando ainda
precisa de uma execução de runtime no AutoCAD antes de ser considerada validada.

## Parser MLN

`MlnStyleParser` interpreta blocos `MLSTYLE` e preserva:

- nome e descrição;
- flags de preenchimento, miters e caps;
- cor de preenchimento em ACI;
- ângulos inicial/final em graus;
- ordem, offset, cor ACI e linetype de cada elemento;
- múltiplos estilos no mesmo arquivo.

O parser impõe limites de tamanho, quantidade de estilos e elementos, aceita
UTF-8/BOM e rejeita códigos fora da sequência, valores não finitos, cores fora de
`0..256`, flags desconhecidas, contagens inconsistentes e nomes duplicados no
mesmo arquivo. O catálogo consolida duplicatas materialmente idênticas e rejeita
colisões case-insensitive com propriedades diferentes, salvo quando um overlay
privado declara um alias explícito para a definição e o arquivo de origem.

Fixtures públicos e sintéticos ficam em `tests/Atlas.Core.Tests/Fixtures`. Nenhum
valor da biblioteca privada foi copiado para esses arquivos.

## Auditoria privada local

A leitura local de 2026-10-09 encontrou:

| Métrica | Resultado |
| --- | ---: |
| Arquivos MLN | 673 |
| Definições de estilo | 680 |
| Elementos | 3.123 |
| Nomes únicos case-insensitive | 650 |
| Colisões de nome | 30 |
| Duplicatas materialmente idênticas | 28 |
| Definições conflitantes | 2 |
| Falhas estruturais do parser | 0 |

As 28 duplicatas idênticas são consolidadas. As duas colisões conflitantes foram
analisadas e receberam aliases aprovados em um overlay privado: uma espessura
`4,75` que usava nome `4,25`, e dois perfis UE de larguras `100`/`85` que
compartilhavam o mesmo nome. O overlay também normaliza a espessura UE para
`2,65`, coerente com arquivo, descrição e família.

Com o overlay aprovado, os 673 arquivos geram 652 estilos únicos. A ordem de diretório
continua não sendo usada como regra implícita e os MLN originais não são editados.

## Ferramenta offline

```powershell
dotnet run --project packages/catalog-builder/Atlas.CatalogBuilder.csproj -- `
  "C:\private\mlines" `
  "C:\private\build\steel.atlascatalog.json" `
  --overrides "C:\private\catalog-overrides.json"
```

O arquivo `.atlascatalog.json` é um intermediário em texto claro. A extensão é
ignorada pelo Git e bloqueada pelo verificador de ativos privados. Ela não entra
em artefatos de cliente; criptografia/autenticação do catálogo pertence ao M5.

## `ATLAS_ENSURE_STYLE`

O comando recebe pelo bridge privado uma definição normalizada e:

1. valida request ID, desenho ativo e todos os campos do estilo;
2. verifica uma colisão antes de carregar linetypes ou modificar o desenho;
3. retorna `existing` se a definição persistida for equivalente;
4. retorna `style_collision` sem sobrescrever se houver qualquer diferença;
5. carrega linetypes ausentes via API suportada e cria o `MlineStyle` em transação;
6. converte graus do MLN para radianos somente na fronteira da API Autodesk.

O cliente público contém `ATLASSTYLETEST`, que instala um estilo puramente
sintético chamado `ATLAS_M2_TEST_STYLE`. Duas execuções consecutivas devem retornar
`created` e depois `existing`, demonstrando idempotência.

## Validações executadas

- `m2-core-smoke: OK`: fixtures single/multi, colisão e round-trip JSON.
- 673 arquivos privados e 680 definições passaram pela validação estrutural.
- Catalog builder gerou 3 estilos a partir dos 2 fixtures públicos.
- O catálogo privado normalizado com overlay gerou 652 estilos únicos.
- O artefato aprovado tem SHA-256
  `B28012360BAB26A36B031604B000886BBFBBBB6D1E66B580C175639477A79FEA`.
- Core, catalog builder e adaptador 2026 compilaram com zero warnings/erros.

## Pendências

- validar `ATLASSTYLETEST` dentro do AutoCAD 2026 e comparar propriedades;
- validar no AutoCAD 2026 a integração já implementada do catálogo com o M3;
- restaurar a suíte xUnit em ambiente com os pacotes disponíveis;
- validar o adaptador legado com AutoCAD 2022/.NET Framework 4.8 Developer Pack.

