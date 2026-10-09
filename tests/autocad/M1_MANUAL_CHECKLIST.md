# Checklist manual M1 — AutoCAD 2022 e 2026

Use DWGs descartáveis e o estilo sintético `ATLAS_TEST_MLINE`. Salve evidências
somente em `tests/autocad/baseline/private/`.

## Identificação

- [x] AutoCAD completo 2026.1.2, build gerenciado `25.1.179.0.0`, registrado.
- [x] Runtime alvo do AutoCAD 2026.1.2 confirmado: .NET 10.
- [x] DLL 2026 compilada com as assemblies da mesma instalação do AutoCAD.
- [x] `NETLOAD` conclui sem erro e `ATLAS_DRAW_MLINE` é reconhecido.
- [x] `atlas-bridge.lsp` carrega e `ATLASBRIDGETEST` é reconhecido.

## Caminho feliz

- [x] LINE equivalente: dois pontos WCS criam uma MLINE no layer solicitado.
- [x] O response contém `success=true` e devolve o handle da nova MLINE.
- [x] Estilo, scale `1.0` e justification `Zero` conferem em `LIST`.
- [x] Normal WCS da MLINE confere em `ENTGET`: `(0.0 0.0 1.0)`.
- [ ] A entidade de origem permanece inalterada.
- [x] Request e response são removidos depois do retorno ao LISP.

## Geometria e contexto

- [x] Três ou mais pontos criam todos os vértices na ordem recebida (quatro vértices, handle `CF0E`).
- [x] Request fechado produz MLINE fechada (flag DXF `71`, bit `2`); o caso inicial aberto permaneceu aberto.
- [ ] UCS rotacionado: o LISP transforma pontos e normal para WCS corretamente.
- [x] Layer informado por nome funciona (`0` no teste registrado).
- [ ] Layer informado por handle funciona.
- [ ] Nome e handle de layers diferentes são rejeitados.
- [ ] Desenho ativo diferente de `expectedDrawingIdentity` é rejeitado.

## Falhas seguras

- [ ] Request ID com caracteres fora de `[a-f0-9]` é rejeitado sem ler arquivo.
- [ ] JSON inválido produz resposta de falha e remove o request.
- [ ] Versão de protocolo diferente de `1` é rejeitada.
- [ ] Menos de dois pontos distintos é rejeitado.
- [ ] `NaN`, infinito, escala zero e normal nula são rejeitados.
- [ ] Pontos fora do plano da normal são rejeitados.
- [ ] Mais de 10.000 pontos ou payload maior que 1 MiB são rejeitados.
- [x] Estilo ausente é rejeitado com `style_not_found` sem criar entidade.
- [ ] Estilo diferente de `ATLAS_TEST_MLINE` é rejeitado.
- [ ] Layer, source handle ou desenho inexistente/incorreto é rejeitado.
- [ ] Layer bloqueado/frozen falha sem modificar a origem.
- [ ] Cancelamento no prompt não cria entidade.

## Segurança e atomicidade

- [x] Pasta `%LOCALAPPDATA%\AtlasCad\bridge\v1` não é reparse point.
- [x] ACL permite usuário atual, SYSTEM e Administrators, sem outros usuários.
- [x] As três DLLs Autodesk não aparecem na pasta de output do plugin 2026.
- [ ] Falha antes do commit não deixa MLINE parcial.
- [x] Um `UNDO` da criação remove a MLINE; o handle `CF0D` passou a retornar `nil`.

## Matriz de aceite

| Caso | 2022 | 2026 |
| --- | --- | --- |
| Build | [ ] | [x] |
| `NETLOAD` | [ ] | [x] |
| Caminho feliz | [ ] | [x] |
| UCS/layer/closed | [ ] | [ ] |
| Falhas seguras | [ ] | [ ] |
| Cleanup/ACL | [ ] | [ ] |

Não marque o M1 como validado em runtime enquanto as duas colunas não estiverem
concluídas ou enquanto a matriz suportada não for formalmente reduzida.
