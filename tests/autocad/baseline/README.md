# Captura do baseline AutoCAD (M0)

As evidências ainda não foram capturadas: esta máquina não tem AutoCAD instalado.
Execute este protocolo em AutoCAD licenciado 2022 e 2026 antes de encerrar o M0.
Use uma cópia descartável dos desenhos e mantenha tudo em
`tests/autocad/baseline/private/<ano>/<caso>/`, que é ignorado pelo Git.

## Preparação

1. Registre versão/edição exata do AutoCAD, `ABOUT`, unidades do desenho e hash
   do LSP usado.
2. Inicie um DWG limpo e carregue somente a cópia privada do STEELFLOW e a
   biblioteca privada aprovada.
3. Ative log da linha de comando. Não inclua caminhos, chaves ou conteúdo de
   catálogo nas capturas compartilháveis.
4. Para cada caso, salve `before.dwg`, `after.dwg`, `before.png`, `after.png`,
   `entities.txt` e `result.json` na pasta privada.

## Casos mínimos

| ID | Entrada/condição | Verificar |
| --- | --- | --- |
| `line-wcs` | LINE, UCS World, layer comum | perfil/estilo, pontos, layer, origem e MLINE |
| `arc-wcs` | ARC com discretização padrão | ordem e quantidade de segmentos, tolerância visual |
| `lwpolyline-open` | LWPOLYLINE aberta com trecho reto e bulge | pontos, abertura e arco |
| `lwpolyline-closed` | LWPOLYLINE fechada | fechamento sem segmento duplicado |
| `ucs-rotated` | UCS rotacionado e com origem deslocada | pontos WCS/UCS, normal e posição final |
| `layer-locked` | origem em layer bloqueado | unlock temporário e restauração do lock |
| `undo` | conversão válida seguida de um UNDO | uma ação intuitiva restaura o estado anterior |
| `missing-style` | perfil sem estilo correspondente | origem preservada e nenhuma MLINE criada |
| `cancel` | cancelamento durante seleção/pergunta | entidades, layer, variáveis e undo preservados |

Repita os casos geométricos com distância automática e, pelo menos, um valor
fixo configurado por `SFDIST`.

## Dados a registrar

Em `entities.txt`, capture antes e depois, usando `LIST` e inspeção equivalente:

- handle, tipo e layer da origem;
- coordenadas dos vértices em WCS, flag closed, normal e elevação;
- handle, estilo, escala, justificação e normal da MLINE;
- existência da origem após sucesso/falha;
- estado locked/frozen do layer antes/depois;
- resultado após um UNDO e um REDO.

Preencha `result.json` a partir de `result.example.json`. Não versione o arquivo
preenchido: handles, nomes, geometria e screenshots permanecem privados.

## Critério de conclusão

Cada ano deve ter todos os casos, dados de entidade e screenshots revisados por
uma segunda pessoa. Divergências entre 2022 e 2026 devem ser registradas antes de
definir os contratos do M1.
