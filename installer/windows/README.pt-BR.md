# Bundle Windows / Autodesk

[English](README.md)

O M6 produz um Autodesk Application Bundle com adaptadores específicos por
runtime. A release contém:

- adaptador C# específico da versão e core compartilhado;
- arquivos LSP públicos do bridge e do loader;
- SteelFlow proprietário compilado como FAS pelo AutoCAD Core Console;
- `steelflow.atlascatalog` criptografado e autenticado;
- `PackageContents.xml` e inventário SHA-256.

Ela nunca inclui o LSP privado do SteelFlow, MLN/MLNX, catálogo JSON em texto
claro, chaves de catálogo/assinatura ou assemblies da Autodesk.

O artefato para clientes é `Instalador-AtlasCad.exe`, um assistente assinado por
usuário. Ele incorpora todos os payloads suportados, detecta as versões do
AutoCAD, abre o pareamento da conta no navegador e prepara a licença do
dispositivo e o catálogo protegido sem copiar identificador de máquina. Consulte
o [fluxo do instalador](../../docs/INSTALLER_FLOW.md).

## Gerar uma release

Assinatura e verificação são obrigatórias no script atual:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-AtlasRelease.ps1 `
  -Version 1.0.0 `
  -PublicLicenseApiUrl https://api.seudominio.com.br `
  -CertificateThumbprint <IMPRESSAO_DIGITAL_DO_CERTIFICADO_AUTHENTICODE>
```

## Instalação direta para desenvolvimento

Depois de fechar o AutoCAD, um bundle extraído pode ser instalado diretamente:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Install-AtlasCad.ps1 `
  -BundlePath .tmp/installer-payload/autocad-2026-1-2-plus/AtlasCad.bundle
```

O caminho comercial é o instalador unificado assinado. Ele recusa continuar
enquanto `acad.exe` estiver em execução e nunca fecha um desenho não salvo.

## Desinstalação

Para remover os bundles preservando ativação e cache do catálogo:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Uninstall-AtlasCad.ps1
```

`-PurgeUserData` é uma operação destrutiva separada: remove estado do
dispositivo/licença, cache do catálogo e diagnósticos abaixo de
`%LOCALAPPDATA%\AtlasCad`.

