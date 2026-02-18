@echo off
REM Inicia el tunel de Cloudflare (app.barcelonaradio.org -> localhost:5261)
cd /d "%~dp0"
set TOKENFILE=%~dp0cloudflare-token.txt

if not exist "%TOKENFILE%" (
    echo No se encuentra cloudflare-token.txt en scripts\
    pause
    exit /b 1
)

set /p TOKEN=<"%TOKENFILE%"
echo Iniciando tunel Cloudflare...
cloudflared tunnel run --token %TOKEN%
pause
