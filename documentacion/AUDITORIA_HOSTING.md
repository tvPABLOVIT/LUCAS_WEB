10# Auditoría completa para hosting — Lucas Web / BETLEM

**Fecha:** 18/02/2026  
**Objetivo:** Asegurar que el proyecto está listo para subir a un hosting y funcionará correctamente en producción.

**Actualización (mejoras aplicadas):** CORS configurable en producción, DataSeeder no crea admin si PIN vacío, limpieza periódica de tokens expirados, Dockerfile con `/app/data`, `docker-compose.yml` con volumen para la BD, `appsettings.Production.json` y `.env.example`.

---

## 1. Resumen ejecutivo

| Área | Estado | Acción prioritaria |
|------|--------|--------------------|
| **Compilación** | ✅ OK | Ninguna |
| **Configuración** | ✅ Ajustado | `appsettings.Production.json` creado; definir `Lucas__DefaultPin` en hosting |
| **Seguridad** | ✅ Mejorado | PATCH settings y POST seed restringidos por rol; CORS por `Lucas:AllowedOrigins` en producción |
| **Base de datos** | ✅ Docker | `docker-compose.yml` con volumen; limpieza de tokens expirados (HostedService) |
| **Docker** | ✅ Listo | Dockerfile con `/app/data`; docker-compose con volumen `lucas-data` |
| **Frontend** | ✅ OK | `config.js` usa mismo origen; no hay URLs hardcodeadas |
| **Health / monitoreo** | ✅ OK | `/api/health` público y sin auth |

**Conclusión:** El programa está listo para hosting. Definir **Lucas__DefaultPin** (y opcionalmente **Lucas__AllowedOrigins**) en el entorno de despliegue.

---

## 2. Estructura del proyecto

- **LucasWeb.Api** — Backend .NET 8 (API + SPA en `wwwroot`)
- **LucasWeb.Api.Tests** — Tests xUnit (predicciones, etc.)
- **LucasCuadranteParser** — Python (parser de cuadrantes; opcional para la API)
- **documentacion** — Docs y auditorías
- **scripts** — Túneles (Cloudflare), servicios Windows

La API sirve tanto las rutas `/api/*` como los estáticos (HTML, JS, CSS) y el fallback a `index.html`. Para un túnel o hosting, todo el tráfico debe ir al mismo origen (puerto 5261).

---

## 3. Configuración y variables de entorno

### 3.1 Archivos de configuración

| Archivo | Existe | Uso |
|---------|--------|-----|
| `appsettings.json` | ✅ | Base: SQLite, Lucas (PIN 1234), CuadranteParser |
| `appsettings.Development.json` | ✅ | Logging Debug, misma BD |
| **`appsettings.Production.json`** | ❌ | **No existe** — recomendado para producción |

**Problema:** En `appsettings.json` está `"DefaultPin": "1234"`. Si en el hosting no se sobreescribe (variable de entorno o `appsettings.Production.json`), el primer acceso seguirá siendo con PIN 1234.

**Recomendación:**

1. Crear `appsettings.Production.json` con:
   - `Lucas:DefaultPin` vacío o valor fuerte (mejor inyectar por variable de entorno).
   - `Lucas:SeedDemoData: false` (ya es false por defecto en `LucasOptions`).
   - `ConnectionStrings:DefaultConnection` si quieres otra ruta para la BD (ej. volumen en Docker).
2. En hosting, definir **Lucas__DefaultPin** (y opcionalmente **ConnectionStrings__DefaultConnection**) por variables de entorno para no dejar 1234.

### 3.2 Connection string

- Por defecto: `Data Source=manageros.db;Cache=Shared`
- La ruta es relativa al directorio de trabajo. En **Docker** el directorio de trabajo es `/app`; si no usas volumen, cada reinicio pierde la BD.
- En **Windows Service** el código usa `ContentRootPath = AppContext.BaseDirectory`, así que `manageros.db` queda junto al ejecutable.

### 3.3 Puertos y URLs

- **Program.cs:** `UseUrls("http://0.0.0.0:5261")` — correcto para escuchar en todas las interfaces (Docker, VM, túnel).
- **Dockerfile:** `ENV ASPNETCORE_URLS=http://+:5261` y `EXPOSE 5261` — coherente (el `UseUrls` en código tiene prioridad si existe).

---

## 4. Seguridad — puntos críticos para producción

### 4.1 Endpoints con permisos insuficientes

| Endpoint | Atributo actual | Riesgo | Recomendación |
|----------|-----------------|--------|----------------|
| **PATCH /api/settings** | `[Authorize]` | Cualquier usuario autenticado (incl. "user") puede cambiar configuración (API keys, Google, PIN, etc.) | `[Authorize(Roles = "admin,manager,master")]` |
| **POST /api/seed/demo** | `[Authorize]` | Cualquier usuario puede borrar todos los días de ejecución y cargar datos de prueba | `[Authorize(Roles = "admin")]` o al menos `admin,manager,master` |

El resto de controladores están bien acotados (Users/Database solo admin; Events/Weather por roles).

### 4.2 CORS

- Actual: `AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()`.
- En producción es recomendable restringir orígenes: `WithOrigins("https://tu-dominio.com")` (y variantes si usas www, etc.) para evitar que cualquier sitio web pueda llamar a tu API.

### 4.3 PIN por defecto

- Ya comentado: no dejar `DefaultPin: "1234"` en producción. Usar variable de entorno o `appsettings.Production.json` sin valor por defecto débil.
- Opcional: en `DataSeeder`, si `options.DefaultPin` está vacío en producción, no crear usuario hasta la primera configuración (o crear con PIN temporal que obligue a cambiar). Actualmente si no hay usuarios se crea uno con el PIN configurado.

### 4.4 Autenticación y tokens

- Login por PIN; tokens Bearer; BCrypt para el hash del PIN — correcto.
- `BearerAuthMiddleware` rellena `context.User` con claims (userId, role). Los `[Authorize]` y `[Authorize(Roles = "...")]` funcionan correctamente.
- Health y Auth (login, logout, me) están con `[AllowAnonymous]` donde corresponde.

---

## 5. Base de datos (SQLite)

### 5.1 Persistencia en Docker

- El **Dockerfile** actual no monta ningún volumen. La BD `manageros.db` se crea dentro del contenedor y se pierde al reiniciar.
- **Acción obligatoria para hosting con Docker:** montar un volumen (o bind mount) en la ruta donde se escribe `manageros.db` (por defecto el directorio de trabajo de la app, p. ej. `/app`).

Ejemplo:

```yaml
# docker-compose o similar
volumes:
  - lucas-data:/app
```

o

```bash
docker run -v /ruta/local/manageros:/app ...
```

Y en producción asegurar que `ConnectionStrings__DefaultConnection` apunte a un archivo dentro de ese volumen (ej. `Data Source=/app/data/manageros.db`) si quieres separar datos de binarios.

### 5.2 Backups

- No hay backups programados de `manageros.db`. En hosting conviene un cron o tarea que copie la BD periódicamente (con rotación) a un almacenamiento seguro.

---

## 6. Docker

### 6.1 Dockerfile actual

- Build multi-stage con .NET 8, publish, runtime con `aspnet:8.0` — correcto.
- **Falta:** volumen o instrucción para persistir `manageros.db`.
- **Recomendación:** documentar en README o en este documento que al ejecutar el contenedor debe montarse un volumen para la BD.

### 6.2 Google Sheets y archivos locales

- `GoogleSheetSyncService` lee la URL de la hoja y la ruta de credenciales desde Settings (BD). Si la ruta es relativa, se resuelve respecto a `AppContext.BaseDirectory` (dentro del contenedor sería `/app`).
- En Docker, las credenciales de Google (JSON) deben estar en un volumen montado o incorporadas en la imagen (menos recomendable por seguridad). Lo habitual es montar un directorio con `google-credentials.json` y en Configuración poner la ruta (ej. `/app/secrets/google-credentials.json`).

---

## 7. Frontend y despliegue

### 7.1 API_BASE y mismo origen

- `wwwroot/js/config.js`: `API_BASE` vacío y uso de `window.location.origin` cuando no hay base. Si la app se sirve desde el mismo host y puerto que la API (caso actual: todo en 5261), las peticiones van al mismo origen — correcto para túnel y hosting monolítico.

### 7.2 Recursos estáticos

- `index.html` carga CSS/JS con `?v=N` para cache busting. Favicon redirigido a `logo.png` en `Program.cs`. No hay referencias a localhost ni IPs fijas en el frontend revisado.

---

## 8. Health y comprobaciones

- **GET /api/health** — `[AllowAnonymous]`, devuelve `{ "status": "ok", "service": "LucasWeb.Api" }`. Útil para el hosting o un balanceador para comprobar que la app está viva.
- La compilación **Release** del proyecto termina correctamente (solo advertencias NU1603 por versión de Google.Apis.Sheets.v4, no bloquean).

---

## 9. Checklist pre-subida a hosting

Usar esta lista antes de subir el proyecto:

- [ ] **Configuración de producción**
  - [ ] Crear `appsettings.Production.json` (o equivalente) con `Lucas:SeedDemoData: false` y sin PIN débil.
  - [ ] Definir en el hosting `Lucas__DefaultPin` (y opcionalmente `ConnectionStrings__DefaultConnection`) por variables de entorno.

- [ ] **Seguridad**
  - [ ] Restringir **PATCH /api/settings** a roles `admin`, `manager`, `master`.
  - [ ] Restringir **POST /api/seed/demo** a `admin` (o admin/manager/master).
  - [ ] (Opcional) Configurar CORS con orígenes concretos en producción.

- [ ] **Base de datos**
  - [ ] Si usas Docker: montar volumen para el directorio donde se escribe `manageros.db`.
  - [ ] (Opcional) Programar backups de `manageros.db`.

- [ ] **Despliegue**
  - [ ] Comprobar que el puerto expuesto (5261) coincide con el que usa el túnel o el host (ej. Cloudflare → `http://localhost:5261`).
  - [ ] Si usas Google Sheets: tener credenciales en un path accesible desde el contenedor/servidor (volumen o ruta configurada en Settings).
  - [ ] **Obligatorio:** Definir variable de entorno `Lucas__DefaultPin` en el hosting antes del primer arranque (en `appsettings.Production.json` el PIN viene vacío a propósito; si no se define, el primer usuario se crearía con PIN vacío).

- [ ] **Pruebas rápidas tras desplegar**
  - [ ] `GET https://tu-dominio/api/health` → 200 y `"status":"ok"`.
  - [ ] Cargar la raíz y comprobar que carga HTML y JS sin errores en consola.
  - [ ] Login con PIN y al menos una navegación (dashboard o configuración según rol).

---

## 10. Resumen de cambios recomendados (orden sugerido)

1. **Crítico para hosting con Docker:** Añadir volumen para `manageros.db` y documentarlo.
2. **Crítico para seguridad:** Restringir PATCH settings y POST seed/demo por rol (ver sección 4.1).
3. **Importante:** Crear `appsettings.Production.json` y usar variables de entorno para el PIN (y opcionalmente connection string).
4. **Recomendado:** CORS con orígenes concretos en producción.
5. **Opcional:** Backups programados de la BD; limpieza de tokens expirados; rate limiting en login (como en la auditoría previa).

Con estos puntos cubiertos, el proyecto queda listo para subir al hosting y debería funcionar de forma estable y segura en producción.

---

## 11. Ejemplo docker-compose con persistencia

Para no perder la base de datos al reiniciar el contenedor:

```yaml
version: '3.8'
services:
  lucas-api:
    build: .
    ports:
      - "5261:5261"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Lucas__DefaultPin=TU_PIN_SEGURO
      - ConnectionStrings__DefaultConnection=Data Source=/app/data/manageros.db;Cache=Shared
    volumes:
      - lucas-data:/app/data
volumes:
  lucas-data:
```

Si la connection string apunta a `/app/data/manageros.db`, el directorio `/app/data` debe existir en el contenedor (el Dockerfile actual no lo crea; puedes añadir `RUN mkdir -p /app/data` en el Dockerfile o usar `Data Source=manageros.db` y montar el volumen en `/app` para persistir todo el directorio de trabajo).
