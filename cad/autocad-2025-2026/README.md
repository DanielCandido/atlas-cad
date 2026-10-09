# Adaptador AutoCAD 2026.1.2+

Build `net10.0-windows` do bridge M1/M2, alinhado à instalação local do AutoCAD
2026.1.2. Versões 2025/2026 anteriores à transição para .NET 10 precisam de um
build `net8.0-windows` separado; não use esta DLL nelas.

```powershell
dotnet build Atlas.AutoCAD.Adapter.csproj `
  -p:AutodeskAutoCADRoot="F:\Softwares\AutoCad2026\AutoCAD 2026"
```

As referências Autodesk usam `Private=false`. O build local foi validado com o
SDK .NET 10.0.401 e AutoCAD 2026.1.2 (`25.1.179.0.0`).
