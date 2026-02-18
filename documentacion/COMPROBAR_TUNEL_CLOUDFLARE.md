# Comprobar por qué no carga la app con el túnel (HEALTHY pero no funciona)

## 1. Comprobar que la API responde en local

En el mismo PC donde corre la API, abre en el navegador:

- **http://localhost:5261/api/health**

Debe devolver algo como: `{"status":"ok","service":"LucasWeb.Api"}`.

Si **no** responde, la API no está en marcha o no escucha en 5261 → reinicia la API.

---

## 2. Comprobar que el túnel llega a la API

Abre en el navegador (desde cualquier sitio):

- **https://app.barcelonaradio.org/api/health**

- **Si responde** `{"status":"ok",...}` → el túnel y la ruta están bien; el fallo puede ser la página principal (/) o caché. Prueba **https://app.barcelonaradio.org/** con Ctrl+Shift+R o en incógnito.

- **Si no responde** (error, timeout, 502, 503) → la configuración del **Public Hostname** del túnel es casi seguro la causa.

---

## 3. Revisar la configuración del túnel "app" en Cloudflare

1. Entra en **Cloudflare Zero Trust** (o **Networks** → **Connectors** → **Cloudflare Tunnels**).
2. Abre el túnel **"app"** (el que está HEALTHY).
3. Ve a la pestaña **"Public Hostname"** (o **"Routes"** / **"Routing"**).
4. Debe haber **al menos una ruta** con:
   - **Subdomain / Hostname:** `app` (para que sea app.barcelonaradio.org) o el dominio completo según cómo lo tengas.
   - **Domain:** `barcelonaradio.org` (o el dominio que uses).
   - **Service type:** `HTTP`.
   - **URL:** `http://localhost:5261` o `http://127.0.0.1:5261` (sin path, sin barra final).

5. Si la URL está en blanco, es otra (ej. otro puerto), o tiene un **path** (ej. `http://localhost:5261/api`), **cámbiala** a exactamente:
   - `http://localhost:5261`
   Así todo el tráfico (/, /index.html, /js/..., /api/...) irá a tu API.

6. Guarda los cambios. Puede tardar un minuto en aplicarse.

---

## 4. Resumen

| Prueba | Qué significa |
|--------|----------------|
| localhost:5261/api/health OK | La API está bien. |
| app.barcelonaradio.org/api/health OK | El túnel y el hostname público están bien; si / no carga, prueba sin caché. |
| app.barcelonaradio.org/api/health falla | Revisa Public Hostname del túnel "app": URL = `http://localhost:5261`. |

Si tras corregir la URL del servicio sigue sin funcionar, indica qué ves exactamente al abrir https://app.barcelonaradio.org/ y https://app.barcelonaradio.org/api/health (mensaje de error o pantalla en blanco).
