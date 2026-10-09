# Adaptador AutoCAD 2022–2024

Build `net48` do bridge M1. Requer .NET Framework 4.8 Developer Pack e as
assemblies gerenciadas de uma instalação autorizada do AutoCAD 2022.

```powershell
dotnet build Atlas.AutoCAD.Adapter.csproj `
  -p:AutodeskAutoCADRoot="C:\Program Files\Autodesk\AutoCAD 2022"
```

As referências Autodesk usam `Private=false` e não são redistribuídas. Este alvo
ainda não foi compilado nem executado nesta máquina.
