# Guía: usar tu dominio de Cloudflare con el túnel

Con un **dominio en Cloudflare** puedes tener una URL fija (ej. `https://lucas.tudominio.com`) en lugar de la URL aleatoria de trycloudflare.com.

---

## Paso 1 — Entrar en Cloudflare Zero Trust

1. Abre el navegador y ve a: **https://one.dash.cloudflare.com**
2. Inicia sesión con tu cuenta de Cloudflare (la que tiene tu dominio).
3. Si es la primera vez, elige o crea una organización (nombre libre, ej. "Mi empresa"). No hace falta plan de pago para el túnel.

---

## Paso 2 — Crear el túnel

1. En el menú izquierdo: **Networks** (o **Red**) → **Cloudflare Tunnels** (o **Túneles**).
2. Pulsa **Create a tunnel** (Crear un túnel).
3. Elige **Cloudflared** (conector que instalas en tu PC).
4. **Nombre del túnel:** escribe uno, por ejemplo `lucas-web`. Pulsa **Save tunnel** (Guardar túnel).

---

## Paso 3 — Configurar la URL pública (tu dominio)

Antes de cerrar la pantalla del túnel:

1. Busca la sección **Public Hostname** (o **Public hostnames** / **Hostname público**).
2. Pulsa **Add a public hostname** (Añadir hostname público).
3. Rellena:
   - **Subdomain:** el prefijo que quieras, ej. `lucas` (la URL será `https://lucas.tudominio.com`).
   - **Domain:** elige **tu dominio** en el desplegable (el que tienes en Cloudflare).
   - **Service type:** **HTTP**.
   - **URL:** `http://localhost:5261` (puerto donde corre tu API).
4. Guarda ( **Save tunnel** o **Add hostname** ).

---

## Paso 4 — Copiar el token

1. En la misma página del túnel verás un **comando de instalación** para cloudflared, algo como:
   ```bash
   cloudflared service install <TOKEN_MUY_LARGO>
   ```
2. El **token** es la parte larga (empieza por algo como `eyJ...`). **Cópialo completo** (Ctrl+C).
3. Guárdalo en un sitio seguro. Lo usarás para arrancar el túnel desde tu PC.

**Opcional:** Si quieres usar el script con un solo clic, pega el token en el archivo **`cloudflare-token.txt`** dentro de la carpeta **`scripts`** (solo la línea del token, sin comillas ni espacios extra). El script `iniciar-pc-hosting-con-dominio.bat` lo usará.

---

## Paso 5 — Arrancar la API y el túnel con tu dominio

**Opción A — Script con token en archivo (recomendado)**

1. Crea el archivo **`scripts/cloudflare-token.txt`** en la carpeta del proyecto.
2. Pega **solo el token** dentro (una línea, sin comillas).
3. Ejecuta **`scripts/iniciar-pc-hosting-con-dominio.bat`**.  
   Se arrancará la API y el túnel con tu dominio; la URL fija será la que configuraste (ej. `https://lucas.tudominio.com`).

**Opción B — Manual**

1. Arranca la API en una terminal:
   ```bash
   cd LucasWeb.Api
   dotnet run
   ```
2. En **otra** terminal, ejecuta (sustituye `TU_TOKEN` por el token que copiaste):
   ```bash
   cloudflared tunnel run --token TU_TOKEN
   ```
3. Abre en el navegador la URL que configuraste (ej. `https://lucas.tudominio.com`).

---

## Resumen

| Paso | Dónde | Qué hacer |
|------|--------|-----------|
| 1 | https://one.dash.cloudflare.com | Iniciar sesión |
| 2 | Networks → Cloudflare Tunnels | Create tunnel → Cloudflared → nombre (ej. lucas-web) → Save |
| 3 | Mismo túnel → Public Hostname | Subdomain: lucas, Domain: tudominio.com, URL: http://localhost:5261 |
| 4 | Misma página | Copiar el token del comando de instalación |
| 5 | En tu PC | Poner token en `scripts/cloudflare-token.txt` y ejecutar `iniciar-pc-hosting-con-dominio.bat` |

Si algo no aparece (menú, Public Hostname, etc.), puede variar un poco según la cuenta; en ese caso en la ayuda de Cloudflare busca "Create a tunnel" y "Public hostname".
