# M6 — empacotamento e preparação da release

[English](M6_RELEASE.md)

## Payloads suportados

O instalador unificado incorpora bundles Win64 separados para AutoCAD 2022–2024
(`R24.1`–`R24.3`), AutoCAD 2025/início de 2026 (`R25.0`–`R25.1`, .NET 8) e
AutoCAD 2026 Update 1.2 ou posterior (`R25.1`, .NET 10).

O AutoCAD descobre bundles abaixo de um diretório
`Autodesk\ApplicationPlugins`. O instalador por usuário usa
`%APPDATA%\Autodesk\ApplicationPlugins`; instalações para todos os usuários usam
o diretório correspondente em ProgramData.

O manifest carrega automaticamente o adaptador .NET e um loader público
pequeno. O loader carrega o bridge público e `STEELFLOW.fas`. O LSP proprietário
é compilado pelo AutoCAD Core Console durante a release e nunca é copiado para o
bundle.

O pipeline completo está em [STEELFLOW_BUILD_TO_EXE.md](STEELFLOW_BUILD_TO_EXE.md).
O plano para suportar outras bibliotecas está em
[LIBRARY_BUILD_AUTOMATION_PLAN.md](LIBRARY_BUILD_AUTOMATION_PLAN.md).

## Build e integridade

`scripts/Build-AtlasRelease.ps1` executa o build Release, compila o FAS, gera o
manifest versionado, copia apenas entradas permitidas, cria `SHA256SUMS.txt`,
valida os bundles incorporados e produz o instalador unificado.

O validador rejeita MLN/MLNX, LSP privado, catálogo em texto claro, chaves
privadas, DLLs da Autodesk, módulos ausentes, caminhos que escapem do bundle,
hashes divergentes e nomes representativos de perfis dentro do pacote
criptografado.

Toda release precisa informar a impressão digital de um certificado
Authenticode. O build assina com SHA-256 e timestamp RFC 3161, depois verifica a
assinatura. A chave privada do certificado nunca é uma entrada do repositório.

## Instalação, upgrade e recuperação

O instalador exige que o AutoCAD esteja fechado, valida o payload, prepara cada
bundle selecionado e faz a substituição atômica. O mesmo executável assinado é
publicado como `Desinstalar-AtlasCad.exe`; nesse modo, ele remove os bundles
gerenciados e preserva os dados locais de licença.

## Diagnóstico e privacidade

`ATLAS_DIAGNOSTICS` grava um relatório protegido por ACL do usuário em
`%LOCALAPPDATA%\AtlasCad\diagnostics`. Ele inclui versões do plugin, runtime e
AutoCAD e códigos de estado. Não inclui tokens, chaves de catálogo, IDs de
ativação, caminhos de desenhos ou geometria.

## Portões restantes da release

- Executar instalação limpa, inicialização automática, upgrade e desinstalação
  no AutoCAD 2026 conforme `tests/autocad/M6_MANUAL_CHECKLIST.md`.
- Usar certificado Authenticode de produção.
- Executar a matriz de aceite em instalações autorizadas do AutoCAD 2022–2025;
  os builds existem, mas o aceite em runtime é um portão separado.

