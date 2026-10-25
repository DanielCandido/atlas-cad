# Gerador de catálogo

[English](README.md)

O modo de normalização do M2 lê arquivos MLN privados, aplica overrides
explícitos de colisão e grava um intermediário JSON privado e validado:

```powershell
dotnet run --project packages/catalog-builder -- `
  C:\private\steelflow `
  C:\private\steel.atlascatalog.json `
  --overrides C:\private\catalog-overrides.json
```

O intermediário `.atlascatalog.json` contém propriedades proprietárias em texto
claro e nunca pode ser distribuído. O M5 o transforma em um pacote autenticado
com AES-256-GCM:

```powershell
dotnet run --project packages/catalog-builder -- protect `
  C:\private\steel.atlascatalog.json `
  C:\build\steel.atlascatalog `
  C:\private\catalog-keys.json `
  1
```

O cabeçalho público contém versões, IDs de produto/catálogo, ID da chave, nonce
e hash. O cabeçalho completo é usado como dado autenticado adicional do GCM;
alterações em metadados, ciphertext ou tag impedem a descriptografia.

A chave de 256 bits não é incluída no pacote ou no cliente.
`ATLAS_INSTALL_CATALOG` aceita somente o pacote criptografado, valida em memória
todos os estilos descriptografados e persiste apenas o ciphertext.

