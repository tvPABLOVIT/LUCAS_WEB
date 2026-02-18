# Acceso por dominio (túnel): https://app.barcelonaradio.org/

## Cómo debe estar configurado el túnel

La app (HTML, JS, CSS y API) la sirve **el mismo backend**. Para que https://app.barcelonaradio.org/ funcione, el túnel tiene que enviar **todo** el tráfico al mismo origen (tu API), no solo las rutas `/api/*`.

- **Correcto:** Todas las peticiones a `https://app.barcelonaradio.org/*` (incluido `/`, `/index.html`, `/js/*`, `/css/*`, `/api/*`) se reenvían al mismo servicio, por ejemplo `http://localhost:5261`.
- **Incorrecto:** Solo reenviar `/api/*` al backend y dejar el resto en otro sitio o sin configurar. Entonces la página principal o los `.js` pueden no cargar y verás pantalla en blanco o errores.

## Ejemplo: Cloudflare Tunnel (cloudflared)

En tu archivo de configuración del túnel (p. ej. `config.yml`), algo así:

```yaml
ingress:
  - hostname: app.barcelonaradio.org
    service: http://localhost:5261
  - service: http_status:404
```

Así, **cualquier** ruta bajo `app.barcelonaradio.org` (/, /index.html, /js/config.js, /api/auth/pin, etc.) va a tu API en el puerto 5261.

## Comprobar que la API está en marcha

En la máquina donde corre la API:

1. La API debe estar arrancada (`dotnet run` o el ejecutable).
2. Debe escuchar en el puerto que use el túnel (p. ej. 5261). Si el túnel apunta a `localhost:5261`, no hace falta que la API escuche en 0.0.0.0.

## Si sigue sin mostrar nada

1. Abre las herramientas de desarrollo (F12) → pestaña **Red**. Recarga la página y mira si alguna petición sale en rojo (404, 502, etc.). Si `/` o `/index.html` o algún `js/...` falla, el túnel no está enviando todo al backend.
2. Pestaña **Consola**: si hay errores de JavaScript (p. ej. “Failed to load resource”), suele ser que esos recursos no llegan al mismo origen que la página.
3. Si la app detecta que no puede cargar (p. ej. no existe `LUCAS_AUTH`), mostrará un mensaje en pantalla pidiendo comprobar que el túnel envíe todo el tráfico al backend.

## Resumen

- **Un solo servicio:** Backend en un puerto (ej. 5261) sirve la web y la API.
- **Túnel:** Todo el host `app.barcelonaradio.org` → ese mismo servicio (ej. `http://localhost:5261`).
- **Frontend:** Usa `window.location.origin`, así que en https://app.barcelonaradio.org/ las llamadas van a ese mismo dominio; no hace falta tocar `config.js` para el dominio.
