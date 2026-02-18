# Auditoría del programa Lucas Web / BETLEM y propuestas de mejora

**Fecha:** 12/02/2026  
**Alcance:** Código backend (LucasWeb.Api), frontend (wwwroot), documentación y estructura del proyecto.  
**Criterio:** Buenas prácticas de programación sin romper la funcionalidad ni el aspecto actual.

---

## 1. Resumen ejecutivo

El programa funciona correctamente y cumple con su propósito. La auditoría identifica **puntos fuertes** (arquitectura clara, validación en APIs críticas, autenticación por token, uso de interfaces en servicios) y **propuestas de mejora** priorizadas y no invasivas, para aplicar de forma gradual sin cambiar el comportamiento visible.

**Backup realizado:** `e:\BETLEM_backup_20260212` (201 archivos, excluyendo bin/obj/.vs).

---

## 2. Estructura del proyecto (positivo)

- **Backend:** Organización por carpetas (Controllers, Services, Models, DTOs, Data, Middleware). Uso de interfaces (IAuthService, IWeatherService, etc.) y inyección de dependencias.
- **Frontend:** JS modular por vistas (dashboard, preguntas, configuracion, etc.), uso de `global.LUCAS_*` para compartir estado y auth.
- **Documentación:** Carpeta `documentacion/` con índices, arquitectura, APIs y flujos bien descritos.
- **Configuración:** Opciones en `LucasOptions`, connection string y secrets en appsettings (evitar subir claves a repositorio).

---

## 3. Seguridad

### 3.1 Lo que está bien

- Autenticación por token Bearer; PIN hasheado con BCrypt.
- Tokens con expiración configurable.
- Validación de entrada en ExecutionController (fechas, turnos, opciones permitidas).
- Settings con lista blanca de claves (`AllowedKeys`); no se aceptan claves arbitrarias.
- Roles diferenciados (admin, manager, master, user) y uso de `[Authorize(Roles = "...")]` en endpoints sensibles (Database, Users, Events, Weather por método).

### 3.2 Propuestas de mejora (no invasivas)

| Prioridad | Propuesta | Motivo | Cómo aplicar sin romper |
|-----------|-----------|--------|---------------------------|
| Alta | **Restringir PATCH /api/settings por rol** | Cualquier usuario autenticado (incl. "user") puede modificar configuración vía API. | Añadir `[Authorize(Roles = "admin,manager,master")]` al método `Patch` de SettingsController. El frontend ya no muestra Configuración a "user", así que no cambia la UX. |
| Media | **CORS más restrictivo en producción** | `AllowAnyOrigin()` acepta cualquier origen. | En producción, configurar CORS con `WithOrigins("https://tu-dominio.com")` (y variantes) y usar `AllowCredentials()` si se envían cookies. Mantener la política actual en desarrollo. |
| Media | **No dejar DefaultPin "1234" en appsettings por defecto** | Si se despliega sin sobreescribir, el PIN queda predecible. | En `appsettings.json` no incluir `DefaultPin` o usar valor vacío; obligar a definirlo en `appsettings.Production.json` o variable de entorno. El DataSeeder ya usa `options.DefaultPin`; si está vacío, no crear usuario hasta primera configuración o usar valor por defecto solo en desarrollo. |
| Baja | **Limpieza periódica de tokens expirados** | Los AuthTokens expirados se acumulan en la BD. | Añadir un HostedService o tarea programada que borre `AuthTokens` con `ExpiresAt < DateTime.UtcNow` (por ejemplo una vez al día). No afecta al comportamiento actual. |
| Baja | **Rate limiting en login** | Reduce fuerza bruta sobre el PIN. | Añadir middleware o filtro que limite intentos por IP en `/api/auth/pin` (ej. 5 intentos por minuto). Opcional y separable. |

---

## 4. Código y patrones

### 4.1 Lo que está bien

- Uso consistente de async/await y `Task` en la API.
- Validación de DTOs en controllers (fechas, rangos, opciones permitidas).
- Middleware de excepciones para devolver JSON en errores de `/api/*`.
- Servicios externos (clima, geocoding) abstraídos detrás de interfaces.

### 4.2 Propuestas de mejora (no invasivas)

| Prioridad | Propuesta | Motivo | Cómo aplicar sin romper |
|-----------|-----------|--------|---------------------------|
| Media | **Registrar servicios con interfaz donde falte** | Algunos servicios se registran solo por tipo concreto (ej. `NextWeekPredictionService`). | Donde tenga sentido, definir interfaz (ej. `INextWeekPredictionService`) y registrar `AddScoped<INextWeekPredictionService, NextWeekPredictionService>()`. Facilita tests y sustitución. Aplicar solo donde el cambio sea trivial. |
| Media | **Evitar `catch { }` sin ningún registro** | En WeatherService, EstimacionesController, etc., un `catch` vacío oculta fallos (red, JSON, etc.). | Sustituir por `catch (Exception ex) { _logger.LogWarning(ex, "…"); return Array.Empty<…>(); }` (o equivalente) en servicios que llaman a APIs externas. Mantener el mismo valor de retorno/comportamiento para no cambiar la lógica. |
| Baja | **Constantes para cadenas repetidas** | Roles como "admin", "manager", "master", "user" aparecen en varios sitios. | Definir una clase estática `Roles` con constantes (Admin, Manager, Master, User) y usarla en `[Authorize(Roles = Roles.Admin + "," + Roles.Manager + ...)]` y en frontend. Reduce errores de escritura. |
| Baja | **Validación de entrada con DataAnnotations o FluentValidation** | Parte de la validación está en el controller (if + BadRequest). | Opcional: usar `[Required]`, `[Range]`, etc. en DTOs o FluentValidation. Solo donde simplifique y no cambie el comportamiento actual. |

---

## 5. Base de datos

### 5.1 Lo que está bien

- Uso de EF Core con SQLite; índices en fechas y claves únicas (ExecutionDay.Date, ShiftFeedback (ExecutionDayId, ShiftName), AuthToken.Token).
- Migraciones implícitas / evolución con DataSeeder (ALTER TABLE con try/catch para columnas nuevas).

### 5.2 Propuestas de mejora (no invasivas)

| Prioridad | Propuesta | Motivo | Cómo aplicar sin romper |
|-----------|-----------|--------|---------------------------|
| Media | **Backups programados de manageros.db** | Pérdida del archivo implica pérdida de datos. | Script o tarea que copie `manageros.db` a una carpeta de backup (p. ej. diario) con rotación (últimos N días). No tocar el código de la aplicación. |
| Baja | **Migraciones EF Core explícitas** | A largo plazo, muchos ALTER en DataSeeder son frágiles. | Cuando se estabilice el modelo, considerar migraciones EF (`Add-Migration`, `Update-Database`) y dejar el seeder solo para datos iniciales. Opcional y gradual. |

---

## 6. Frontend

### 6.1 Lo que está bien

- Versionado de scripts (`?v=8`) para evitar caché antigua.
- Auth centralizado (LUCAS_AUTH) y uso de `fetchWithAuth` con manejo de 401.
- Rutas por hash y vistas separadas por rol (user solo ve Feedback diario).

### 6.2 Propuestas de mejora (no invasivas)

| Prioridad | Propuesta | Motivo | Cómo aplicar sin romper |
|-----------|-----------|--------|---------------------------|
| Baja | **Minificación de JS/CSS en publicación** | Menor tamaño y carga en producción. | Usar un paso de build (npm script, dotnet bundle, o herramienta externa) que genere .min.js/.min.css y que index.html apunte a ellos en release. Mantener los .js/.css actuales para desarrollo. |
| Baja | **Content-Security-Policy (CSP) en cabeceras** | Reduce riesgo de XSS. | Añadir cabecera CSP restrictiva (bloquear inline scripts si no se usan, permitir solo origen propio y APIs conocidas). Probar en staging para no romper nada. |
| Baja | **Evitar datos sensibles en sessionStorage** | El token en sessionStorage es aceptable; no guardar PIN ni datos críticos. | Revisar que no se persista el PIN ni información sensible más allá del token/role/userId. Ya se ve un uso razonable. |

---

## 7. Logging y observabilidad

### 7.1 Propuestas (no invasivas)

| Prioridad | Propuesta | Motivo |
|-----------|-----------|--------|
| Media | **Log estructurado en errores de API** | Incluir en el log (además del mensaje) request path, método, usuario si existe, y excepción. Ayuda en producción. |
| Baja | **Niveles de log por entorno** | En producción usar `Warning` por defecto y `Information` solo en puntos clave; en desarrollo mantener más detalle. |

---

## 8. Tests

- **Estado actual:** No se han detectado proyectos de tests (xUnit, NUnit, etc.).
- **Propuesta (baja prioridad, gradual):** Añadir un proyecto de tests y cubrir primero:
  - Servicios de dominio puro (por ejemplo FeedbackScoring, cálculos de predicción).
  - Validación de DTOs (ExecutionController, SettingsController).
  - No tocar flujos E2E ni UI hasta que el equipo esté cómodo con los tests.

---

## 9. Resumen de prioridades

- **Alta:** Restricción de PATCH settings por rol (admin/manager/master).
- **Media:** CORS en producción, DefaultPin no por defecto en producción, logging en catches de servicios externos, backup programado de la BD, registro por interfaz donde sea sencillo.
- **Baja:** Limpieza de tokens expirados, rate limiting en login, constantes de roles, validación con anotaciones, migraciones EF, minificación frontend, CSP, tests unitarios graduales.

Ninguna propuesta debe cambiar el flujo ni el diseño actual de la aplicación; son mejoras de seguridad, mantenibilidad y operación que se pueden aplicar de forma incremental.
