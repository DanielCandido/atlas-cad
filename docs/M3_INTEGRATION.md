# M3 — integração privada do STEELFLOW

## Estado

O M3 foi iniciado pela fronteira final de desenho. A cópia privada do STEELFLOW
chama o bridge C# para criar a MLINE e preserva as rotinas existentes de seleção,
associação texto/perfil, discretização de arco, resolução de estilo, layer, UCS,
variáveis e agrupamento de undo.

O adaptador AutoCAD 2026 com essa integração compila com zero warnings e zero
erros. Em 2026-10-09, `ATLAS_INSTALL_CATALOG` validou e instalou os 652 estilos
no AutoCAD 2026, e `ATLASCATALOGTEST` criou com sucesso um estilo real por meio
do catálogo instalado. `SFRELOAD` também recebeu os 652 nomes dentro da cópia M3
do STEELFLOW. O `SFM3SMOKETEST` confirmou em runtime o estilo existente
`HP-200X53C`, a criação da MLINE com handle retornado e a substituição segura da
LINE temporária. A matriz completa de paridade para ARC/LWPOLYLINE, UCS, layers
bloqueados e cancelamentos ainda está pendente.

O harness privado `M3_PARITY_TEST.lsp` automatiza nove verificações de runtime:
LINE aberta, ARC discretizado, LWPOLYLINE aberta/fechada/com bulge, layer
bloqueado com relock, UCS inclinado, retry sem duplicação e falha com preservação
da origem. Todas as entidades ficam no mesmo grupo de undo. Durante a preparação
do harness foi corrigida, somente na cópia M3, a comparação do último vértice de
uma polilinha fechada; o fonte original continua inalterado.

## Integridade do fonte privado

- Fonte original: `docs/knowledge/STEELFLOW/STEELFLOW.lsp`.
- SHA-256 original preservado:
  `28F4700CDBD41ECAD6B1E8BE31B13A512096DB030A237AD47204ED3091ED52AD`.
- Cópia de trabalho ignorada: `fixtures/private/m3/STEELFLOW.m3.lsp`.
- Nenhuma alteração foi feita no original e nenhuma lógica proprietária foi
  reproduzida em arquivos públicos.

## Fronteira de desenho

A cópia privada passa ao `atlas-bridge-draw`:

- pontos e normal em WCS;
- estilo resolvido pela lógica existente;
- layer da entidade de origem;
- flag aberto/fechado;
- handle da origem e identidade do desenho.

O C# valida todo o contrato, confirma que estilo/layer/origem pertencem ao desenho
ativo e cria a MLINE em transação. O LISP só remove a origem depois de receber
`success`, resolver o handle e confirmar que a entidade é realmente uma MLINE.

Se a exclusão da origem falhar, a cópia privada remove a MLINE recém-criada e
registra falha, evitando manter duas geometrias concorrentes.

## Retry e resposta perdida

Quando existe `sourceHandle`, a MLINE recebe XData sob o RegApp `ATLAS_CAD`, com
uma referência ao handle da origem. Antes de criar outra entidade, o adaptador
procura no espaço ativo uma MLINE Atlas com o mesmo estilo, layer e origem.

Assim, se o commit funcionar mas a resposta não chegar ao LISP, uma tentativa
posterior devolve o handle da MLINE existente. A origem continua preservada até
essa confirmação.

## Catálogo e estilos sob demanda

O carregamento MLN legado foi retirado do fluxo ativo da cópia M3. O comando
`ATLAS_INSTALL_CATALOG` valida o `.atlascatalog.json` e instala uma cópia em
`%LocalAppData%\AtlasCad\catalog\v1`, com ACL restrita ao usuário, SYSTEM e
administradores. O arquivo em texto claro continua sendo provisório até o M5.
No pacote de desenvolvimento, o comando localiza automaticamente o catálogo ao
lado da DLL e não depende do diálogo de arquivos ou da variável `FILEDIA`.

`ATLAS_CATALOG_STYLE_NAMES` fornece ao LISP somente os 652 nomes normalizados,
preservando a resolução de perfil já existente sem expor as definições pelo
contrato LISP. Depois que um nome é escolhido, `atlas-bridge-ensure-style` pede
ao C# que localize a definição no catálogo instalado. O `ATLAS_ENSURE_STYLE`
valida e cria apenas aquele estilo; uma definição equivalente retorna `existing`
e uma colisão diferente retorna `style_collision` sem sobrescrever o desenho.

O desenho é iniciado somente depois dessa confirmação. Catálogo ausente,
inválido ou sem o estilo solicitado preserva a entidade de origem.

## Limites atuais

- O licenciamento legado permanece apenas para preservar comportamento; M4 fará
  o licenciamento de produção.
- O XData é metadado de recuperação, não um mecanismo de segurança ou segredo.
- A busca de retry percorre o espaço ativo e será indexada antes de produção caso
  medições mostrem impacto em desenhos grandes.
- AutoCAD 2022 continua sem build/runtime local.
- A instalação e o uso do catálogo ainda precisam do smoke test no AutoCAD 2026;
  a compilação e as validações offline não afirmam paridade de runtime.

