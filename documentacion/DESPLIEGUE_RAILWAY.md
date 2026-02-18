# Guía paso a paso: desplegar Lucas/BETLEM en Railway

**Fecha:** 18/02/2026  
**Objetivo:** Llevar la aplicación Lucas Web (BETLEM) a producción usando Railway como hosting.

---

## Resumen de pasos

| Paso | Acción |
|------|--------|
| 1 | Ajustar la app para usar el puerto dinámico de Railway (`PORT`) |
| 2 | Crear cuenta y proyecto en Railway |
| 3 | Conectar el repositorio y configurar el servicio con Dockerfile |
| 4 | Configurar variables de entorno |
| 5 | Añadir volumen para persistir la base de datos SQLite |
| 6 | Generar dominio y comprobar HTTPS |
| 7 | Probar login y uso de la aplicación |

---

## Paso 1: Ajustar el puerto para Railway

Railway asigna un puerto en tiempo de ejecución (variable de entorno `PORT`, normalmente `8080`). La aplicación está fijada al puerto **5261** en `Program.cs`; hay que hacer que use `PORT` cuando exista.

### Cambio en `LucasWeb.Api/Program.cs`

**Sustituir** la línea:

```csharp
builder.WebHost.UseUrls("http://0.0.0.0:5261");
```

**Por:**

```csharp
var port = Environment.GetEnvironmentVariable("PORT") ?? "5261";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
```

- En local (sin `PORT`) se seguirá usando el puerto **5261**.
- En Railway la app escuchará en el puerto que asigne la plataforma.

---

## Paso 2: Crear proyecto y conectar el repositorio (desde "New project")

Estás en la pantalla **"What would you like to create?"** con varias opciones. Para Lucas/BETLEM lo más sencillo es que Railway construya la app desde tu código en GitHub.

---

### Paso 2.1 — Qué opción elegir

- **Recomendado:** haz clic en **"GitHub Repository"** (la primera opción, con el icono de GitHub).
  - Así Railway se conecta a tu repositorio, detecta el Dockerfile y construye la app cada vez que hagas push. No necesitas subir imágenes a ningún sitio.
- **Alternativa:** si en su lugar eliges **"Docker Image"**, más adelante te pedirá "Connect a Repo" o "Deploy from an Image". Para este proyecto conviene **"Connect a Repo"** igualmente, porque el Dockerfile está dentro del repo.

**Acción:** haz clic en **"GitHub Repository"**.

---

### Paso 2.2 — Si aún no tienes el proyecto en GitHub

Si Railway muestra "No repositories found", crea un repo en GitHub y sube tu código:

1. **En GitHub:** [Create a new repository](https://github.com/new). Nombre ejemplo: `LUCAS_WEB`. **No** marques "Add a README" (debe quedar vacío). Clic en **Create repository**.
2. **Instalar Git** en el PC si no lo tienes: [git-scm.com/download/win](https://git-scm.com/download/win). Reinicia la terminal después de instalar.
3. **En la carpeta del proyecto** (ej. `e:\BETLEM`), abre PowerShell o CMD y ejecuta (sustituye `tvPABLOVIT` y `LUCAS_WEB` por tu usuario y nombre del repo):

   ```powershell
   cd e:\BETLEM
   git init
   git add .
   git commit -m "Primer commit - proyecto BETLEM"
   git branch -M main
   git remote add origin https://github.com/tvPABLOVIT/LUCAS_WEB.git
   git push -u origin main
   ```

   Si te pide autenticación, en GitHub ya no se usa contraseña: crea un **Personal Access Token** (Settings → Developer settings → Personal access tokens) y úsalo como contraseña al hacer `git push`.

4. Vuelve a Railway, **Refresh** en la pantalla de GitHub, y selecciona el repo que acabas de subir.

### Paso 2.3 — Conectar GitHub (cuando ya tengas el repo)

1. Si es la primera vez, Railway te pedirá **autorizar el acceso a GitHub**. Acepta y elige la cuenta donde está el repo.
2. Cuando te muestre la lista de repositorios, **busca y selecciona el que contiene este proyecto** (ej. `LUCAS_WEB`, con la carpeta `LucasWeb.Api` y el Dockerfile).
3. Confirma. Railway creará un proyecto y un **servicio** asociado a ese repo.

---

### Paso 2.4 — Indicar dónde está el Dockerfile

El Dockerfile de Lucas está en **`LucasWeb.Api/`**, no en la raíz del repositorio. Hay que decírselo a Railway:

1. En el proyecto que se acaba de crear, entra en el **servicio** (el recuadro que representa tu app).
2. Arriba o en el menú lateral, abre **"Settings"** (Configuración).
3. Busca la sección **"Source"** o **"Build"**.
4. Configura:
   - **Root Directory** (o "Repository root"): pon **`LucasWeb.Api`** para que el contexto de build sea esa carpeta.
   - **Dockerfile path** (si aparece como campo aparte): **`Dockerfile`** (o `LucasWeb.Api/Dockerfile` si el root no se puede cambiar).
   - Si solo hay un campo "Dockerfile path" y no "Root Directory", pon **`LucasWeb.Api/Dockerfile`**.
5. Guarda los cambios. Railway hará el **primer build** automáticamente.

---

### Paso 2.5 — Resumen de lo que acabas de hacer

- Creaste un **proyecto** en Railway.
- Conectaste tu **repositorio de GitHub** (BETLEM).
- Configuraste el **servicio** para que construya con el Dockerfile que está en `LucasWeb.Api/`.

Cuando el build termine (puede tardar unos minutos), sigue con el **Paso 4** (variables de entorno). El Paso 3 de la guía queda cubierto por estos pasos 2.1–2.4.

---

## Paso 4: Variables de entorno

En el servicio → **Variables** (o **Variables** del proyecto), añade:

| Variable | Valor | Notas |
|----------|--------|--------|
| `LUCAS_DEFAULT_PIN` | *(tu PIN seguro)* | **Obligatorio** en producción. |
| `ConnectionStrings__DefaultConnection` | `Data Source=/app/data/manageros.db` | Para que la BD use el directorio persistente. |
| `Lucas__AllowedOrigins` | *(opcional)* | Orígenes CORS permitidos, separados por coma (ej. `https://app.tudominio.com`). |

No definas `PORT`; Railway la inyecta automáticamente en tiempo de ejecución.

---

## Paso 5: Persistencia de datos (volumen para SQLite)

Sin volumen, los datos en `/app/data` se pierden al reiniciar o redeployar.

1. En el servicio → **Settings** → **Volumes**.
2. Añade un **Volume**:
   - **Mount path**: `/app/data`
3. La base SQLite `manageros.db` y el resto de archivos en `/app/data` quedarán persistentes.

**Nota:** En planes gratuitos Railway puede limitar volúmenes; revisa la documentación actual de Railway.

---

## Paso 6: Dominio público y HTTPS

1. En el servicio → **Settings** → **Networking** (o pestaña **"Generate domain"**).
2. Pulsa **"Generate domain"**. Railway asignará una URL tipo `tu-servicio-xxxx.up.railway.app`.
3. HTTPS suele estar gestionado automáticamente por Railway para ese dominio.

Para un **dominio propio**, en la misma sección añade un **Custom domain** y configura el registro DNS que indique Railway.

---

## Paso 7: Comprobar que todo funciona

1. Abre la URL generada (ej. `https://tu-servicio-xxxx.up.railway.app`).
2. Prueba el **login** con el PIN configurado en `LUCAS_DEFAULT_PIN`.
3. Si la API expone un endpoint de health (ej. `/api/health`), compruébalo también.

---

## Referencias del proyecto

- **API + SPA:** .NET 8 en `LucasWeb.Api`; estáticos y fallback en `wwwroot`.
- **Base de datos:** SQLite en `/app/data/manageros.db` (volumen recomendado).
- **Docker:** `LucasWeb.Api/Dockerfile` y `LucasWeb.Api/docker-compose.yml` para ejecución local.
- **Configuración:** Ver `documentacion/AUDITORIA_HOSTING.md` y `LucasWeb.Api/.env.example`.

---

## Resolución de problemas

- **La app no arranca:** Revisa los **Logs** del servicio en Railway; confirma que escucha en el puerto inyectado (`PORT`).
- **Error de PIN o login:** Comprueba que `LUCAS_DEFAULT_PIN` está definida y sin espacios.
- **Datos perdidos tras redeploy:** Asegúrate de tener un volumen montado en `/app/data`.
- **CORS:** Si accedes desde otro dominio, configura `Lucas__AllowedOrigins` con las URLs permitidas.
