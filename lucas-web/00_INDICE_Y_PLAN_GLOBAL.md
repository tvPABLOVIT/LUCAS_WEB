# Lucas Web — Índice y plan global

**Versión:** 1.0  
**Fecha:** Febrero 2026  
**Objetivo:** Plan maestro para llevar Lucas (Manager OS) a una versión web completa, con guías de inventario, especificaciones y despliegue.

---

## Índice de guías

| # | Archivo | Contenido principal |
|---|---------|---------------------|
| 01 | [01_ARQUITECTURA_Y_TECNOLOGIAS.md](01_ARQUITECTURA_Y_TECNOLOGIAS.md) | Arquitectura actual vs objetivo, stack, reutilización Backend |
| 02 | [02_INVENTARIO_FUNCIONALIDADES_LUCAS.md](02_INVENTARIO_FUNCIONALIDADES_LUCAS.md) | Funcionalidades, roles, prioridades web |
| 03 | [03_INVENTARIO_APIS_BACKEND.md](03_INVENTARIO_APIS_BACKEND.md) | Endpoints, DTOs, ejemplos JSON, autenticación |
| 04 | [04_INVENTARIO_ENTIDADES_Y_BD.md](04_INVENTARIO_ENTIDADES_Y_BD.md) | Esquema BD, entidades, rutas, migraciones |
| 05 | [05_INVENTARIO_SERVICIOS_Y_LOGICAS.md](05_INVENTARIO_SERVICIOS_Y_LOGICAS.md) | Servicios (Registro, Inteligencia, etc.) y dónde exponerlos en web |
| 06 | [06_ESPECIFICACION_PANTALLA_REGISTRO.md](06_ESPECIFICACION_PANTALLA_REGISTRO.md) | Registro de ejecución: campos, validaciones, APIs |
| 07 | [07_ESPECIFICACION_PANTALLA_ESTIMACIONES.md](07_ESPECIFICACION_PANTALLA_ESTIMACIONES.md) | Estimaciones: KPIs, predicción, alertas, APIs |
| 08 | [08_ESPECIFICACION_PANTALLA_CONFIGURACION.md](08_ESPECIFICACION_PANTALLA_CONFIGURACION.md) | Configuración: parámetros, ubicación, integraciones |
| 09 | [09_ESPECIFICACION_LOGIN_Y_ROLES.md](09_ESPECIFICACION_LOGIN_Y_ROLES.md) | Login por PIN, roles, permisos, flujo |
| 10 | [10_FLUJOS_CRUZADOS_Y_SINCRONIZACION.md](10_FLUJOS_CRUZADOS_Y_SINCRONIZACION.md) | Flujos entre módulos, sincronización tablet/web |
| 11 | [11_MIGRACION_DATOS_Y_DESPLIEGUE.md](11_MIGRACION_DATOS_Y_DESPLIEGUE.md) | Migración de datos, despliegue Backend y Frontend |

---

## Plan de fases

### Fase 1 — Base
1. Reutilizar Backend existente (API ya disponible).
2. Crear frontend web (React/Vue/HTML+JS) con rutas: Login, Registro, Estimaciones, Configuración.
3. Implementar auth por PIN y roles.

### Fase 2 — Registro y Estimaciones
4. Pantalla Registro: cargar día, turnos, feedback, guardar vía `/api/execution`.
5. Pantalla Estimaciones: usar `/api/estimaciones` (cache) y/o `/api/predictions/next-week`, `/api/recommendations`.

### Fase 3 — Configuración
6. Pantalla Configuración: parámetros básicos, ubicación, integraciones.
7. Exponer lectura/escritura de `settings.json` si se necesita vía API nueva (o mantener en app Windows).

### Fase 4 — Sincronización y despliegue
8. Flujos cruzados: tablet ↔ web, refresco por `/api/recommendations/version`.
9. Migración de datos (BD en servidor si Modo B), despliegue Backend y Frontend.

---

## Dependencias entre guías

```
00_INDICE_Y_PLAN_GLOBAL
       │
       ├──► 01_ARQUITECTURA_Y_TECNOLOGIAS
       ├──► 02_INVENTARIO_FUNCIONALIDADES_LUCAS
       │
       ├──► 03_INVENTARIO_APIS_BACKEND  ────► 06, 07, 08, 09
       ├──► 04_INVENTARIO_ENTIDADES_Y_BD
       ├──► 05_INVENTARIO_SERVICIOS_Y_LOGICAS
       │
       ├──► 06_ESPECIFICACION_PANTALLA_REGISTRO
       ├──► 07_ESPECIFICACION_PANTALLA_ESTIMACIONES
       ├──► 08_ESPECIFICACION_PANTALLA_CONFIGURACION
       ├──► 09_ESPECIFICACION_LOGIN_Y_ROLES
       │
       ├──► 10_FLUJOS_CRUZADOS_Y_SINCRONIZACION
       └──► 11_MIGRACION_DATOS_Y_DESPLIEGUE
```

---

## Checklist de uso

- [ ] Leer 01 y 02 para entender arquitectura y funcionalidades.
- [ ] Consultar 03 y 04 al implementar llamadas a API y modelo de datos.
- [ ] Usar 06, 07, 08, 09 como especificaciones de cada pantalla web.
- [ ] Seguir 10 para flujos cruzados (tablet/web) y 11 para despliegue.

## Estado del proyecto

Ver [ESTADO_PROYECTO.md](ESTADO_PROYECTO.md) para el estado de implementación y entregables.
