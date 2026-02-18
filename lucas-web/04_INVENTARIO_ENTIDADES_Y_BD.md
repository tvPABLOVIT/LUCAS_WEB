# Lucas Web — Inventario de entidades y BD

**Versión:** 1.0  
**Fecha:** Febrero 2026

---

## Ubicación

- **BD:** `%LocalAppData%\ManagerOS\manageros.db` (SQLite)
- **ConnectionString:** incluye `Cache=Shared` en Backend para acceso concurrente con app Windows

---

## Entidades principales

| Entidad | Propósito |
|---------|-----------|
| **ExecutionDay** | Día de operación: Id, Date (único), TotalRevenue, TotalHoursWorked, StaffTotal, Notes, WeatherCode, WeatherTemp, IsHoliday, CreatedAt, UpdatedAt. Colección ShiftFeedbacks |
| **ShiftFeedback** | Turno: Id, ExecutionDayId, ShiftName (Mediodia|Tarde|Noche), Revenue, HoursWorked, StaffFloor, StaffKitchen, FeedbackQ1–Q4, RecordedBy, EditedBy, CreatedAt. Índice único (ExecutionDayId, ShiftName) |
| **User** | Id, FullName, Email, PasswordHash, Role, PinHash, IsActive, CreatedAt, UpdatedAt |
| **DailyAnalysis** | Análisis de un día (productividad, resumen, comparaciones) |
| **DetectedPattern** | Patrones (Estacional, Operativo: clima, festivos, temperatura) |
| **DetectedTrend** | Tendencias (up/down/stable, últimas 4 vs 4 anteriores) |
| **Recommendation** | Recomendaciones (pending, accepted, applied, discarded, expired) |
| **WeeklyPrediction** | Predicción semanal: WeekStartMonday, PredictedRevenue, DailyPredictionsJson, HistoricalStatsJson, AccuracyMetricsJson |
| **Event** | Eventos (festivos, cambios de carta, etc.) |

---

## Rutas de datos

- **App Windows:** `AppDataService.ConnectionString` (sin Cache=Shared por defecto)
- **Backend:** `ApplicationDbContext` con ConnectionString incluyendo `Cache=Shared`
- **settings.json:** `%LocalAppData%\ManagerOS\settings.json` (parámetros, integraciones)

---

## Migraciones

- Ensamblado: `ManagerOS.Infrastructure`
- Se aplican en arranque del Backend y de la app Windows (`db.Database.Migrate()`)
- Nombres típicos: InitialCreate, Fase6_PatronesTendenciasPrediccion, CompletarModeloDatos, etc.
