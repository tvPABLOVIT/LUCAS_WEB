# Lucas Web — Frontend

Frontend web para **Lucas (Manager OS)** según el plan en `../lucas-web/`.  
Login por PIN, pantallas Registro, Estimaciones y Configuración.

## Cómo ejecutar

1. **Opción A — Servido por el Backend**  
   Copia el contenido de `lucas-web-app/` en la carpeta de archivos estáticos del Backend ASP.NET Core (por ejemplo `wwwroot/lucas`). La app usará la misma origen y no necesitas cambiar la URL del API.

2. **Opción B — Servidor estático local**  
   Sirve la carpeta con cualquier servidor (por ejemplo `npx serve .` o Live Server).  
   Edita `js/config.js` y define la URL del Backend:
   ```js
   API_BASE: 'http://localhost:5261'
   ```
   Ajusta el puerto si tu Backend usa otro.

3. **CORS**  
   Si el frontend y el Backend están en orígenes distintos, el Backend debe permitir CORS para ese origen.

## Rutas

- **Login:** PIN → `POST /api/auth/pin` → token en `sessionStorage` y header `Authorization: Bearer {token}`.
- **Registro:** Cargar/crear/editar día con `GET/POST/PATCH /api/execution`.
- **Estimaciones:** `GET /api/dashboard/week`, `/api/estimaciones`, `/api/predictions/next-week`, `/api/recommendations`.
- **Configuración:** Placeholder; la configuración avanzada se gestiona desde la app Windows.

## Roles

Solo los roles **admin**, **manager** y **master** acceden al dashboard completo (Registro, Estimaciones, Configuración). El rol **user** solo tiene acceso a feedback (pantalla de turnos en la app actual).
