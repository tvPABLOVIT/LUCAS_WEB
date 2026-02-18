# 02 — Modelo de datos

Esquema completo de la base de datos para implementar Lucas Web desde cero. Equivalente a las entidades y migraciones del proyecto actual (ManagerOS.Core + ManagerOS.Infrastructure).

---

## 1. Resumen de tablas

| Tabla | Descripción |
|-------|-------------|
| **ExecutionDays** | Un registro por día de operación: fecha, facturación total, horas trabajadas, personal total, notas, clima, festivo. |
| **ShiftFeedbacks** | Tres filas por día (Mediodía, Tarde, Noche): facturación y horas del turno, personal sala/cocina, feedback Q1–Q4. |
| **Users** | Usuarios: nombre, email, hash contraseña, rol, PinHash (BCrypt), activo. |
| **DailyAnalyses** | Análisis calculado por día: productividad, comparativas, resumen (opcional; se puede recalcular en backend). |
| **DetectedPatterns** | Patrones detectados (operativo, estacional, etc.) para recomendaciones. |
| **DetectedTrends** | Tendencias (facturación/productividad al alza/baja). |
| **Recommendations** | Recomendaciones generadas: tipo, título, descripción, prioridad, estado, fechas. |
| **WeeklyPredictions** | Predicción por semana (lunes a domingo): facturación total, JSON por día/turno, métricas. |
| **Events** | Eventos externos (festivos, cambios de carta, etc.) por fecha. |

---

## 2. Tabla ExecutionDays

Representa un **día de ejecución** (una jornada del restaurante).

| Columna | Tipo | Nullable | Descripción |
|---------|------|----------|-------------|
| Id | GUID (UUID) | NO | PK. |
| Date | DATE / DATETIME | NO | Fecha del día. **Índice único.** |
| TotalRevenue | DECIMAL(18,2) | NO | Facturación total del día (€). |
| TotalHoursWorked | DECIMAL(18,2) | NO | Horas trabajadas totales. |
| StaffTotal | INT | NO | Suma de personal (sala + cocina) del día. |
| Notes | TEXT / VARCHAR | SÍ | Notas libres. |
| WeatherCode | VARCHAR | SÍ | Código clima (ej. WMO). |
| WeatherTemp | DECIMAL(5,2) | SÍ | Temperatura registrada. |
| IsHoliday | BOOLEAN / TINYINT | NO | Si el día es festivo. Default 0. |
| CreatedAt | DATETIME / TIMESTAMP | NO | Alta. |
| UpdatedAt | DATETIME / TIMESTAMP | NO | Última actualización. |

**Índices:** PK en `Id`, UNIQUE en `Date`.

**Relación:** 1 ExecutionDay → N ShiftFeedbacks (eliminación en cascada).

---

## 3. Tabla ShiftFeedbacks

Un **turno** dentro de un día. Siempre hay hasta tres: Mediodía, Tarde, Noche.

| Columna | Tipo | Nullable | Descripción |
|---------|------|----------|-------------|
| Id | GUID | NO | PK. |
| ExecutionDayId | GUID | NO | FK → ExecutionDays.Id (ON DELETE CASCADE). |
| ShiftName | VARCHAR(50) | NO | "Mediodia", "Tarde", "Noche". |
| Revenue | DECIMAL(18,2) | NO | Facturación del turno (€). Puede ser 0 si solo se rellenan preguntas. |
| HoursWorked | DECIMAL(18,2) | NO | Horas del turno. |
| StaffFloor | INT | NO | Personal sala. |
| StaffKitchen | INT | NO | Personal cocina. |
| FeedbackQ1 | VARCHAR | SÍ | Ver lista de valores en 03_REGLAS_DE_NEGOCIO. Volumen. |
| FeedbackQ2 | VARCHAR | SÍ | Ritmo. |
| FeedbackQ3 | VARCHAR | SÍ | Margen. |
| FeedbackQ4 | VARCHAR | SÍ | Dificultad. |
| RecordedBy | VARCHAR | SÍ | Quién registró (nombre o id). |
| EditedBy | VARCHAR | SÍ | Quién editó. |
| CreatedAt | DATETIME | NO | Alta. |

**Índices:** PK en `Id`, UNIQUE en `(ExecutionDayId, ShiftName)`.

**Valores permitidos para FeedbackQ1–Q4:** Ver documento 03 (listas exactas de opciones).

---

## 4. Tabla Users

| Columna | Tipo | Nullable | Descripción |
|---------|------|----------|-------------|
| Id | GUID | NO | PK. |
| FullName | VARCHAR(200) | NO | Nombre completo. |
| Email | VARCHAR(256) | NO | **Índice único.** |
| PasswordHash | VARCHAR(500) | NO | Hash BCrypt de la contraseña. |
| Role | VARCHAR(50) | NO | "admin", "manager", "user" (o "master"). Default "user". |
| PinHash | VARCHAR(500) | SÍ | Hash BCrypt del PIN (4+ dígitos). Si NULL/vacío, no puede entrar por PIN. |
| IsActive | BOOLEAN | NO | Default true. |
| CreatedAt | DATETIME | NO | |
| UpdatedAt | DATETIME | NO | |

**Índices:** PK en `Id`, UNIQUE en `Email`.

---

## 5. Tabla DailyAnalyses

Análisis calculado por día (productividad, comparativas). Se puede rellenar al guardar un día o con un job.

| Columna | Tipo | Nullable | Descripción |
|---------|------|----------|-------------|
| Id | GUID | NO | PK. |
| AnalysisDate | DATE | NO | **Índice único.** |
| ProductivityEurPerHour | DECIMAL(18,2) | NO | TotalRevenue/TotalHoursWorked. |
| TotalRevenue | DECIMAL(18,2) | NO | Copia o referencia. |
| TotalHours | DECIMAL(18,2) | NO | |
| TotalStaff | INT | NO | |
| DayOfWeek | INT | NO | 0=Sunday, 1=Monday… o 1=Monday, 7=Sunday según convención. |
| IsHoliday | BOOLEAN | NO | |
| WeatherCode | VARCHAR | SÍ | |
| WeatherTemp | DECIMAL(5,2) | SÍ | |
| ComparisonsJson | TEXT | SÍ | JSON comparativas. |
| Summary | TEXT | SÍ | Resumen en texto. |
| DeviationExplanationsJson | TEXT | SÍ | JSON explicaciones. |
| CreatedAt | DATETIME | NO | |

---

## 6. Tablas DetectedPatterns, DetectedTrends, Recommendations

- **DetectedPatterns:** Id, Type (Operativo, Estacional, etc.), Name, Description, Confidence, Severity, Payload (JSON), ValidFrom, ValidTo, DetectedAt, IsActive.
- **DetectedTrends:** Id, Metric, Direction (up/down/stable), StrengthPercent, Slope, Payload (JSON), FromDate, ToDate, DetectedAt.
- **Recommendations:** Id, Type, Title, Description, Priority (1–5), Status (pending, accepted, applied, discarded, expired), RelatedDate, SourcePatternId, SourceTrendId, Outcome (JSON), EffectivenessScore, AppliedAt, EvaluatedAt, CreatedAt.

Usadas por la lógica de “inteligencia” (patrones, tendencias, recomendaciones). Ver 07 y 03.

---

## 7. Tabla WeeklyPredictions

Una fila por semana (lunes como clave).

| Columna | Tipo | Nullable | Descripción |
|---------|------|----------|-------------|
| Id | GUID | NO | PK. |
| WeekStartMonday | DATE | NO | Lunes de la semana. Índice. |
| WeekEndSunday | DATE | SÍ | Domingo. |
| PredictedRevenue | DECIMAL(18,2) | NO | Facturación total estimada. |
| ActualRevenue | DECIMAL(18,2) | SÍ | Rellenado al cerrar la semana. |
| DailyPredictionsJson | TEXT | SÍ | JSON: array de días con date, revenue, min, max, y por turno (mediodia, tarde, noche) revenue. |
| HistoricalStatsJson | TEXT | SÍ | JSON: base_revenue, semanas_usadas, ajustes. |
| AccuracyMetricsJson | TEXT | SÍ | JSON: error_pct, accuracy, staff_estimated, staff_actual. |
| EstimatedStaffHours | DECIMAL(18,2) | SÍ | Horas de personal estimadas para la semana. |
| CompletedAt | DATETIME | SÍ | Cuando se cerró la semana. |
| CreatedAt | DATETIME | NO | |

---

## 8. Tabla Events

Eventos externos (festivos, obras, etc.) por fecha.

| Columna | Tipo | Nullable | Descripción |
|---------|------|----------|-------------|
| Id | GUID | NO | PK. |
| Name | VARCHAR(500) | NO | |
| Description | TEXT | SÍ | |
| EventDate | DATE | NO | Índice. |
| Type | VARCHAR(50) | NO | Holiday, MenuChange, Special, etc. |
| Impact | VARCHAR(50) | NO | Alto, Medio, Bajo. |
| CreatedAt | DATETIME | NO | |

---

## 9. Convenciones para MySQL

- GUID: `CHAR(36)` o `BINARY(16)` (UUID). En PHP se suele usar `CHAR(36)`.
- Boolean: `TINYINT(1)` (0/1).
- Decimal: `DECIMAL(18,2)` para dinero y horas; `DECIMAL(5,2)` para temperatura.
- Fechas: `DATE` para día, `DATETIME` o `TIMESTAMP` para CreatedAt/UpdatedAt.
- JSON: `TEXT` o `JSON` (MySQL 5.7+).

---

## 10. Referencia al código actual

- Entidades: `ManagerOS.Core/Entities/*.cs`.
- Configuración EF: `ManagerOS.Infrastructure/Data/ApplicationDbContext.cs` (OnModelCreating).
- Migraciones: `ManagerOS.Infrastructure/Migrations/*.cs` (nombres y orden de columnas).
