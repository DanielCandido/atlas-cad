# Arquitetura V1

[English](architecture.md)

## Fluxo de runtime

O `STEELFLOW.m4.lsp` preserva o fluxo estabelecido de seleção e identificação
dos perfis e envia IDs opacos de request ao adaptador específico da versão do
AutoCAD. O adaptador é responsável pela consulta ao catálogo, criação exata do
estilo MLINE, criação da MLINE e validação da licença. Uma falha no bridge ou na
licença preserva a geometria de origem.

O adaptador moderno possui alvos `net8.0-windows` e `net10.0-windows`; o
adaptador do AutoCAD 2022–2024 usa `net48`. O aceite fora do ambiente instalado
do AutoCAD 2026.1.2 continua fazendo parte da matriz de versões do M6.

## Fluxo de licenciamento

A API ASP.NET Core emite entitlements de 72 horas assinados com ECDSA P-256. Uma
chave ECDSA P-256 é criada por dispositivo no Windows CNG como não exportável.
A ativação e a renovação exigem uma prova assinada pela chave do dispositivo; o
plugin valida localmente assinatura do servidor, protocolo, emissor, produto,
módulo, fingerprint do dispositivo e validade.

A API de produção armazena comércio, licenças, vínculos de dispositivos, nonces
antirreplay e auditoria de webhooks no PostgreSQL, usando transações e restrições
únicas. O deploy também exige backups criptografados, chaves protegidas, HTTPS e
procedimento documentado de rotação.

## Marcos restantes

- M5 concluído e aceito no AutoCAD 2026 com pacote AES-256-GCM, liberação de
  chave vinculada à prova do dispositivo e cache DPAPI de 72 horas.
- M6: bundle Autodesk, assinatura, upgrades e matriz AutoCAD 2022–2026.
- Conta web, checkout Asaas, portal do cliente e download protegido formam a
  camada comercial ao redor do protocolo de licenciamento.

