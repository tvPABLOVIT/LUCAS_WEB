#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Instala Lucas Web (API + túnel Cloudflare) como servicios de Windows.
  Los servicios arrancan al encender el PC y se ejecutan en segundo plano.

.NOTES
  Ejecutar como Administrador (clic derecho -> Ejecutar como administrador).
#>
$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$ApiDir = Join-Path $ProjectRoot "LucasWeb.Api"
$PublishDir = Join-Path $ApiDir "publish"
$TokenFile = Join-Path $ScriptDir "cloudflare-token.txt"

$ServiceNameApi = "LucasWebApi"
$ServiceNameTunnel = "cloudflared"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Lucas Web - Instalacion como servicios" -ForegroundColor Cyan
Write-Host "  (API + tunel Cloudflare)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Comprobar que existe el proyecto
if (-not (Test-Path (Join-Path $ApiDir "LucasWeb.Api.csproj"))) {
    Write-Host "ERROR: No se encuentra LucasWeb.Api en $ApiDir" -ForegroundColor Red
    exit 1
}

# 2. Token del tunel
if (-not (Test-Path $TokenFile)) {
    Write-Host "ERROR: No existe el archivo con el token de Cloudflare:" -ForegroundColor Red
    Write-Host "  $TokenFile" -ForegroundColor Red
    Write-Host "Crea el tunel en Cloudflare Zero Trust y pega el token en ese archivo." -ForegroundColor Yellow
    Write-Host "Ver: scripts\GUIA_TUNEL_CON_TU_DOMINIO.md" -ForegroundColor Yellow
    exit 1
}

# 3. Detener procesos y servicio que puedan bloquear archivos
Write-Host "[1/6] Deteniendo procesos y servicios existentes (dotnet, cloudflared, LucasWebApi)..." -ForegroundColor Gray
$svcApi = Get-Service -Name $ServiceNameApi -ErrorAction SilentlyContinue
if ($svcApi -and $svcApi.Status -eq "Running") {
    Stop-Service -Name $ServiceNameApi -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
}
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process -Name "cloudflared" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3

# 3b. Borrar carpeta publish para evitar bloqueos y rutas anidadas
if (Test-Path $PublishDir) {
    Write-Host "  Eliminando carpeta publish anterior..." -ForegroundColor Gray
    Remove-Item -Path $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    if (Test-Path $PublishDir) {
        Write-Host "ERROR: No se pudo borrar $PublishDir (algo lo esta usando). Cierra todo lo que use la API y vuelve a intentar, o reinicia el PC." -ForegroundColor Red
        exit 1
    }
}

# 4. Publicar la API
Write-Host "[2/6] Publicando la API en $PublishDir ..." -ForegroundColor Gray
Push-Location $ApiDir
try {
    dotnet publish -c Release -o $PublishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish fallo" }
} finally {
    Pop-Location
}

# 5. Servicio de la API
Write-Host "[3/6] Configurando servicio de Windows para la API ($ServiceNameApi)..." -ForegroundColor Gray
$dotnetExe = (Get-Command dotnet -ErrorAction Stop).Source
$dllPath = Join-Path $PublishDir "LucasWeb.Api.dll"
if (-not (Test-Path $dllPath)) {
    Write-Host "ERROR: No se genero la DLL en $dllPath" -ForegroundColor Red
    exit 1
}

# Si el servicio ya existe, eliminarlo primero
$existing = Get-Service -Name $ServiceNameApi -ErrorAction SilentlyContinue
if ($existing) {
    if ($existing.Status -eq "Running") {
        Stop-Service -Name $ServiceNameApi -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
    }
    & sc.exe delete $ServiceNameApi | Out-Null
    Start-Sleep -Seconds 3
}

# Crear servicio: New-Service maneja mejor rutas con espacios que sc.exe
$binaryPath = "`"$dotnetExe`" `"$dllPath`""
try {
    New-Service -Name $ServiceNameApi -BinaryPathName $binaryPath -DisplayName "Lucas Web API" -StartupType Automatic -ErrorAction Stop
} catch {
    Write-Host "ERROR: No se pudo crear el servicio $ServiceNameApi" -ForegroundColor Red
    Write-Host "  Detalle: $($_.Exception.Message)" -ForegroundColor Gray
    exit 1
}
# URL en la que escucha la API (necesaria cuando se ejecuta como servicio)
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceNameApi" -Name "Environment" -Value "ASPNETCORE_URLS=http://localhost:5261" -Type MultiString -ErrorAction SilentlyContinue

# 6. Servicio del tunel Cloudflare
Write-Host "[4/6] Configurando servicio de Windows para el tunel ($ServiceNameTunnel)..." -ForegroundColor Gray
$token = (Get-Content $TokenFile -Raw).Trim()
# Si ya estaba instalado, desinstalar para actualizar token
$cloudflaredSvc = Get-Service -Name $ServiceNameTunnel -ErrorAction SilentlyContinue
if ($cloudflaredSvc) {
    if ($cloudflaredSvc.Status -eq "Running") { Stop-Service -Name $ServiceNameTunnel -Force }
    cloudflared service uninstall
    Start-Sleep -Seconds 2
}
cloudflared service install $token
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: No se pudo instalar el servicio cloudflared. Comprueba que cloudflared esta en el PATH." -ForegroundColor Red
    exit 1
}

# 7. Iniciar servicios (el tunel puede depender de que la API este arriba; dar un retraso)
Write-Host "[5/6] Iniciando servicio de la API..." -ForegroundColor Gray
Start-Service -Name $ServiceNameApi
Start-Sleep -Seconds 5
Write-Host "[6/6] Iniciando servicio del tunel Cloudflare..." -ForegroundColor Gray
Start-Service -Name $ServiceNameTunnel

Write-Host ""
Write-Host "Listo. Los servicios estan instalados y en ejecucion." -ForegroundColor Green
Write-Host ""
Write-Host "  - API:        http://localhost:5261" -ForegroundColor White
Write-Host "  - Tunel:      app.barcelonaradio.org (o la URL que tengas configurada)" -ForegroundColor White
Write-Host ""
Write-Host "Arrancan solos cada vez que enciendas el PC." -ForegroundColor Cyan
Write-Host "Para desinstalar: ejecuta scripts\desinstalar-servicios-windows.ps1 como administrador." -ForegroundColor Gray
Write-Host ""
