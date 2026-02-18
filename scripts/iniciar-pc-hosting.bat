@echo off
chcp 65001 >nul
title Lucas Web - PC como hosting con Cloudflare
setlocal

:: Ruta de la API (carpeta del script = BETLEM\scripts, API = BETLEM\LucasWeb.Api)
set "SCRIPT_DIR=%~dp0"
set "API_DIR=%SCRIPT_DIR%..\LucasWeb.Api"
if not exist "%API_DIR%\LucasWeb.Api.csproj" (
    echo ERROR: No se encuentra LucasWeb.Api en %API_DIR%
    pause
    exit /b 1
)

echo.
echo ========================================
echo  Lucas Web - PC como hosting
echo  (API + Cloudflare Tunnel)
echo ========================================
echo.

:: 1. Arrancar la API en una ventana nueva
echo [1/2] Arrancando la API en una ventana nueva...
start "LucasWeb API" cmd /c "cd /d "%API_DIR%" && dotnet run"
if errorlevel 1 (
    echo ERROR: No se pudo arrancar dotnet run. ¿Tienes .NET 8 SDK instalado?
    pause
    exit /b 1
)

:: 2. Esperar a que la API esté lista
echo [2/2] Esperando 8 segundos a que la API esté lista...
timeout /t 8 /nobreak >nul

:: 3. Comprobar si cloudflared está instalado
where cloudflared >nul 2>&1
if errorlevel 1 (
    echo.
    echo ERROR: cloudflared no está instalado o no está en el PATH.
    echo Instálalo desde: https://github.com/cloudflare/cloudflared/releases
    echo O con: winget install Cloudflare.cloudflared
    echo.
    echo La API ya está corriendo en esta PC. Puedes abrir http://localhost:5261
    echo Para el túnel, instala cloudflared y ejecuta en otra terminal:
    echo   cloudflared tunnel --url http://localhost:5261
    pause
    exit /b 0
)

:: 4. Iniciar el túnel (esta ventana mostrará la URL pública)
echo.
echo Iniciando túnel Cloudflare. La URL pública aparecerá abajo.
echo Usa esa URL en la tablet o en cualquier navegador.
echo.
echo Para cerrar todo: cierra esta ventana y la ventana "LucasWeb API".
echo.
cloudflared tunnel --url http://localhost:5261

pause
