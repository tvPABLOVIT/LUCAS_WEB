# Documentación — Lucas Web desde cero

**Versión:** 1.0  
**Fecha:** Febrero 2026  
**Objetivo:** Documentación extremadamente detallada para llevar adelante el proyecto Lucas (Manager OS) como **aplicación web** desde cero, sin depender del código actual.

---

## Índice de documentos

| # | Archivo | Contenido principal |
|---|---------|---------------------|
| 00 | **00_INDICE_Y_VISION.md** (este) | Índice, visión del proyecto y plan de fases |
| 01 | [01_ARQUITECTURA_Y_TECNOLOGIAS.md](01_ARQUITECTURA_Y_TECNOLOGIAS.md) | Arquitectura general, stack recomendado, hosting, seguridad |
| 02 | [02_MODELO_DE_DATOS.md](02_MODELO_DE_DATOS.md) | Entidades, tablas, campos, tipos, índices, relaciones, migraciones |
| 03 | [03_REGLAS_DE_NEGOCIO_Y_FORMULAS.md](03_REGLAS_DE_NEGOCIO_Y_FORMULAS.md) | Cálculos, scoring V/R/M/D, KPIs, predicciones, bandas de confianza |
| 04 | [04_APIS_Y_ENDPOINTS.md](04_APIS_Y_ENDPOINTS.md) | Todos los endpoints, request/response, DTOs, autenticación |
| 05 | [05_AUTENTICACION_Y_ROLES.md](05_AUTENTICACION_Y_ROLES.md) | Login por PIN, sesión, cookies, tokens, roles y permisos |
| 06 | [06_FLUJOS_DE_USUARIO.md](06_FLUJOS_DE_USUARIO.md) | Flujos: login, registro de ejecución, estimaciones, configuración, feedback tablet |
| 07 | [07_ESTIMACIONES_Y_PREDICCIONES.md](07_ESTIMACIONES_Y_PREDICCIONES.md) | Lógica de predicción semanal, factores, clima, festivos, eventos |
| 08 | [08_INTEGRACIONES_EXTERNAS.md](08_INTEGRACIONES_EXTERNAS.md) | Clima (Open-Meteo), festivos (Nager), Open Data BCN, GuiaBCN, Google Sheets |
| 09 | [09_ESPECIFICACION_PANTALLAS_WEB.md](09_ESPECIFICACION_PANTALLAS_WEB.md) | Especificación pantalla a pantalla para la app web |
| 10 | [10_DESPLIEGUE_Y_CONFIGURACION.md](10_DESPLIEGUE_Y_CONFIGURACION.md) | Despliegue, variables de entorno, settings, BD, backup |
| 11 | [11_GLOSARIO_Y_REFERENCIAS.md](11_GLOSARIO_Y_REFERENCIAS.md) | Glosario, acrónimos, referencias a código actual |

---

## Visión del proyecto

**Lucas** es un sistema de inteligencia operativa para restaurantes. La versión actual incluye:

- **App de escritorio (Windows WPF):** Registro de ejecución, Dashboard, Estimaciones, Configuración, Patrones, Recomendaciones.
- **Backend API (ASP.NET Core):** Sirve la misma base de datos SQLite y expone endpoints para la **vista tablet** (feedback por PIN) y para una futura app web.
- **Vista tablet:** Página web (HTML/JS/CSS) bajo `/feedback`: login por PIN, preguntas de feedback por turno (Mediodía, Tarde, Noche), y para administradores también Registro y Estimaciones.

El objetivo de esta documentación es permitir **construir una aplicación web completa** (Lucas Web) que replique y extienda la funcionalidad actual, enfocada en:

1. **Hosting web estándar** (PHP + MySQL o ASP.NET Core + MySQL/SQL Server) a coste 0 €/mes en hosting compartido si se desea.
2. **Misma lógica de negocio:** registro diario, turnos, feedback Q1–Q4, dashboard semanal, predicciones, recomendaciones, integraciones (clima, festivos, eventos).
3. **Roles y permisos:** usuario (solo feedback/preguntas), admin/manager/master (registro, estimaciones, configuración).
4. **Vista tablet:** opcionalmente la misma URL puede servir la interfaz de “solo preguntas” para tablets en sala.

---

## Plan de fases recomendado

### Fase 1 — Base
1. Definir stack (ej. PHP + MySQL o ASP.NET Core + MySQL).
2. Crear proyecto, BD (esquema según 02_MODELO_DE_DATOS).
3. Implementar autenticación por PIN y roles (05, 04).
4. Pantalla de login web y redirección por rol.

### Fase 2 — Registro y Dashboard
5. Pantalla Registro de ejecución: día, facturación, horas, turnos, feedback Q1–Q4 (06, 04, 03).
6. Pantalla Dashboard: resumen semanal, KPIs, comparativas (04, 03).

### Fase 3 — Estimaciones y predicciones
7. Lógica de predicción de la semana siguiente (07, 03).
8. Pantalla Estimaciones: KPIs históricos, predicción, días, alertas (tendencia, clima, festivos, misma semana mes anterior, eventos/obras) (07, 08).

### Fase 4 — Configuración e integraciones
9. Pantalla Configuración: parámetros, ubicación, integraciones (08, 10).
10. Integrar clima, festivos, Open Data BCN (y opcionalmente GuiaBCN, Google Sheets) (08).

### Fase 5 — Vista tablet y despliegue
11. Vista “solo preguntas” para tablet (misma app con rol user o ruta dedicada).
12. Despliegue en hosting, backup, documentación de operación (10).

---

## Cómo usar esta documentación

- **Empezar desde cero:** Leer en orden 00 → 01 → 02 → 03. Luego 04 y 05 para implementar auth y APIs. Después 06, 07, 09 para pantallas y 08, 10 para integraciones y despliegue.
- **Implementar una pantalla concreta:** 02 (modelo), 03 (reglas), 04 (APIs), 09 (especificación de esa pantalla).
- **Implementar integraciones:** 08 (detalle de cada servicio externo) y 07 (cómo se usan en estimaciones).
- **Desplegar:** 10 (despliegue y configuración) y 05 (cookies/sesión en producción).

---

## Referencia al código actual

La documentación se ha extraído del proyecto actual:

- **ManagerOS.Core:** entidades (`ExecutionDay`, `ShiftFeedback`, `User`, etc.).
- **ManagerOS.Infrastructure:** `ApplicationDbContext`, migraciones EF Core, integraciones (Google Sheets).
- **ManagerOS.Backend:** controladores API (`Auth`, `Execution`, `Dashboard`, `Recommendations`, `Estimaciones`), `Program.cs`, reescritura `/feedback/api` → `/api`.
- **ManagerOS.Windows:** servicios (`RegistroService`, `InteligenciaService`, `ConfiguracionService`, `ClimaService`, `FestivosService`, `OpenDataBcnService`, `GuiaBcnScraperService`, `TurnoScoringService`), ViewModels, vistas WPF.
- **wwwroot/feedback:** `index.html`, `app.js`, `styles.css` (vista tablet).

Los nombres de archivos y clases se citan en los documentos cuando ayuda a localizar la lógica exacta.
