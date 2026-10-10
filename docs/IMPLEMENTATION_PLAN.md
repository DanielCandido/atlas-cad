# ATLAS CAD - implementation plan (Option B)

## Status em 2026-10-09

- M0 concluído: baseline e ativos privados isolados.
- M1 concluído no AutoCAD 2026: bridge LISP/C# e falha segura.
- M2 concluído: catálogo aprovado com 652 estilos, instalação idempotente e
  colisões documentadas.
- M3 concluído no AutoCAD 2026: integração STEELFLOW, paridade 9/9 e fluxo real
  validado com `W150X13`.
- M4 concluído e validado em runtime no AutoCAD 2026: emissão, ativação,
  renovação, assinatura, vínculo ao dispositivo, expiração e revogação.
- M5 concluído e validado em runtime no AutoCAD 2026: pacote AES-256-GCM,
  liberação vinculada ao dispositivo, cache DPAPI e catálogo com 652 estilos.
- M6 implementado como release candidate para AutoCAD 2026.1.2+: bundle
  Autodesk, FAS sem fonte privada, hashes, validação, diagnóstico e scripts de
  instalação/upgrade/desinstalação. O aceite em runtime do bundle está pendente.
- AutoCAD 2022-2025 continua pendente por ausência das instalações/referências;
  o bundle atual declara somente a série R25.1 validada localmente.

## Entregas por marco

### M0 - Baseline e segurança

Inventariar projetos, runtimes Autodesk e comportamento do STEELFLOW; manter
fontes LSP/MLN privadas fora do Git e da distribuição; registrar baseline.

**Aceite:** inventário, build reproduzível e ativos privados excluídos.

### M1 - Bridge C# mínimo

Manter adaptadores separados, contrato versionado, transporte por ID opaco,
payload local protegido, coordenadas WCS explícitas e transação segura.

**Aceite:** C# cria MLINE com estilo sintético; falha preserva a origem.

### M2 - Importação de estilos

Normalizar MLN, rejeitar ambiguidades, instalar `MlineStyle` de modo idempotente
e impedir sobrescrita silenciosa de colisões.

**Aceite:** contagem/propriedades aprovadas e repetição sem duplicação.

### M3 - Integração STEELFLOW

Preservar seleção, perfil, proximidade, arcos, layer/UCS e comandos públicos;
substituir apenas catálogo e criação final pela fronteira C#; manter undo e
origem em qualquer falha.

**Aceite:** matriz LINE/ARC/LWPOLYLINE, fechamento, lock, UCS, retry e falha 9/9.

### M4 - Licenciamento de dispositivo

Emitir entitlements curtos assinados; verificar protocolo, emissor, produto,
módulo, ativação, geração, chave do dispositivo e prazo. Gerar chave privada no
Windows CNG como não exportável, fazer ativação/renovação online, permitir até
72 horas offline e revogar futuras renovações após transferência.

**Aceite:** concluído no AutoCAD 2026. Token ausente/inválido/expirado negado;
cópia de estado negada; transferência impede renovação antiga; expiração offline
documentada.

### M5 - Catálogo protegido

Criar pacote autenticado e criptografado, versionamento, integridade e estratégia
de liberação/rotação de chave. Documentar que estilos persistidos no DWG e o
cliente compilado continuam inspecionáveis.

**Aceite:** concluído no AutoCAD 2026. Adulteração rejeitada, dispositivo sem
licença não acessa operações protegidas e o runtime não contém MLN bruto nem
catálogo JSON em texto claro.

### M6 - Empacotamento e release

Criar Autodesk bundle por versão, assinatura de código, diagnóstico sem dados
sensíveis, upgrade/uninstall e matriz manual AutoCAD 2022-2026.

**Aceite:** instalação limpa, upgrade e remoção reproduzíveis em cada versão.
O release candidate 2026 foi gerado e validado estaticamente; instalação limpa,
autoload, upgrade e uninstall aguardam o checklist manual. As demais versões
aguardam ambientes Autodesk autorizados próprios.

## Fora do protótipo atual

Website comercial, pagamentos, IA, exportação Excel e macOS/Linux. O estado JSON
da API M4 é apenas single-node; PostgreSQL transacional é obrigatório antes de
produção ou múltiplas instâncias.
