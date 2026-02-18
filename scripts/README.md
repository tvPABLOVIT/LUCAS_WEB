# Scripts

## Tunel Cloudflare (app.barcelonaradio.org)

Para que https://app.barcelonaradio.org/ llegue a tu API local:

1. **cloudflare-token.txt** – Contiene el token del tunel (no se sube a Git).
2. **Iniciar el tunel** (con [cloudflared](https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/install-and-setup/installation/) instalado):
   - PowerShell: `.\scripts\iniciar-tunnel-cloudflare.ps1`
   - Doble clic: `iniciar-tunnel-cloudflare.bat`
3. **Tener la API en marcha** en el puerto 5261 (`dotnet run` en LucasWeb.Api).

El tunel debe estar activo y la API corriendo para poder entrar por el dominio.
