# Checklist manual M2 — AutoCAD 2026

Use um DWG descartável. O teste usa somente o estilo público sintético
`ATLAS_M2_TEST_STYLE`.

## Carregamento

- [x] Core, catalog builder e adaptador 2026 compilam sem warnings/erros.
- [ ] DLL M2 carrega com `NETLOAD` e `ATLAS_ENSURE_STYLE` é reconhecido.
- [ ] Cliente recarrega com `APPLOAD` e `ATLASSTYLETEST` é reconhecido.

## Criação e equivalência

- [ ] Primeira execução retorna `created`.
- [ ] O dicionário contém exatamente um `ATLAS_M2_TEST_STYLE`.
- [ ] Três elementos preservam ordem e offsets `10`, `0`, `-10`.
- [ ] Cores ACI, `CONTINUOUS`, preenchimento, miters e caps conferem.
- [ ] Ângulos persistidos equivalem a 90 graus.
- [ ] Segunda execução retorna `existing` sem duplicar ou alterar o estilo.

## Falhas seguras

- [ ] Mesmo nome com propriedade diferente retorna `style_collision`.
- [ ] Linetype inexistente retorna falha sem criar estilo parcial.
- [ ] Desenho ativo diferente do request retorna `drawing_mismatch`.
- [ ] Request inválido é removido e não altera o desenho.

## Catálogo privado

- [x] 673 MLN/680 definições passam pelo parser estrutural.
- [x] 30 colisões são detectadas e bloqueiam a geração.
- [x] Overlay proposto consolida 28 duplicatas e gera 652 estilos únicos.
- [x] Três aliases propostos para as duas colisões foram confirmados.
- [ ] Contagem e propriedades de fixtures privados aprovados conferem no AutoCAD.

