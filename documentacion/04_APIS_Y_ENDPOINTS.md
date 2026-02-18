# 04 — APIs y endpoints

Listado completo de endpoints del Backend actual para replicar en la app web. Incluye método, ruta, cuerpo, respuesta y autenticación.

---

## 1. Base URL y autenticación

- **Base:** En el Backend actual, si la app se sirve bajo `/feedback`, las peticiones API van a `/feedback/api/...` (reescritas internamente a `/api/...`). En una app web independiente, usar directamente `/api/...`.
- **Autenticación:** Sesión (cookie) o header `Authorization: Bearer <token>` (token devuelto por `POST /api/auth/pin`). Ver 05_AUTENTICACION_Y_ROLES.

---

## 2. Auth

### POST /api/auth/pin

**Descripción:** Login por PIN. Crea sesión y opcionalmente devuelve token para Bearer.

**Request body (JSON):**
```json
{ "pin": "1234" }
```

**Response 200 (JSON):**
```json
{
  "role": "admin",
  "userId": "guid",
  "token": "opcional-token-para-bearer"
}
```

**Response 401:** `{ "error": "PIN requerido" }` o `{ "error": "PIN incorrecto" }`.

---

### GET /api/auth/me

**Descripción:** Devuelve el usuario actual (sesión o Bearer).

**Headers:** Cookie de sesión o `Authorization: Bearer <token>`.

**Response 200 (JSON):**
```json
{
  "userId": "guid",
  "role": "admin"
}
```

**Response 401:** `{ "error": "No hay sesión" }` o `{ "error": "Sesión inválida" }`.

---

### POST /api/auth/logout

**Descripción:** Cierra sesión (cookie y/o invalida token).

**Response 204:** Sin cuerpo.

---

## 3. Execution (registro de ejecución)

### GET /api/execution

**Descripción:** Lista los últimos días de ejecución (resumen).

**Query:** `days` (opcional, default 90, máx 365).

**Response 200 (JSON):** Array de:
```json
{
  "id": "guid",
  "date": "yyyy-MM-dd",
  "total_revenue": 1234.56,
  "total_hours_worked": 45.5,
  "staff_total": 8
}
```

---

### GET /api/execution/{date}

**Descripción:** Obtiene el día de ejecución por fecha (para cargar formulario de feedback/registro). `date` = yyyy-MM-dd.

**Response 200 (JSON):**
```json
{
  "id": "guid",
  "date": "yyyy-MM-dd",
  "total_revenue": 1234.56,
  "total_hours_worked": 45.5,
  "staff_total": 8,
  "notes": "...",
  "shifts": [
    {
      "shift_name": "Mediodia",
      "revenue": 400,
      "hours_worked": 6,
      "staff_floor": 2,
      "staff_kitchen": 1,
      "feedback_q1": "Media sala",
      "feedback_q2": "Entradas tranquilas",
      "feedback_q3": "Justo",
      "feedback_q4": "Normal",
      "recorded_by": null,
      "edited_by": null
    }
  ]
}
```

**Response 404:** Día no existe.

---

### POST /api/execution

**Descripción:** Crea un día de ejecución (y sus turnos).

**Request body (JSON):**
```json
{
  "date": "2026-02-08",
  "total_revenue": 1200,
  "total_hours_worked": 42,
  "staff_total": 7,
  "notes": null,
  "shifts": [
    {
      "shift_name": "Mediodia",
      "revenue": 400,
      "hours_worked": 6,
      "staff_floor": 2,
      "staff_kitchen": 1,
      "feedback_q1": "Media sala",
      "feedback_q2": "Entradas tranquilas",
      "feedback_q3": "Justo",
      "feedback_q4": "Normal",
      "recorded_by": null,
      "edited_by": null
    }
  ]
}
```

**Response 201:** Mismo cuerpo que GET /api/execution/{date} para esa fecha.  
**Response 400:** "Date is required."  
**Response 409:** "Day already exists; use PATCH to update."

---

### PATCH /api/execution/{date}

**Descripción:** Actualiza un día existente (turnos, total_revenue, total_hours_worked, staff_total, notes).

**Request body (JSON):** Campos opcionales:
```json
{
  "total_revenue": 1250,
  "total_hours_worked": 43,
  "staff_total": 8,
  "notes": "...",
  "shifts": [ /* mismo formato que POST */ ]
}
```

**Response 200:** Mismo formato que GET por fecha.  
**Response 404:** Día no existe.

---

## 4. Dashboard

### GET /api/dashboard/week

**Descripción:** Resumen de la semana (lunes a domingo).

**Query:** `weekStart` (opcional): lunes en yyyy-MM-dd. Si no se envía, se usa la semana que contiene hoy.

**Response 200 (JSON):**
```json
{
  "totalRevenue": 8500,
  "avgProductivity": 48.5,
  "totalHours": 175,
  "avgStaff": 7.2,
  "avgRevenueHistoric": 8200,
  "prevWeekRevenue": 8000,
  "prevWeekProductivity": 47,
  "resumenClasificacion": "🟢 Semana buena",
  "resumenTexto": "Esta semana la facturación ha sido de 8.500 € ...",
  "days": [
    {
      "dayName": "Lunes",
      "date": "yyyy-MM-dd",
      "revenue": 1200,
      "productivity": 50,
      "avgRevenueHistoric": 1150,
      "historicalCount": 10,
      "trendLabel": "↑ Al alza",
      "staffTotal": 7,
      "context": "—"
    }
  ]
}
```

---

## 5. Recommendations y Predictions

### GET /api/recommendations

**Descripción:** Lista de recomendaciones (pendientes o recientes).

**Query:** `limit` (opcional, default 50, máx 100).

**Response 200 (JSON):** Array de:
```json
{
  "id": "guid",
  "type": "Operativa",
  "title": "...",
  "description": "...",
  "priority": 3,
  "status": "pending",
  "relatedDate": null,
  "createdAt": "2026-02-08T..."
}
```

---

### PATCH /api/recommendations/{id}

**Descripción:** Actualiza el estado de una recomendación.

**Request body (JSON):**
```json
{ "status": "applied" }
```
Valores: `accepted`, `applied`, `discarded`.

**Response 200:** Objeto recomendación actualizado.  
**Response 400:** Estado no válido.  
**Response 404:** No existe.

---

### GET /api/recommendations/version

**Descripción:** Versión de datos (timestamp o cadena que cambia al guardar ejecución, recomendaciones o predicciones). La web puede usarlo para refrescar solo cuando hay cambios.

**Response 200 (JSON):**
```json
{ "version": "2026-02-08T12:00:00.0000000Z" }
```

---

### GET /api/predictions/next-week

**Descripción:** Predicción de la semana siguiente (si existe).

**Response 200 (JSON):** Objeto o null.
```json
{
  "weekStart": "yyyy-MM-dd",
  "predictedRevenue": 9000,
  "dailyPredictionsJson": "[ { \"date\": \"...\", \"revenue\": 1200, \"min\": 1000, \"max\": 1400, \"mediodia\": {...}, \"tarde\": {...}, \"noche\": {...} } ]",
  "historicalStatsJson": "..."
}
```

---

## 6. Estimaciones (caché de la app Windows)

En el Backend actual, la app Windows publica una “vista” de estimaciones en un archivo JSON; el endpoint la sirve para la tablet.

### GET /api/estimaciones

**Descripción:** Devuelve la última vista de estimaciones publicada por la app Windows (caché en disco). Si no hay caché, estructura vacía.

**Response 200 (JSON):** Objeto con campos como:
- `titulo`, `mensaje`
- `kpiFacturacionPromedioDisplay`, `kpiProductividadPromedioDisplay`, `kpiHorasPromedioDisplay`
- `kpiCostoPersonalPctFacturacion`, `kpiCostoPersonalPredPrincipal`
- `resumenClasificacion`, `resumenTexto`
- `weekNumberText`, `weekRangeText`
- `predictedRevenue`, `hasPrediction`
- `daysTop`, `daysBottom` (arrays de días con dayName, date, predictedRevenue, minRevenue, maxRevenue, confidenceLabel, shifts, salaScheme, cocinaScheme, weatherDescription, isHoliday, holidayName)
- `alertasGenerales` (array de { tipo, title, description })

**En una app web pura:** Este endpoint puede no existir; en su lugar la app web calculará estimaciones con la misma lógica (dashboard/week + predictions/next-week + recomendaciones + alertas propias) o expondrá un endpoint equivalente que ejecute esa lógica en el servidor.

---

### POST /api/estimaciones/cache

**Descripción:** Guarda la vista de estimaciones (llamado por la app Windows al cargar la pestaña). En una app web solo backend, puede omitirse o usarse para caché interno.

**Request body:** Mismo formato que la respuesta de GET /api/estimaciones.  
**Response 204:** Sin cuerpo.

---

## 7. Salud y diagnóstico (opcionales)

- **GET /health** → `{ "status": "ok", "backend": "ManagerOS" }`
- **GET /api/health** → `{ "status": "ok", "message": "..." }`
- **GET /api/debug** → path, pathBase, method, dbPath (solo desarrollo)
- **GET /api/info** → databasePath, hint

---

## 8. Referencia al código actual

- **AuthController.cs:** LoginByPin, Me, Logout.
- **ExecutionController.cs:** List, GetByDate, Create, Update.
- **DashboardController.cs:** GetWeek.
- **RecommendationsController.cs:** List, UpdateStatus, GetDataVersion.
- **PredictionsController** (en el mismo archivo): NextWeek.
- **EstimacionesController.cs:** Get, SaveCache.
