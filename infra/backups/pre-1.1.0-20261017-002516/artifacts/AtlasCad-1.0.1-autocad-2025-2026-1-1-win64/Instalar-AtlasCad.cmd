@echo off
setlocal
title Instalador ATLAS CAD
cd /d "%~dp0"

echo ========================================
echo          INSTALADOR ATLAS CAD
echo ========================================
echo.
echo O AutoCAD deve estar fechado.
echo A instalacao sera feita somente para o usuario atual.
echo.

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-AtlasCad.ps1" -BundlePath "%~dp0AtlasCad.bundle"
set "ATLAS_EXIT_CODE=%ERRORLEVEL%"

echo.
if not "%ATLAS_EXIT_CODE%"=="0" (
    echo A instalacao nao foi concluida. Veja a mensagem acima.
    echo Nenhuma versao funcional anterior foi removida.
    echo.
    pause
    exit /b %ATLAS_EXIT_CODE%
)

echo Instalacao concluida com sucesso.
echo Abra sua versao compativel do AutoCAD e execute ATLAS_DIAGNOSTICS.
echo.
pause
exit /b 0
