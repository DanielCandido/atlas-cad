# Checklist manual M3 — integração STEELFLOW

Executar somente em cópias descartáveis de DWGs e com a cópia privada ignorada.

## Preparação no AutoCAD 2026

Os arquivos necessários estão reunidos em `.tmp/m3-runtime-v4`.

1. `NETLOAD`: `.tmp/m3-runtime-v4/Atlas.AutoCAD.Adapter.2026.dll`.
2. `APPLOAD`: `.tmp/m3-runtime-v4/atlas-bridge.lsp`.
3. Execute `ATLAS_INSTALL_CATALOG`. O catálogo ao lado da DLL é localizado
   automaticamente, sem abrir uma janela de seleção.
4. A mensagem esperada é `Catalog installed and validated: 652 styles`.
5. `APPLOAD`: `.tmp/m3-runtime-v4/STEELFLOW.m3.lsp`.
6. Execute `STEELFLOW` em uma cópia descartável de um DWG representativo.

Para a matriz automatizada, carregue também
`.tmp/m3-runtime-v4/M3_PARITY_TEST.lsp`, execute `SFM3PARITYTEST` e indique um
ponto-base vazio. O resultado esperado é `9 passed; 0 failed`. Um único
`Ctrl+Z` deve remover todas as entidades e o layer temporário.

**Aceitação AutoCAD 2026 concluída em 2026-10-09:** oito cenários passaram na
matriz conjunta e o nono, LWPOLYLINE fechada, passou no teste isolado após a
correção da leitura do bit DXF 70. Resultado consolidado: 9/9.

## Segurança básica

- [x] Hash do STEELFLOW original permanece inalterado.
- [x] Cópia M3 fica em `fixtures/private` e fora do Git.
- [x] Adaptador 2026 compila sem warnings/erros.
- [x] Catálogo regenerado mantém 652 estilos e o SHA-256 aprovado.
- [x] Falha/ausência do bridge mantém a origem.
- [x] Estilo ausente mantém a origem (`catalog_style_not_found`).
- [x] Retry com a mesma origem retorna o mesmo handle e não duplica MLINE.
- [ ] Falha ao excluir origem remove a nova MLINE.

## Paridade geométrica

- [x] LINE aberta no layer normal pelo `SFM3SMOKETEST`.
- [x] Comando `STEELFLOW` completo associa LINE e texto `W150X13`, resolve a
  variante escolhida e conclui a conversão pelo catálogo C#.
- [x] LINE em layer bloqueado é restaurada ao estado original de lock.
- [x] LWPOLYLINE aberta, fechada e com bulge preserva vértices/fechamento.
- [x] ARC preserva a discretização configurada por `SFARCO` (91 vértices).
- [x] UCS inclinado preserva pontos e normal.
- [x] Cancelamento antes da matriz não cria entidades nem abre grupo de undo.
- [x] Identidade divergente é rejeitada com `drawing_mismatch` e preserva a origem.

## Undo e origem

- [x] Sucesso substitui origem temporária por exatamente uma MLINE.
- [x] Um Ctrl+Z desfaz o lote de teste inteiro, inclusive estilo/layer temporários.
- [x] Falha parcial não exclui nenhuma origem não confirmada.
- [x] Reexecução usa a MLINE marcada por XData e retorna o mesmo handle.

## Estilos

- [x] Estilo existente equivalente funciona (`HP-200X53C`, status `existing`).
- [ ] Colisão de estilo falha sem sobrescrever.
- [x] Catálogo aprovado do M2 substitui o carregamento MLN legado no código M3.
- [x] Instalação retorna 652 estilos no AutoCAD 2026.
- [x] Primeiro uso cria somente o estilo escolhido a partir do catálogo.
- [x] Repetição do mesmo estilo retorna `existing` sem duplicar ou alterar.
- [x] `SFRELOAD` recebe os 652 nomes do catálogo no STEELFLOW M3.
- [x] `SFM3SMOKETEST` substitui sua LINE temporária por exatamente uma MLINE.

## Hardening não bloqueante

- [ ] Forçar falha artificial de `entdel` para observar rollback da nova MLINE.
- [ ] Executar colisão material de estilo dentro do AutoCAD; o core e o comando já
  rejeitam esse caso por contrato.
- [ ] Repetir a matriz no AutoCAD 2022 quando as assemblies autorizadas estiverem
  disponíveis.

