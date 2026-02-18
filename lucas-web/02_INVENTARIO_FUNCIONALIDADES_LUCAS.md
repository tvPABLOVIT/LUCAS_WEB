# Lucas Web — Inventario de funcionalidades

**Versión:** 1.0  
**Fecha:** Febrero 2026

---

## Funcionalidades por pantalla

| Pantalla | Descripción | Fuente de datos | Prioridad Web |
|----------|-------------|-----------------|---------------|
| **Login** | Entrada por PIN, roles | Users (BD), AuthController | Alta |
| **Registro** | Día de ejecución, turnos (Mediodía, Tarde, Noche), feedback Q1–Q4, facturación, horas, personal sala/cocina | ExecutionController, ExecutionDay, ShiftFeedback | Alta |
| **Estimaciones** | KPIs semanales, predicción semana siguiente, 7 días con min/max, alertas | DashboardController, EstimacionesController, PredictionsController, RecommendationsController | Alta |
| **Configuración** | Parámetros (productividad ideal, horas por turno), ubicación (lat/lon), integraciones (Google Sheets, túnel) | settings.json, ConfiguracionService (app Windows) | Media |

---

## Roles y permisos

| Rol | Acceso desktop | Acceso web/tablet |
|-----|----------------|-------------------|
| **admin** | Completo (Registro, Estimaciones, Configuración) | Completo web (si se implementa) |
| **manager** | Completo | Completo web |
| **master** | Completo | Completo web |
| **user** | No (solo feedback por tablet) | Solo feedback (registro de turnos) |

---

## Prioridades para la versión web

### Prioridad 1 (esencial)
- Login por PIN (ya existe en `/api/auth/pin`)
- Registro: ver/editar día y turnos, guardar vía POST/PATCH `/api/execution`
- Estimaciones: ver predicción semana siguiente y alertas (GET `/api/estimaciones` o `/api/predictions/next-week`)

### Prioridad 2 (importante)
- Configuración básica: parámetros de predicción y ubicación (requiere API nueva si se expone por web)
- Sincronización: refresco cuando cambian datos (GET `/api/recommendations/version`)

### Prioridad 3 (opcional)
- Configuración avanzada: Google Sheets, túnel Cloudflare (habitualmente se gestiona desde la app Windows)
