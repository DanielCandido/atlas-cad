# Checklist manual M3 — integração STEELFLOW

Executar somente em cópias descartáveis de DWGs e com a cópia privada ignorada.

## Preparação no AutoCAD 2026

Os cinco arquivos necessários estão reunidos em `.tmp/m3-runtime-v3`.

1. `NETLOAD`: `.tmp/m3-runtime-v3/Atlas.AutoCAD.Adapter.2026.dll`.
2. `APPLOAD`: `.tmp/m3-runtime-v3/atlas-bridge.lsp`.
3. Execute `ATLAS_INSTALL_CATALOG`. O catálogo ao lado da DLL é localizado
   automaticamente, sem abrir uma janela de seleção.
4. A mensagem esperada é `Catalog installed and validated: 652 styles`.
5. `APPLOAD`: `.tmp/m3-runtime-v3/STEELFLOW.m3.lsp`.
6. Execute `STEELFLOW` em uma cópia descartável de um DWG representativo.

Para a matriz automatizada, carregue também
`.tmp/m3-runtime-v3/M3_PARITY_TEST.lsp`, execute `SFM3PARITYTEST` e indique um
ponto-base vazio. O resultado esperado é `9 passed; 0 failed`. Um único
`Ctrl+Z` deve remover todas as entidades e o layer temporário.

## Segurança básica

- [x] Hash do STEELFLOW original permanece inalterado.
- [x] Cópia M3 fica em `fixtures/private` e fora do Git.
- [x] Adaptador 2026 compila sem warnings/erros.
- [x] Catálogo regenerado mantém 652 estilos e o SHA-256 aprovado.
- [ ] Falha/ausência do bridge mantém a origem.
- [ ] Estilo ausente mantém a origem.
- [ ] Resposta ausente mantém a origem; retry não duplica MLINE.
- [ ] Falha ao excluir origem remove a nova MLINE.

## Paridade geométrica

- [x] LINE aberta no layer normal pelo `SFM3SMOKETEST`.
- [ ] LINE em layer bloqueado é restaurada ao estado original de lock.
- [ ] LWPOLYLINE aberta e fechada preserva vértices/fechamento.
- [ ] ARC preserva a discretização configurada por `SFARCO`.
- [ ] UCS não-world preserva pontos e normal.
- [ ] Cancelamento restaura variáveis, locks e undo.
- [ ] Troca de desenho não aceita resposta no documento errado.

## Undo e origem

- [x] Sucesso substitui origem temporária por exatamente uma MLINE.
- [ ] Um Ctrl+Z desfaz o lote inteiro.
- [ ] Falha parcial não exclui nenhuma origem não confirmada.
- [ ] Reexecução após resposta perdida usa a MLINE marcada por XData.

## Estilos

- [x] Estilo existente equivalente funciona (`HP-200X53C`, status `existing`).
- [ ] Colisão de estilo falha sem sobrescrever.
- [x] Catálogo aprovado do M2 substitui o carregamento MLN legado no código M3.
- [x] Instalação retorna 652 estilos no AutoCAD 2026.
- [x] Primeiro uso cria somente o estilo escolhido a partir do catálogo.
- [x] Repetição do mesmo estilo retorna `existing` sem duplicar ou alterar.
- [x] `SFRELOAD` recebe os 652 nomes do catálogo no STEELFLOW M3.
- [x] `SFM3SMOKETEST` substitui sua LINE temporária por exatamente uma MLINE.

