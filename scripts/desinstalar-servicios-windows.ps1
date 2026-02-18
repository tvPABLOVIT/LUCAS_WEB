#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Desinstala los servicios de Windows de Lucas Web (API + tunel Cloudflare).
#>
$ErrorActionPreference = "Stop"
$ServiceNameApi = "LucasWebApi"
$ServiceNameTunnel = "cloudflared"

Write-Host ""
Write-Host "Desinstalando servicios de Lucas Web..." -ForegroundColor Cyan
Write-Host ""

foreach ($name in @($ServiceNameApi, $ServiceNameTunnel)) {
    $svc = Get-Service -Name $name -ErrorAction SilentlyContinue
    if ($svc) {
        if ($svc.Status -eq "Running") {
            Write-Host "  Deteniendo $name..." -ForegroundColor Gray
            Stop-Service -Name $name -Force
            Start-Sleep -Seconds 2
        }
        if ($name -eq $ServiceNameTunnel) {
            cloudflared service uninstall
        } else {
            sc.exe delete $name
        }
        Write-Host "  $name desinstalado." -ForegroundColor Green
    } else {
        Write-Host "  $name no estaba instalado." -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Listo. Para volver a usar la app en ventanas, ejecuta scripts\iniciar-pc-hosting-con-dominio.bat" -ForegroundColor Cyan
Write-Host ""
