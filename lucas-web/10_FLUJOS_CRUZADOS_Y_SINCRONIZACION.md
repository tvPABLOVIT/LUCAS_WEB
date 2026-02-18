# Lucas Web — Flujos cruzados y sincronización

**Versión:** 1.0  
**Fecha:** Febrero 2026

---

## Flujos entre módulos

### Tablet → Backend → App Windows
1. Usuario en tablet introduce feedback de turnos.
2. POST/PATCH `/api/execution/{date}` escribe en SQLite (misma BD que app Windows).
3. La app Windows, en el siguiente ciclo de análisis (5 min) o al abrir Registro/Estimaciones, ve los datos actualizados porque comparten BD.

### App Windows → Backend → Web
1. Usuario en app Windows guarda día o importa Excel.
2. RegistroService escribe en SQLite; opcionalmente SyncToGoogleSheet.
3. La app lanza RunFullBackgroundAnalysisAsync (patrones, tendencias, predicción, recomendaciones).
4. RecomendacionesViewModel publica cache a POST `/api/estimaciones/cache` al cargar Estimaciones.
5. La web consulta GET `/api/estimaciones`, `/api/dashboard/week`, `/api/predictions/next-week` y ve los datos actualizados.

### Web → Backend → App Windows
1. Usuario en web guarda día (POST/PATCH `/api/execution`).
2. Backend escribe en SQLite; opcionalmente SyncToGoogleSheet.
3. La app Windows, al siguiente ciclo de análisis o al abrir Registro, ve los datos.
4. Si la app Windows está abierta en Estimaciones, el cache se refresca al volver a cargar; si no, la web puede usar directamente `/api/dashboard/week` y `/api/predictions/next-week`.

---

## Sincronización tablet/web

### Versión de datos
- GET `/api/recommendations/version` devuelve el timestamp más reciente entre ExecutionDays.UpdatedAt, Recommendations.CreatedAt, WeeklyPredictions.CreatedAt.
- La web y la tablet pueden consultar esta ruta cada 60 s; si el valor cambia, recargar datos (estimaciones, predicción, recomendaciones).

### Tabla resumen

| Origen del cambio | Quién ve el cambio | Cuándo |
|-------------------|--------------------|--------|
| Tablet guarda feedback | App Windows, Web | Inmediato (misma BD) |
| App Windows guarda día | Tablet, Web | Inmediato |
| App Windows ejecuta análisis | Web (estimaciones) | Cuando se publique cache o se consulten APIs de predicción/recomendaciones |
| Web guarda día | App Windows, Tablet | Inmediato |

### Nota
- La predicción y recomendaciones se generan solo en la app Windows (InteligenciaService.RunFullBackgroundAnalysisAsync). Si la web se usa sin app Windows abierta, la predicción y alertas pueden estar desactualizadas o vacías; para Modo B (servidor) habría que implementar el análisis en el Backend.
