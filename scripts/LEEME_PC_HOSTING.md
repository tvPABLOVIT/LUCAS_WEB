# PC como hosting con Cloudflare — Qué hacer y qué datos necesitas

## Qué hacer (todo en uno)

1. **Instala cloudflared** (solo la primera vez):
   - Opción A: [Descargar cloudflared](https://github.com/cloudflare/cloudflared/releases) (Windows 64-bit), descomprimir y añadir la carpeta al PATH.
   - Opción B: En PowerShell o CMD como administrador: `winget install Cloudflare.cloudflared`

2. **Arranca todo:**  
   Haz doble clic en **`iniciar-pc-hosting.bat`** (está en la carpeta `scripts` del proyecto).

3. Se abrirá:
   - Una ventana con la **API** (no la cierres).
   - Esta ventana, donde al poco saldrá una **URL** tipo `https://xxxx.trycloudflare.com`.

4. **Copia esa URL** y ábrela en la tablet del trabajo (o en cualquier navegador). Esa es tu app; no hace falta configurar nada más.

5. Para apagar: cierra la ventana de la API y esta ventana.

---

## Datos que necesitas

### Opción normal (URL que cambia cada vez)

- **Ninguno.**  
  No hace falta cuenta de Cloudflare ni datos tuyos. El script arranca la API y un “quick tunnel”; la URL sale en esta ventana (tipo `https://xxxx.trycloudflare.com`). Cada vez que vuelvas a ejecutar el script la URL puede ser distinta; la copias y la usas en la tablet.

### Opción URL fija con tu dominio (ej. `https://lucas.tudominio.com`)

Solo si quieres una **URL fija** con **tu dominio** en Cloudflare:

1. Entra en [Cloudflare Zero Trust](https://one.dash.cloudflare.com/) (con la misma cuenta donde tienes el dominio).
2. **Access** → **Tunnels** → **Create a tunnel**.
3. Elige **Cloudflared** y pon un nombre (ej. `lucas-web`).
4. En **Public Hostname** añade un hostname, por ejemplo: `lucas` (subdominio) en tu dominio, y que apunte a `http://localhost:5261` (o a la IP de tu PC si lo configuras así).
5. Al final te darán un **token** (línea larga que empieza por algo como `eyJ...`).
6. **Ese token es el único dato que necesitas.**  
   Guárdalo en un sitio seguro.

Para usar ese token en lugar del quick tunnel:

- En una terminal (en la carpeta del proyecto o donde tengas `cloudflared`):
  ```bash
  cloudflared tunnel run --token PEGA_AQUI_TU_TOKEN
  ```
- La API tiene que estar ya corriendo (por ejemplo con `iniciar-pc-hosting.bat` pero **sin** dejar que llegue a ejecutar `cloudflared tunnel --url ...`, o arrancando solo la API con `dotnet run` en `LucasWeb.Api` y luego en otra terminal el comando de arriba con tu token).

Si quieres, en una próxima versión se puede añadir al script la opción de usar tu token (por ejemplo guardado en un archivo o variable de entorno) para que todo siga siendo “un solo clic” con URL fija.

---

## Resumen

| Qué quieres              | Datos que necesitas | Qué ejecutar                          |
|--------------------------|---------------------|----------------------------------------|
| Usar la app desde fuera  | Ninguno             | `iniciar-pc-hosting.bat`              |
| URL fija con tu dominio  | Token del túnel     | API + `cloudflared tunnel run --token …` |

Si algo no arranca (API o túnel), revisa que tengas **.NET 8 SDK** y **cloudflared** instalados y en el PATH.
