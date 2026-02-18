# 05 — Autenticación y roles

Login por PIN, sesión, cookies, tokens Bearer y roles (user, admin, manager, master) para la app web.

---

## 1. Métodos de acceso

- **Login por PIN:** Principal para la app (y para la vista tablet). El usuario introduce un PIN de 4+ dígitos; el backend comprueba el hash BCrypt contra `Users.PinHash`.
- **Login por email/contraseña:** Opcional (en el proyecto actual se usa en la app Windows con PasswordHash). Para la web puede limitarse a admin o no exponerse.

En esta documentación el foco es **PIN** para usuarios y tablet.

---

## 2. Almacenamiento del PIN

- **Nunca** guardar el PIN en claro.
- Guardar solo **hash BCrypt** del PIN en `Users.PinHash`.
- Al crear/usar PIN: mínimo 4 caracteres; recomendable solo numérico para tablet.
- Verificación: `BCrypt.Verify(pinIntroducido, pinHashDeLaBD)`.

Si `PinHash` es NULL o vacío, ese usuario **no puede** entrar por PIN.

---

## 3. Flujo de login por PIN

1. Cliente envía `POST /api/auth/pin` con `{ "pin": "1234" }`.
2. Backend busca usuarios activos con `PinHash` no nulo; para cada uno verifica BCrypt con el PIN recibido.
3. Si hay coincidencia:
   - Crear sesión: guardar en sesión `UserId` y `Role`.
   - Opcionalmente generar un token (GUID) y guardarlo en memoria con expiración (ej. 24 h); devolverlo en la respuesta para uso Bearer.
   - Responder 200 con `{ "role": "admin", "userId": "guid", "token": "..." }`.
4. Si no hay coincidencia: 401 con `{ "error": "PIN incorrecto" }`.

---

## 4. Sesión (cookie)

- **Cookie de sesión:** HttpOnly, SameSite=Lax, Secure en HTTPS. Nombre ej. `ManagerOS.Session`.
- **Path:** Puede limitarse a `/feedback` si la tablet solo usa esa ruta; en app web completa suele ser `/`.
- **Timeout:** Ej. 24 horas de inactividad.
- **Contenido:** En el servidor, asociado al ID de sesión: `UserId`, `Role`.

En cada petición a `/api/*` (excepto login): leer sesión; si no hay sesión, comprobar header `Authorization: Bearer <token>` y validar token en memoria.

---

## 5. Token Bearer (tablet / API)

- Tras login por PIN, el backend puede devolver un `token` en la respuesta.
- El cliente (p. ej. la vista tablet) guarda el token (p. ej. en sessionStorage) y envía en cada petición: `Authorization: Bearer <token>`.
- El backend mantiene un “TokenStore” en memoria: `token → (UserId, Role, Expiry)`. Si la petición trae Bearer y no hay sesión, validar token y considerar usuario autenticado.
- **Logout:** Además de borrar sesión, eliminar el token del store si se envía el mismo Bearer en el logout.

En despliegue con varios nodos, el token store debe ser compartido (Redis/cache) o usar JWT firmado en lugar de GUID en memoria.

---

## 6. Roles

| Rol | Descripción | Permisos en app web |
|-----|-------------|----------------------|
| **user** | Usuario de sala/tablet | Solo pantalla de feedback (4 preguntas por turno). No ve Registro, Estimaciones ni Configuración. |
| **admin** | Administrador | Registro, Dashboard, Estimaciones, Configuración, usuarios. |
| **manager** | Igual que admin en el proyecto actual | Mismos permisos que admin. |
| **master** | Creado en primer arranque (setup) | Mismos que admin; en algunos flujos se usa para “único usuario inicial”. |

Comprobación en backend: para rutas de Registro, Estimaciones, Configuración y listado de usuarios, comprobar `role` ∈ { "admin", "manager", "master" }. Si es "user", devolver 403 o no exponer esas pantallas en el front.

---

## 7. GET /api/auth/me

- Si hay sesión (cookie): devolver `{ "userId": "guid", "role": "admin" }`.
- Si no hay sesión pero sí `Authorization: Bearer <token>` y el token es válido y no expirado: devolver lo mismo.
- Si no: 401.

Usado por el front para saber si está logueado y qué rol tiene (y así mostrar u ocultar pestañas).

---

## 8. Logout

- **POST /api/auth/logout:** Limpiar sesión y, si la petición trae Bearer, borrar ese token del store. Response 204.

---

## 9. Creación de usuarios (Configuración)

- Solo roles admin/manager/master pueden crear usuarios.
- Campos: FullName, Email, PIN (obligatorio, mínimo 4 caracteres), Role (user, admin, manager).
- Guardar: Email único, PasswordHash (puede ser un valor aleatorio si solo se usa PIN), PinHash = BCrypt.Hash(PIN), Role, IsActive = true.

---

## 10. Referencia al código actual

- **AuthController.cs:** LoginByPin (BCrypt.Verify, Session.SetString, TokenStore), Me (Session o Bearer), Logout.
- **ManagerOS.Windows:** ConfiguracionService.CreateUserWithPinAsync, CreateMasterUserAsync; AuthService (login app escritorio).
- **wwwroot/feedback/app.js:** Llamada a POST /api/auth/pin, guardado de token, uso de Bearer en fetch, showAppWithRole(role), comprobación isAdminRole.
