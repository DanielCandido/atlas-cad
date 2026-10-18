# Instalador unificado e ativação automática

O release comercial publica `Instalador-AtlasCad.exe`, um assistente gráfico
assinado e self-contained para Windows x64. Ele é o único canal de instalação
e elimina a seleção manual de versão e o transporte de identificadores.

## Fluxo do cliente

1. O cliente baixa o instalador autenticado na Área do Cliente.
2. O assistente detecta AutoCAD 2022–2026 e recusa continuar enquanto
   `acad.exe` estiver aberto.
3. Os bundles compatíveis são validados por SHA-256 e instalados por usuário em
   `%APPDATA%\Autodesk\ApplicationPlugins`.
4. O instalador cria ou reutiliza a chave CNG `AtlasCad.Device.v1`, envia apenas
   a chave pública e abre `/vincular` no navegador.
5. O cliente entra na conta e confirma o código mostrado pelo instalador. Se já
   houver outra máquina, a substituição exige confirmação explícita.
6. A API só efetiva a troca quando recebe a conclusão assinada pela nova
   máquina. O entitlement e a chave do catálogo são salvos nos mesmos formatos
   usados pelo plugin.
7. O AutoCAD abre com licença e catálogo prontos.

O vínculo expira em dez minutos. A chave privada nunca deixa o Windows e não são
coletados serial de hardware, usuário do Windows ou arquivos do cliente.

## Compatibilidade

| AutoCAD | Runtime selecionado | Bundle instalado |
| --- | --- | --- |
| 2022–2024 | .NET Framework 4.8 | `AtlasCad.R24.bundle` |
| 2025 | .NET 8 | `AtlasCad.R25.0.bundle` |
| 2026 até Update 1.1 | .NET 8 | `AtlasCad.R25.1.bundle` |
| 2026 Update 1.2+ | .NET 10 | `AtlasCad.R25.1.bundle` |

Os manifests de 2025 e 2026 são restritos à série detectada durante a instalação
para evitar que dois adaptadores sejam carregados no mesmo AutoCAD.

## Publicação

`Build-AtlasRelease.ps1` incorpora os três payloads no instalador único. O
script embute a URL da API e a chave pública de assinatura, publica em
single-file e exige Authenticode válido com timestamp para qualquer release.

No servidor, configure:

```dotenv
ATLAS_INSTALLER_PATH=/downloads/Instalador-AtlasCad.exe
ATLAS_UNINSTALLER_PATH=/downloads/Desinstalar-AtlasCad.exe
```

O artefato deve ser montado no container da API junto aos demais downloads.

## Recuperação e desinstalação

O instalador registra uma entrada por usuário em “Aplicativos instalados”. A
desinstalação remove os bundles `R24`, `R25.0`, `R25.1` e o bundle legado, mas
preserva `%LOCALAPPDATA%\AtlasCad` para permitir reinstalação.
