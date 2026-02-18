# Lucas Web — Estado del proyecto

**Fecha:** Febrero 2026

---

## Fases completadas

| Fase | Descripción | Estado |
|------|-------------|--------|
| **1. Base** | Backend ASP.NET Core, frontend HTML/JS, auth por PIN y roles | ✅ Completado |
| **2. Registro y Estimaciones** | Pantallas Registro (cargar/crear/editar día y turnos) y Estimaciones (KPIs, predicción, recomendaciones) | ✅ Completado |
| **3. Configuración** | API GET/PATCH `/api/settings`, pantalla Configuración con parámetros editables | ✅ Completado |
| **4. Sincronización y despliegue** | Versión de datos desde BD (`/api/recommendations/version`), refresco automático en Estimaciones, documentación DEPLOY, Dockerfile, health | ✅ Completado |

---

## Entregables

- **Backend (LucasWeb.Api):** API completa (auth, execution, dashboard, estimaciones, predictions, recommendations, settings, health), SQLite, token Bearer, CORS, wwwroot con frontend.
- **Frontend (wwwroot y lucas-web-app):** Login, Registro (3 turnos, Q1–Q4 con textos exactos del doc 03), **Preguntas** (feedback tablet: fecha, turno activo por hora, Q1–Q4 en radio, “Guardar Lucas”), Estimaciones (selector de semana ◀ ▶, refresco por versión), Configuración (Productividad ideal, Horas por turno, **Coste personal por hora**, restaurante, Lat/Lon). Rol *user*: solo pantalla Preguntas; admin/manager/master: Registro, Estimaciones, Preguntas, Configuración. Redirección a login en 401.
- **Backend:** Validación de Q1–Q4 contra listas permitidas (doc 03) en POST/PATCH execution; setting `CostePersonalPorHora` en Configuración.
- **Documentación:** README raíz, DEPLOY.md (Modo A/B, túnel, Docker), .gitignore, lucas-web/ especificaciones.

---

## Cómo cerrar el proyecto

1. Revisar y ajustar `appsettings.json` / variables de entorno en producción.
2. Definir política CORS en producción (restringir orígenes si el frontend está en otro dominio).
3. Opcional: migrar a PostgreSQL u otro motor en Modo B; ver `11_MIGRACION_DATOS_Y_DESPLIEGUE.md`.
4. Opcional: integrar con app Windows (misma BD, POST `/api/estimaciones/cache` desde la app).
