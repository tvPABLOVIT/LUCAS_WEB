# Funcionamiento exacto del botón "Guardar Lucas"

Documento que describe **paso a paso** qué ocurre cuando el usuario pulsa el botón **"Guardar Lucas"** en la pestaña Preguntas de Lucas (tablet): desde el evento click hasta la persistencia en base de datos y la sincronización opcional con Google Sheets.

---

## 1. Dónde está el botón

- **Pantalla:** Pestaña **Preguntas** de Lucas (app web para tablet), en `ManagerOS.Backend/wwwroot/feedback/index.html`.
- **Elemento:** `<button type="button" id="btnSave" class="btn-save">Guardar Lucas</button>`.
- **Evento:** En `app.js`, al cargar la app se hace `btnSave.addEventListener('click', saveDay)`. Al hacer clic se ejecuta la función **saveDay()**.

---

## 2. Flujo completo (resumen)

1. Guardar en memoria el turno actual (formulario → `dataByShift[currentShift]`).
2. Obtener fecha, construir payload de los 3 turnos y facturación del día.
3. Deshabilitar el botón y mostrar "Guardando…".
4. Hacer **GET** al día para saber si ya existe.
5. Si **404** → **POST** `/api/execution` (crear día). Si **200** → **PATCH** `/api/execution/{date}` (actualizar día).
6. Si la respuesta es correcta: mostrar "Día guardado" y volver a **cargar el día** (GET + rellenar formulario). Si hay error: mostrar "Error al guardar".
7. En todos los casos: volver a habilitar el botón.

En el Backend, al crear o actualizar se persiste en SQLite y, si está configurado, se lanza en segundo plano la sincronización con Google Sheets.

---

## 3. Paso a paso en el cliente (app.js)

### 3.1. saveFormToShift(currentShift)

- **Qué hace:** Escribe en memoria el estado actual del formulario en el turno que está seleccionado.
- **Detalle:**
  - Lee los **radios** Q1–Q4 (`input[name="q1"]:checked`, etc.) y guarda el **value** en `dataByShift[currentShift].feedback_q1` … `feedback_q4` (o null si no hay selección).
  - Lee **Personal sala** (`inputStaffFloor`) y **Personal cocina** (`inputStaffKitchen`): `parseInt(..., 10) || 0` → `staff_floor`, `staff_kitchen`.
  - Si el turno es **Noche** y existe el input de facturación del día (`inputRevenueDay`): `parseFloat(inputRevenueDay.value, 10)` → `day_revenue` (o null si no es número).
- **Por qué:** Así, lo que el usuario ve en pantalla (incluido el turno actual) queda reflejado en `dataByShift` antes de montar el payload.

### 3.2. dateStr, shifts, totalRevenue, staffTotal, totalHoursWorked

- **dateStr** = `inputDate.value || todayStr()` (fecha del input, formato yyyy-MM-dd).
- **shifts** = `buildShiftsPayload()`:
  - Para cada turno en `['Mediodia', 'Tarde', 'Noche']` se toma `getShiftData(s)` y se devuelve un objeto por turno:
    - `shift_name`: nombre del turno.
    - `revenue`: **0** (en Preguntas no se introduce facturación por turno).
    - `hours_worked`: **0** (en Preguntas no se introducen horas).
    - `staff_floor`, `staff_kitchen`: del objeto del turno (o 0).
    - `feedback_q1`, `feedback_q2`, `feedback_q3`, `feedback_q4`: texto de la opción o null.
    - `recorded_by`: el que venga del servidor (si se cargó el día antes); si no, null.
    - `edited_by`: **null** (el cliente no lo rellena al guardar).
- **totalRevenue** = `getTotalRevenueFromPayload()` = valor de `dataByShift['Noche'].day_revenue` si es un número válido; si no, 0. Es la **facturación del día entero** (solo se puede introducir en el turno Noche).
- **staffTotal** = suma de `(staff_floor + staff_kitchen)` de los tres turnos.
- **totalHoursWorked** = **0** (en Preguntas no se introducen horas).

### 3.3. Deshabilitar botón y mensaje

- `btnSave.disabled = true`.
- `setStatus('Guardando…')` (mensaje visible en la UI).

### 3.4. Decisión POST vs PATCH

- Se llama a **GET** `API_BASE + '/api/execution/' + encodeURIComponent(dateStr)` (misma URL que al cargar el día).
- En el **then** del GET:
  - Si **r.status === 404** → no existe el día → se ejecuta **doPost()**.
  - En caso contrario (200 u otro) → se ejecuta **doPatch()**.
- **doPost()** = `apiPost(dateStr, body)` con body:
  - `date`: dateStr (yyyy-MM-dd).
  - `total_revenue`: totalRevenue.
  - `total_hours_worked`: 0.
  - `staff_total`: staffTotal.
  - `notes`: null.
  - `shifts`: array de 3 turnos (igual que arriba).
- **doPatch()** = `apiPatch(dateStr, body)` con body:
  - `shifts`: mismo array de 3 turnos.
  - `total_revenue`: totalRevenue.
  - `total_hours_worked`: 0.
  - `staff_total`: staffTotal.
  - (no se envía `notes` en el código actual de saveDay; el backend acepta `request.Notes != null` para actualizar).

### 3.5. Tras la respuesta (POST o PATCH)

- **then:** Si `!r.ok`, se hace `r.json().then(...)` y se lanza un error para ir al catch. Si `r.ok`:
  - `setStatus('Día guardado')`.
  - **loadDay()**: se vuelve a hacer GET del mismo día y se rellenan `dataByShift` y el formulario con la respuesta del servidor (datos tal como quedaron guardados).
- **catch:** `setStatus('Error al guardar', true)`.
- **finally:** `btnSave.disabled = false`.

---

## 4. Llamadas HTTP que se generan

| Orden | Método | URL | Cuándo |
|-------|--------|-----|--------|
| 1 | GET | `/api/execution/{date}` | Siempre, para decidir si el día existe. |
| 2a | POST | `/api/execution` | Solo si el GET devolvió 404 (crear día). |
| 2b | PATCH | `/api/execution/{date}` | Solo si el GET devolvió 200 (actualizar día). |
| 3 | GET | `/api/execution/{date}` | Solo si POST o PATCH fue correcto (loadDay para refrescar). |

**Headers:** En todas las peticiones se envían `authHeaders()`: `Accept: application/json` y, si hay token en sessionStorage, `Authorization: Bearer <token>`. En POST y PATCH además `Content-Type: application/json`. `credentials: 'include'` para enviar cookies.

---

## 5. Cuerpo del POST (crear día)

```json
{
  "date": "yyyy-MM-dd",
  "total_revenue": <número o 0>,
  "total_hours_worked": 0,
  "staff_total": <suma sala+cocina de los 3 turnos>,
  "notes": null,
  "shifts": [
    {
      "shift_name": "Mediodia",
      "revenue": 0,
      "hours_worked": 0,
      "staff_floor": <número>,
      "staff_kitchen": <número>,
      "feedback_q1": "<texto opción>" | null,
      "feedback_q2": "<texto opción>" | null,
      "feedback_q3": "<texto opción>" | null,
      "feedback_q4": "<texto opción>" | null,
      "recorded_by": null,
      "edited_by": null
    },
    { "shift_name": "Tarde", ... },
    { "shift_name": "Noche", ... }
  ]
}
```

`total_revenue` es el valor introducido en "Facturación del día entero" (solo visible en turno Noche); si no se rellenó o no es número, 0.

---

## 6. Cuerpo del PATCH (actualizar día)

```json
{
  "shifts": [ /* mismo array de 3 turnos que arriba */ ],
  "total_revenue": <número>,
  "total_hours_worked": 0,
  "staff_total": <número>
}
```

No se envía `notes` desde saveDay; el backend solo actualiza Notes si `request.Notes != null`.

---

## 7. Qué hace el Backend (ExecutionController)

### 7.1. POST (Create)

- Valida que `request.Date` exista. Convierte a `dateOnly` (solo fecha).
- Comprueba que no exista ya un `ExecutionDay` con esa fecha; si existe → **409 Conflict** ("Day already exists; use PATCH to update.").
- Crea un nuevo **ExecutionDay**: Id (nuevo Guid), Date, TotalRevenue, TotalHoursWorked, StaffTotal, Notes, CreatedAt, UpdatedAt.
- **MapShifts(request.Shifts, day.Id):** Por cada elemento de `request.Shifts` con nombre de turno válido (Mediodia, Tarde, Noche normalizado) crea un **ShiftFeedback** con: ShiftName, Revenue, HoursWorked, StaffFloor, StaffKitchen, FeedbackQ1–Q4, RecordedBy, EditedBy, CreatedAt. Los asocia al día.
- `_db.ExecutionDays.Add(day)` y `await _db.SaveChangesAsync(ct)`.
- **SyncToGoogleSheet(day):** en segundo plano (no bloquea la respuesta). Ver sección 9.
- Responde **201 Created** con `ToResponse(day, shifts)` (mismo formato que GET: id, date, total_revenue, total_hours_worked, staff_total, notes, shifts).

### 7.2. PATCH (Update)

- Parsea la fecha de la ruta (yyyy-MM-dd). Si no es válida → **400 Bad Request**.
- Busca el **ExecutionDay** con esa fecha e incluye **ShiftFeedbacks**. Si no existe → **404 Not Found**.
- Si `request.Shifts != null`: por cada turno en el body:
  - Se normaliza el nombre (Mediodia, Tarde, Noche).
  - Si ya existe un ShiftFeedback con ese nombre en el día: se actualizan Revenue, HoursWorked, StaffFloor, StaffKitchen, FeedbackQ1–Q4, RecordedBy, EditedBy.
  - Si no existe: se añade un nuevo ShiftFeedback con esos datos.
  - Luego se actualizan del día: **TotalRevenue** = `request.TotalRevenue ?? Sum(Revenue de turnos)`, **TotalHoursWorked** = `request.TotalHoursWorked ?? Sum(HoursWorked)`, **StaffTotal** = `request.StaffTotal ?? Sum(StaffFloor+StaffKitchen)`. Si `request.Notes != null`, se actualiza Notes. **UpdatedAt** = ahora.
- `await _db.SaveChangesAsync(ct)`.
- **SyncToGoogleSheet(day)** en segundo plano.
- Responde **200 OK** con `ToResponse(day, list)` (lista de turnos ordenada Mediodía, Tarde, Noche).

**Nota:** En Preguntas siempre se envían revenue y hours_worked en 0 por turno; por tanto el backend usa los valores del request para TotalRevenue/TotalHoursWorked/StaffTotal (request.TotalRevenue es el "Facturación del día entero" y request.TotalHoursWorked es 0).

---

## 8. Qué se persiste en la base de datos

- **ExecutionDays:** una fila por día (Id, Date, TotalRevenue, TotalHoursWorked, StaffTotal, Notes, CreatedAt, UpdatedAt). TotalRevenue = valor enviado como "facturación del día entero" (desde Noche). TotalHoursWorked = 0 cuando se guarda desde Preguntas.
- **ShiftFeedbacks:** tres filas por día (una por turno): Id, ExecutionDayId, ShiftName, Revenue (0 desde Preguntas), HoursWorked (0), StaffFloor, StaffKitchen, FeedbackQ1, FeedbackQ2, FeedbackQ3, FeedbackQ4, RecordedBy, EditedBy, CreatedAt. Los textos de las opciones (Q1–Q4) se guardan tal cual.

Misma base SQLite que usa la app Windows cuando el Backend comparte la misma configuración (ruta de la base).

---

## 9. Sincronización con Google Sheets (SyncToGoogleSheet)

- **Cuándo:** Se invoca después de `SaveChangesAsync` tanto en Create como en Update. Se ejecuta en **segundo plano** (`Task.Run`), no bloquea la respuesta HTTP.
- **Condición:** En la carpeta de configuración (LocalApplicationData/ManagerOS) debe existir `settings.json` con **GoogleSheetsUrl** (URL del spreadsheet) y opcionalmente **GoogleCredentialsPath** (ruta al JSON de credenciales). Si no hay ruta, se usa `google-credentials.json` en esa misma carpeta. Si falta sheetId o credenciales, no se hace nada.
- **Qué hace SyncAsync:**
  - Obtiene/crea la **hoja del mes** (p. ej. "Febrero 2026") en el spreadsheet (si no existe, se duplica "Plantilla" o la primera hoja).
  - Construye **una fila** por día: **Fecha** (yyyy-MM-dd), **Día** (nombre en español), **Observaciones mediodía**, **Observaciones tarde**, **Observaciones noche** (texto resumen Nivel 3 a partir de FeedbackQ1–Q4 con TurnoResumenBuilder.BuildResumenFromFeedback), **Facturación mediodía**, **Facturación tarde**, **Facturación noche**, **Total** (TotalRevenue del día).
  - En guardados desde Preguntas, Revenue por turno es 0, así que las columnas de facturación por turno quedan en 0 y **Total** es el valor de "Facturación del día entero".
  - La fila se escribe en la posición **día del mes + 1** (día 1 → fila 2, día 31 → fila 32) en el rango A:I de la hoja del mes. Si ya había datos en esa fila, se **sobrescriben**.
- Si SyncAsync lanza excepción, se registra en log y no afecta al guardado en BD.

---

## 10. Resumen: qué genera el botón "Guardar Lucas"

| Qué | Dónde / Cómo |
|-----|----------------|
| Guardado en memoria del turno actual | saveFormToShift(currentShift) en dataByShift |
| Payload de 3 turnos (feedback + personal; revenue/horas = 0) | buildShiftsPayload() |
| Facturación del día | getTotalRevenueFromPayload() (Noche.day_revenue) |
| GET para comprobar si existe el día | GET /api/execution/{date} |
| Creación del día | POST /api/execution (si 404) → ExecutionDay + 3 ShiftFeedbacks en SQLite |
| Actualización del día | PATCH /api/execution/{date} (si existe) → actualiza día y turnos en SQLite |
| Refresco del formulario | loadDay() = GET /api/execution/{date} + rellenar dataByShift y UI |
| Mensajes al usuario | "Guardando…" → "Día guardado" o "Error al guardar" |
| Sincronización opcional | SyncToGoogleSheet(day) → actualiza/inserta fila en Google Sheet (hoja del mes, fila = día+1, columnas Fecha, Día, Obs x3, Fact x3, Total) |

Con esto queda documentado de forma exacta el funcionamiento del botón "Guardar Lucas" y todo lo que genera en cliente, red, backend y Google Sheets.
