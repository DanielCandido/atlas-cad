# Comandos do SteelFlow

O comando principal no AutoCAD e `SF`. Os comandos antigos `STEELFLOW` e os
nomes auxiliares sem sublinhado foram removidos.

## Fluxo principal

1. Execute `SF`.
2. Selecione as entidades de eixo: `LINE`, `ARC` ou `LWPOLYLINE`.
3. Selecione os textos `TEXT` ou `MTEXT` com os nomes dos perfis.
4. Quando houver variantes, escolha CORTE ou PLANTA.

O SteelFlow so remove a geometria de origem depois de confirmar a criacao da
MLINE. Se o estilo nao puder ser instalado ou a MLINE falhar, a origem e
preservada.

## Referencia

| Comando | Funcao |
| --- | --- |
| `SF` | Converte os eixos selecionados em MLINE conforme o perfil associado. |
| `SF_CONFIG` | Configura a pasta da biblioteca. |
| `SF_DIST` | Consulta ou altera a distancia maxima entre texto e eixo. |
| `SF_ARCO` | Consulta ou altera a suavidade usada na conversao de arcos. |
| `SF_LIST` | Gera um arquivo com os estilos disponiveis. |
| `SF_RELOAD` | Limpa o cache e recarrega o catalogo de estilos. |
| `SF_CATALOG` | Exibe o diagnostico do catalogo. |
| `SF_ACTIVATE` | Ativa a licenca do SteelFlow. |
| `SF_SMOKETEST` | Executa o teste tecnico de criacao de MLINE. |

## Diagnostico complementar

- `ATLAS_DIAGNOSTICS`: mostra versoes e o estado da licenca e do catalogo.
- `ATLAS_INSTALL_CATALOG`: instala o catalogo protegido depois da ativacao.
- `ATLAS_LICENSE_STATUS`: mostra o estado e a validade da licenca.
- `ATLAS_LICENSE_RENEW`: renova a autorizacao offline.
