# Lucas Web — Migración de datos y despliegue

**Versión:** 1.0  
**Fecha:** Febrero 2026

---

## Migración de datos

### Modo A (actual, PC local)
- **BD:** SQLite en `%LocalAppData%\ManagerOS\manageros.db`.
- **Configuración:** settings.json en la misma carpeta.
- **Migración:** No hay migración entre sistemas; la app Windows y el Backend usan la misma BD y settings.
- Para mover a otro PC: copiar la carpeta `%LocalAppData%\ManagerOS` (manageros.db, settings.json, estimaciones-cache.json, google-credentials.json si existe).

### Modo B (VPS, opcional)
- **BD:** En un servidor (PostgreSQL, SQL Server, etc.). Implica migrar esquema y datos desde SQLite.
- **Herramientas:** EF Core Migrations para crear esquema en el nuevo motor; scripts o herramientas para migrar datos (ej. SQLite → CSV → importar en PostgreSQL).
- **settings.json:** Sustituir por variables de entorno o configuración en servidor (ConnectionString, claves API, etc.).

---

## Despliegue Backend

### Modo A (PC local)
1. Ejecutar `dotnet run --project src/ManagerOS.Backend --urls "http://0.0.0.0:5261"` o usar el ejecutable publicado.
2. La app Windows inicia el Backend automáticamente al abrir.
3. Túnel Cloudflare: `cloudflared tunnel --url http://localhost:5261` (quick tunnel) o `cloudflared tunnel run --token "..."` (túnel con nombre).

### Modo B (VPS)
1. Publicar: `dotnet publish src/ManagerOS.Backend -c Release -o ./publish`.
2. Configurar ConnectionString, settings y variables de entorno.
3. Ejecutar: `dotnet ManagerOS.Backend.dll` o servir con Kestrel/nginx/IIS.
4. Túnel o dominio directo según infraestructura.

---

## Despliegue Frontend (web)

### Opción 1: Estáticos en Backend
- Colocar HTML/JS/CSS en `wwwroot/feedback` (o nueva carpeta `wwwroot/app`) y servir como estáticos.
- La URL sería `/feedback` o `/app`.

### Opción 2: SPA (React/Vue)
- Build de producción (`npm run build`).
- Copiar la carpeta `dist` o `build` a `wwwroot` del Backend o servir con un servidor web estático (nginx, CDN).
- Configurar rutas para SPA (fallback a index.html).

### Opción 3: Servidor separado
- Frontend en un servidor (Vercel, Netlify, etc.).
- Backend en otro (Railway, Render, Fly.io).
- Configurar CORS en Backend para permitir el origen del frontend.
- Usar variables de entorno en frontend para la URL base de la API.
