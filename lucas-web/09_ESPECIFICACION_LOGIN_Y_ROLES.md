# Lucas Web — Especificación Login y roles

**Versión:** 1.0  
**Fecha:** Febrero 2026

---

## Login

### Método
- **PIN** (obligatorio para tablet; opcional en desktop).
- POST `/api/auth/pin` con body `{ "pin": "1234" }`.
- Respuesta: `{ role, userId, token }`.
- El token se usa en peticiones siguientes: header `Authorization: Bearer {token}`.

### Alternativa
- Cookie de sesión (Path=/feedback) si el navegador la acepta; útil cuando la web y la API comparten origen.

---

## Roles y permisos

| Rol | Acceso web completo | Solo feedback |
|-----|--------------------|---------------|
| **admin** | Sí | No |
| **manager** | Sí | No |
| **master** | Sí | No |
| **user** | No | Sí (registro turnos) |

- Los roles admin/manager/master pueden acceder a Registro, Estimaciones y Configuración (si se implementan).
- El rol user solo puede usar la pantalla de feedback (turnos) y no debería ver Registro, Estimaciones ni Configuración.

---

## Flujo

1. Usuario abre la web → pantalla Login (PIN).
2. Introduce PIN → POST `/api/auth/pin`.
3. Si correcto: guardar token en sessionStorage; redirigir a la pantalla principal (o feedback si es user).
4. En cada petición API: incluir `Authorization: Bearer {token}`.
5. Si 401: redirigir a Login.
6. Cerrar sesión: POST `/api/auth/logout`; borrar token; redirigir a Login.

---

## Verificación de sesión

- GET `/api/auth/me` devuelve `{ userId, role }` si hay sesión o token válido.
- Usar para comprobar sesión al cargar la app y para decidir a qué pantallas redirigir según rol.
