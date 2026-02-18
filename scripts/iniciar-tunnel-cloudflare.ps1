# Inicia el tunel de Cloudflare (app.barcelonaradio.org -> localhost:5261)
# Ejecutar desde la raiz del proyecto: .\scripts\iniciar-tunnel-cloudflare.ps1

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$tokenFile = Join-Path $scriptDir "cloudflare-token.txt"

if (-not (Test-Path $tokenFile)) {
    Write-Host "No se encuentra $tokenFile" -ForegroundColor Red
    exit 1
}

$token = Get-Content $tokenFile -Raw
$token = $token.Trim()
if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "El archivo del token esta vacio." -ForegroundColor Red
    exit 1
}

Write-Host "Iniciando tunel Cloudflare (app.barcelonaradio.org -> localhost:5261)..." -ForegroundColor Cyan
& cloudflared tunnel run --token $token
