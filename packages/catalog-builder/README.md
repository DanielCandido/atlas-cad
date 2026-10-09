# Catalog builder

Ferramenta offline do M2 para ler uma pasta de arquivos MLN, validar e normalizar
todos os estilos em um catálogo JSON intermediário.

```powershell
dotnet run --project packages/catalog-builder/Atlas.CatalogBuilder.csproj -- `
  "C:\private\mlines" `
  "C:\private\build\steel.atlascatalog.json" `
  --overrides "C:\private\catalog-overrides.json"
```

O parser aceita múltiplos estilos por arquivo e preserva offsets, cores ACI,
linetypes, flags/caps e ângulos em graus. Duplicatas materialmente idênticas são
consolidadas. Arquivos malformados e definições diferentes com o mesmo nome
interrompem a geração, exceto quando um overlay identifica explicitamente arquivo,
nome original e novo alias.

O resultado ainda contém propriedades proprietárias em texto claro. A extensão
`.atlascatalog.json` é ignorada e bloqueada pelo verificador de ativos privados;
ela não deve ser versionada nem distribuída. Proteção do catálogo pertence ao M5.

No ambiente de desenvolvimento M3, o catálogo aprovado é instalado pelo comando
`ATLAS_INSTALL_CATALOG`. O adaptador relê e valida o arquivo antes de qualquer
resolução ou criação de estilo; catálogos vazios, grandes demais, duplicados ou
estruturalmente inválidos são rejeitados.
