@echo off
chcp 65001 >nul
title Lucas Web - PC hosting con tu dominio (Cloudflare Tunnel)
setlocal

set "SCRIPT_DIR=%~dp0"
set "API_DIR=%SCRIPT_DIR%..\LucasWeb.Api"
set "TOKEN_FILE=%SCRIPT_DIR%cloudflare-token.txt"

if not exist "%API_DIR%\LucasWeb.Api.csproj" (
    echo ERROR: No se encuentra LucasWeb.Api en %API_DIR%
    pause
    exit /b 1
)

echo.
echo ========================================
echo  Lucas Web - PC hosting con tu dominio
echo  (API + Cloudflare Tunnel con token)
echo ========================================
echo.

:: Comprobar si existe el archivo con el token
if not exist "%TOKEN_FILE%" (
    echo No se encuentra el archivo con el token:
    echo   %TOKEN_FILE%
    echo.
    echo Sigue la guía: scripts\GUIA_TUNEL_CON_TU_DOMINIO.md
    echo Crea el túnel en Cloudflare Zero Trust, copia el token
    echo y pégalo en un archivo llamado: cloudflare-token.txt
    echo dentro de la carpeta scripts.
    echo.
    pause
    exit /b 1
)

:: 1. Arrancar la API en una ventana nueva
echo [1/2] Arrancando la API...
start "LucasWeb API" cmd /c "cd /d "%API_DIR%" && dotnet run"
if errorlevel 1 (
    echo ERROR: No se pudo arrancar dotnet run.
    pause
    exit /b 1
)

echo [2/2] Esperando 8 segundos a que la API esté lista...
timeout /t 8 /nobreak >nul

:: 2. Ejecutar el túnel con el token (lee la primera línea del archivo)
echo.
echo Iniciando túnel con tu dominio. La URL será la que configuraste (ej. https://lucas.tudominio.com)
echo Para cerrar todo: cierra esta ventana y la ventana "LucasWeb API".
echo.
for /f "usebackq delims=" %%a in ("%TOKEN_FILE%") do (
    cloudflared tunnel run --token %%a
    goto :done
)
:done

pause
