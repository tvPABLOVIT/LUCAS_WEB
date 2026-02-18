# Lucas Web — Inventario de APIs Backend

**Versión:** 1.0  
**Fecha:** Febrero 2026

---

## Autenticación

| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | `/api/auth/pin` | Login por PIN. Body: `{ "pin": "1234" }`. Respuesta: `{ role, userId, token }` |
| GET | `/api/auth/me` | Usuario actual (sesión o Bearer). Respuesta: `{ userId, role }` |
| POST | `/api/auth/logout` | Cierra sesión y elimina token |

**Autenticación en peticiones:**  
- Cookie de sesión (Path=/feedback)  
- O header: `Authorization: Bearer {token}` (recomendado para SPA/tablet)

---

## Ejecución (Registro)

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/execution` | Lista resúmenes de días (query `days`, máx. 365) |
| GET | `/api/execution/{date}` | Día por fecha (yyyy-MM-dd) con turnos |
| POST | `/api/execution` | Crea día (body: date, total_revenue, shifts, …). 409 si ya existe |
| PATCH | `/api/execution/{date}` | Actualiza día y turnos (feedback tablet) |

**DTOs principales (snake_case en JSON):**
- `CreateExecutionRequest`: date, total_revenue, total_hours_worked, staff_total, notes, shifts
- `UpdateExecutionRequest`: total_revenue, total_hours_worked, staff_total, notes, shifts
- `ShiftDto`: shift_name, revenue, hours_worked, staff_floor, staff_kitchen, feedback_q1..q4, recorded_by, edited_by

---

## Dashboard

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/dashboard/week?weekStart=yyyy-MM-dd` | Resumen semanal (lunes a domingo) |

**Respuesta (camelCase):** totalRevenue, avgProductivity, totalHours, avgStaff, avgRevenueHistoric, prevWeekRevenue, prevWeekProductivity, resumenClasificacion, resumenTexto, days (array de DashboardDayItemDto).

---

## Estimaciones

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/estimaciones` | Cache publicado por la app Windows (KPIs, días, alertas) |
| POST | `/api/estimaciones/cache` | Guarda cache (llamado por app Windows) |
| GET | `/api/predictions/next-week` | Predicción semana siguiente (WeeklyPrediction) |
| GET | `/api/recommendations` | Lista recomendaciones (query limit) |
| PATCH | `/api/recommendations/{id}` | Actualiza estado (accepted, applied, discarded) |
| GET | `/api/recommendations/version` | Versión de datos (para refresco) |

---

## Ejemplo: Login

```http
POST /api/auth/pin
Content-Type: application/json

{"pin": "1234"}
```

```json
{
  "role": "manager",
  "userId": "guid",
  "token": "hex-token"
}
```

---

## Ejemplo: Obtener día de ejecución

```http
GET /api/execution/2026-02-06
Authorization: Bearer {token}
```

```json
{
  "id": "guid",
  "date": "2026-02-06",
  "total_revenue": 2400,
  "total_hours_worked": 24,
  "staff_total": 6,
  "notes": null,
  "shifts": [
    {
      "shift_name": "Mediodia",
      "revenue": 800,
      "hours_worked": 6,
      "staff_floor": 2,
      "staff_kitchen": 1,
      "feedback_q1": "Media sala",
      "feedback_q2": "Flujo constante",
      "feedback_q3": "Justo",
      "feedback_q4": "Normal",
      "recorded_by": null,
      "edited_by": null
    }
  ]
}
```
