# AutoCAD release matrix

| AutoCAD | Series | Runtime/build | Build | Runtime | Status |
|---|---:|---|---|---|---|
| 2022 | R24.1 | .NET Framework 4.8 | Pending installation/references | Pending | Not supported by current bundle |
| 2023 | R24.2 | .NET Framework 4.8 | Pending installation/references | Pending | Not supported by current bundle |
| 2024 | R24.3 | .NET Framework 4.8 | Pending installation/references | Pending | Not supported by current bundle |
| 2025 | R25.0 | .NET 8 Windows | Separate build pending | Pending | Not supported by current bundle |
| 2026.0 | R25.1 | Runtime-specific build required | Pending exact installation | Pending | Not claimed |
| 2026.1.2+ | R25.1 | .NET 10 Windows | Passed, 0 warnings | M1-M5 passed; M6 pending | Release candidate |

Never infer compatibility from a successful compile against another AutoCAD
version. Each supported row requires the authorized Autodesk references, clean
bundle install, automatic load, STEELFLOW parity, diagnostics, upgrade and
uninstall checks.
