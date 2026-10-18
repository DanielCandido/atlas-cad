# M5 manual checklist - AutoCAD 2026

**Resultado:** aprovado em runtime pelo usuário em 2026-10-09. Ativação,
instalação do catálogo protegido, carregamento dos 652 estilos e fluxo STEELFLOW
funcionando.

Use `.tmp/m5-runtime-v1`. The local licensing API must be running with the
matching signing key and `ATLAS_CATALOG_KEYS_PATH`.

1. Close AutoCAD so the previous adapter DLL can be replaced.
2. `NETLOAD` `Atlas.AutoCAD.Adapter.2026.dll`.
3. `APPLOAD` `atlas-bridge.lsp` and `STEELFLOW.m5.lsp`.
4. Run `SFACTIVATE` with a fresh M5 activation code.
5. Run `ATLAS_INSTALL_CATALOG`; it should automatically select
   `steel.atlascatalog` beside the DLL and report 652 authenticated styles,
   catalog version 1 and key `steel-2026-01`.
   The legacy `%LOCALAPPDATA%\AtlasCad\catalog\v1\steel.atlascatalog.json`
   cache should no longer exist after this successful installation.
6. Run `SFRELOAD`; expected: 652 styles.
7. Run the normal `STEELFLOW` test with `W150X13`; expected: conversion succeeds.
8. Run `ATLAS_LICENSE_STATUS`, disconnect the API, then repeat `SFRELOAD` and the
   conversion; expected: both work from the DPAPI cache during the 72-hour grant.
9. Without a valid license, `SFRELOAD`, style creation and MLINE drawing must be
   denied before proprietary catalog data is returned.
10. Copy `steel.atlascatalog`, flip one byte in a disposable copy and attempt
    `ATLAS_INSTALL_CATALOG`; expected: `catalog_authentication_failed` and the
    installed catalog remains unchanged.
11. Verify the runtime directory contains no `.mln` or `.atlascatalog.json`.

Record the command log. AutoCAD 2022-2024 is not accepted by this checklist.
