# Cliente AutoLISP público do bridge

`atlas-bridge.lsp` implementa o transporte público do M1/M2/M3. Ele não contém
rotinas, catálogo ou segredos do STEELFLOW.

O ponto de integração é `atlas-bridge-draw`, que recebe pontos e normal já em WCS
e retorna uma lista com `success` e `handle`/`error`. O comando
`ATLASBRIDGETEST` permite um smoke test interativo com dois pontos,
`ATLASBRIDGETESTPATH` testa múltiplos vértices e fechamento, e `ATLASSTYLETEST`
valida criação/idempotência de um estilo sintético público.

No M3, `atlas-bridge-ensure-style` solicita um estilo pelo nome ao catálogo
instalado no C#. O LISP não recebe offsets, cores ou demais propriedades privadas.
`ATLAS_INSTALL_CATALOG` instala/valida o catálogo e
`ATLAS_CATALOG_STYLE_NAMES` fornece apenas os nomes usados na resolução de perfil.

As fontes proprietárias continuam restritas às pastas privadas ignoradas.
