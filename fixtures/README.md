# Fixtures privados

Use `fixtures/private/` somente para cópias locais de LSP, MLN, MLNX, DWG e
evidências fornecidas pelo proprietário. Todo o diretório é ignorado pelo Git e
não pode entrar em artefatos de build ou instaladores.

O diretório legado `docs/knowledge/` recebeu a mesma proteção para manter os
arquivos já fornecidos exatamente onde estão, sem movê-los ou modificá-los.

Antes de commit/push, execute:

```powershell
pwsh -File scripts/verify-private-assets.ps1
```

Não substitua fixtures privados por dados “anonimizados” sem validar que nomes,
geometria e metadados também deixaram de revelar propriedade intelectual.
