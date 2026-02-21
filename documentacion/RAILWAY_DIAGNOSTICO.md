# Diagnóstico cuando la app no carga en Railway

Si la app no responde (ni `/health` ni la web):

## 1. Revisar los logs del deploy

En Railway: **Tu proyecto** → **Deployments** → **último deploy** → **View Logs**.

Busca estas líneas (se escriben al arrancar):

- `[Lucas] Iniciando...` → El proceso arrancó.
- `[Lucas] Puerto: 8080` (o el número que use) → Variable PORT leída.
- `Escuchando en http://0.0.0.0:8080` → La app va a escuchar.
- `[Lucas] A punto de escuchar en 0.0.0.0:8080` → Justo antes de `RunAsync`.

**Si no aparece "Iniciando..."** → El contenedor no está ejecutando la app o falla antes (imagen, entrypoint, crash al cargar).

**Si aparece "Iniciando..." pero no "A punto de escuchar"** → Algo falla entre `builder.Build()` y `app.RunAsync()` (p. ej. excepción al configurar rutas o middleware). Revisa si hay una excepción en rojo en los logs.

**Si aparecen todas** → La app está escuchando; el problema puede ser puerto, proxy o health check de Railway.

## 2. Comprobar variables de entorno en Railway

En el servicio: **Variables**.

- Debe existir **PORT** (Railway suele inyectarla; si no, añade `PORT=8080`).
- Opcional: `ASPNETCORE_URLS=http://0.0.0.0:8080` para forzar el puerto.

## 3. Health check y puerto público

- **Settings** del servicio → **Networking**: el puerto público debe coincidir con el que usa la app (por defecto 8080).
- Si hay **Health Check** configurado, la ruta debe ser `/health` y el puerto el correcto.

## 4. Redeploy

Haz **Redeploy** del último deploy (sin cambiar código). A veces el fallo es transitorio (red, disco, plataforma).

## 5. Probar el contenedor en local

```bash
cd LucasWeb.Api
docker build -t lucas-test .
docker run --rm -e PORT=8080 -p 8080:8080 lucas-test
```

Luego abre `http://localhost:8080/health`. Si responde en local pero no en Railway, el problema es de Railway (red, proxy, dominio, etc.).
