# Lucas Web — Despliegue

Guía para ejecutar y desplegar Lucas Web (Backend + frontend) según el plan en `lucas-web/11_MIGRACION_DATOS_Y_DESPLIEGUE.md`.

---

## Nota

Para **recompilar** el Backend, detén antes el proceso en ejecución (Ctrl+C en la terminal donde corre `dotnet run`); de lo contrario la compilación puede fallar por archivos bloqueados.

---

## Modo A — PC local

### Backend + frontend en uno

1. **Requisitos:** .NET 8 SDK
2. **Ejecutar:**
   ```bash
   cd LucasWeb.Api
   dotnet run
   ```
3. La app escucha en **http://localhost:5261**. El frontend se sirve en la misma URL.
4. **Base de datos:** SQLite en `LucasWeb.Api/manageros.db` (se crea al iniciar).
5. **Usuario por defecto:** admin con PIN **1234** (configurable en `appsettings.json` → `Lucas:DefaultPin`).

### Escuchar en todas las interfaces (para tablet en la red local)

```bash
cd LucasWeb.Api
dotnet run --urls "http://0.0.0.0:5261"
```

Desde otro dispositivo en la misma red: `http://<IP-del-PC>:5261`.

### PC como hosting con Cloudflare Tunnel (acceso desde internet)

**Forma rápida (todo en uno):** ejecuta el script **`scripts/iniciar-pc-hosting.bat`**. Arranca la API y el túnel; en la ventana saldrá la URL pública (tipo `https://xxxx.trycloudflare.com`). Usa esa URL en la tablet. Ver **`scripts/LEEME_PC_HOSTING.md`** para pasos y **qué datos necesitas**.

**Datos que necesitas:**
- **Para usar ya (URL que puede cambiar):** ninguno. Solo instala cloudflared (una vez) y ejecuta el script.
- **Para URL fija con tu dominio:** el **token** del túnel que te da Cloudflare Zero Trust al crear un túnel con nombre. Detalle en `scripts/LEEME_PC_HOSTING.md`.

**Pasos manuales** (si prefieres no usar el script):

1. **Ejecutar la API** (en una terminal): `cd LucasWeb.Api` y `dotnet run`. La app queda en **http://localhost:5261**.
2. **Instalar cloudflared** (una vez): [Descargar cloudflared](https://github.com/cloudflare/cloudflared/releases) o `winget install Cloudflare.cloudflared`.
3. **Crear el túnel** (en otra terminal): `cloudflared tunnel --url http://localhost:5261`. Usa la URL que muestre (ej. `https://xxxx.trycloudflare.com`) en la tablet.
4. **URL fija con tu dominio:** guía paso a paso en **`scripts/GUIA_TUNEL_CON_TU_DOMINIO.md`**. Luego ejecuta **`scripts/iniciar-pc-hosting-con-dominio.bat`** (poniendo el token en `scripts/cloudflare-token.txt`).

### Servicios de Windows — siempre en segundo plano (recomendado para este PC)

Para que la **API y el túnel Cloudflare** se ejecuten en segundo plano y **arranquen solos cada vez que enciendas el PC** (máximo tiempo online):

1. **Requisitos:** .NET 8 SDK, cloudflared en el PATH, token en `scripts/cloudflare-token.txt`.
2. **Instalar una sola vez (como Administrador):**
   - Clic derecho en **`scripts/instalar-servicios-windows.bat`** → **Ejecutar como administrador**.
   - O en PowerShell (admin): `.\scripts\instalar-servicios-windows.ps1`
3. El script publica la API, crea el servicio **LucasWebApi** y instala el servicio **cloudflared** con tu token. Ambos quedan en **inicio automático**.
4. A partir de ahí, cada vez que enciendas el PC la app estará disponible en **http://localhost:5261** y en tu URL pública (ej. **https://app.barcelonaradio.org**). La única forma de que no esté activa es con el PC apagado.
5. **Desinstalar servicios:** ejecutar como administrador **`scripts/desinstalar-servicios-windows.ps1`**.

---

## Modo B — VPS o servidor

### Publicar y ejecutar

1. **Publicar:**
   ```bash
   cd LucasWeb.Api
   dotnet publish -c Release -o ./publish
   ```
2. Copiar a la máquina destino: carpeta `publish`, `appsettings.json` (y opcionalmente `appsettings.Production.json`).
3. **Configurar:** ConnectionString en `appsettings.json` (ej. PostgreSQL o mantener SQLite con ruta absoluta).
4. **Ejecutar:**
   ```bash
   cd publish
   dotnet LucasWeb.Api.dll --urls "http://0.0.0.0:5261"
   ```
   O usar un reverse proxy (nginx, Caddy) que apunte al puerto 5261.

### Variables de entorno

- `ASPNETCORE_ENVIRONMENT`: `Production`
- `ConnectionStrings__DefaultConnection`: cadena de conexión a la BD (si no se usa solo appsettings.json).

### Docker (opcional)

Desde la carpeta del proyecto:

```bash
cd LucasWeb.Api
docker build -t lucas-web-api .
docker run -p 5261:5261 -v "%cd%:C:\data" -e ConnectionStrings__DefaultConnection="Data Source=C:/data/manageros.db" lucas-web-api
```

En Linux/Mac usar `$(pwd)` en lugar de `%cd%`. La BD se persiste en el volumen. Para producción usar variables de entorno o un volumen nombrado.

---

## Frontend solo (sin Backend en la misma máquina)

Si el frontend se sirve desde otro host (ej. CDN, Vercel):

1. Copiar el contenido de `lucas-web-app/` (o `LucasWeb.Api/wwwroot/`) al servidor estático.
2. En `js/config.js` definir la URL del API:
   ```js
   API_BASE: 'https://tu-backend.com'
   ```
3. El Backend debe tener CORS habilitado para el origen del frontend (en LucasWeb.Api ya está `AllowAnyOrigin` en desarrollo; en producción conviene restringir).

---

## Migración de datos (Modo A)

- **Misma PC:** No hay migración; Backend y app Windows pueden usar la misma BD si apuntan al mismo `manageros.db` (ej. `%LocalAppData%\ManagerOS\manageros.db`).
- **Mover a otro PC:** Copiar la carpeta `%LocalAppData%\ManagerOS` (manageros.db, settings.json, etc.) al nuevo PC y ajustar rutas si es necesario.

## Migración de datos (Modo B)

- Sustituir SQLite por PostgreSQL (u otro): crear esquema con EF Core Migrations o scripts, y migrar datos (export/import o herramientas ETL) según `lucas-web/11_MIGRACION_DATOS_Y_DESPLIEGUE.md`.

---

## Resumen de puertos y URLs

| Entorno        | URL Backend / app      |
|----------------|------------------------|
| Local          | http://localhost:5261  |
| Red local      | http://&lt;IP&gt;:5261 |
| Tras túnel     | https://xxx.trycloudflare.com (o tu dominio) |
| VPS            | https://tu-dominio.com (con reverse proxy)   |
