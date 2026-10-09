# Plano de integração opção B — STEELFLOW

## Evidências da inspeção dos arquivos privados
- STEELFLOW mantém geometria LINE/ARC/LWPOLYLINE, associação de TEXT/MTEXT, seleção de estilo, controle de layer, UCS, undo e restauração de variáveis.
- `SF-LoadLibrary` varre MLN/MLNX e `SF-LoadStyleFile` registra estilos no dicionário `ACAD_MLINESTYLE`.
- `SF-DrawMline` chama `_.MLINE` com pontos transformados WCS→UCS; a entidade original só é excluída quando a nova MLINE é detectada.
- Há múltiplos estilos dentro de um mesmo MLN. Os arquivos MLN originais e o LSP original NÃO são distribuídos neste pacote.

## Limite do protótipo entregue
O comando C# `ATLAS_MLINE_BRIDGE` cria MLINE com estilo previamente carregado e pontos escolhidos interativamente, mas está bloqueado por padrão e ainda não substitui as rotinas do STEELFLOW. Não há licenciamento real, catálogo criptografado, carregador MLN ou passagem de argumentos LISP→C#.

## Ordem de implementação
1. Testar manualmente o comando C# em AutoCAD 2022 e 2026 com estilo de teste não proprietário.
2. Implementar importador MLN offline (ferramenta de build) com testes de contagem, offsets, flags e linetypes.
3. Empacotar catálogo em AES-GCM, usando chaves liberadas apenas após ativação válida. Não embutir segredo global no cliente.
4. Implementar emissão de licença assinada pelo servidor e prova de posse de chave de dispositivo.
5. Implementar comandos C# não interativos `ATLAS_ENSURE_STYLE` e `ATLAS_DRAW_MLINE` e contrato seguro de transferência de pontos (WCS, fechamento, layer, estilo).
6. Criar cópia privada de STEELFLOW, substituir somente `SF-LoadLibrary`, `SF-ResolveStyle` quando necessário, e `SF-DrawMline`; preservar o resto.
7. Testar LINE, ARC, LWPOLYLINE, UCS rotacionado, layers bloqueados, desfazer, entidades inválidas, estilos já presentes, falhas e cópia de dispositivo.

## Critérios de aceite
- Nunca apagar geometria original se criação da MLINE falhar.
- Estilo inexistente não modifica o desenho.
- Estilos gerados são equivalentes aos MLN de origem, incluindo estilos múltiplos por arquivo.
- Bloqueio de licença falha fechado.
- Revogação offline não pode ser imediata: usar validade curta (ex.: 72h).

## Segurança
O catálogo pode ser protegido em repouso, mas estilos inseridos no DWG não são secretos. C# pode ser descompilado. O sistema aumenta o custo de cópia, não garante impossibilidade de engenharia reversa.
