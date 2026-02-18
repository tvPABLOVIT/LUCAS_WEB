# Análisis: pestaña Feedback diario (Preguntas)

Documento que explica **cómo funciona la pestaña Feedback diario** en Lucas Web: ruta, estado, carga y guardado del día, 5 preguntas, clima por turno, y relación con el resto del sistema.

---

## 1. Dónde está

- **Ruta:** `#preguntas` (tab "Feedback diario" en la barra de navegación).
- **Vista:** `LucasWeb.Api/wwwroot/js/views/preguntas.js` → `LUCAS_PREGUNTAS_VIEW.render()` / `renderAndLoad()`.
- **Roles:**
  - **admin, manager, master:** ven todas las pestañas (Dashboard, Estimaciones, Feedback diario, Configuración, Registro). Al entrar en Feedback diario se llama `render(container)` y, si hace falta, `loadDay(fecha)`.
  - **user:** solo ven la pantalla Feedback diario (sin tabs). Se llama `renderAndLoad(container)`, que inicializa estado con hoy y hace `loadDay(todayStr())`.

---

## 2. Objetivo

- Recoger **feedback por turno** (Mediodía, Tarde, Noche) para cada día.
- Por cada turno se guardan:
  - **5 respuestas:** Q1 Volumen, Q2 Ritmo, Q3 Margen, Q4 Dificultad (sala), Q5 Dificultad cocina. En backend se derivan SGT, Estado y Tipo (T1–T15).
  - **Personal sala** y **personal cocina**.
  - **Clima del turno** (weather_code, weather_temp_avg, weather_precip_mm, weather_wind_max_kmh): se obtiene para la fecha (GET /api/weather/for-date) y se guarda con el turno al hacer "Guardar Lucas".
- En el turno **Noche** además: **facturación del día entero** (€), que se envía como `total_revenue` del día.
- La facturación y horas **por turno** se editan en **Registro de ejecución**; en Feedback solo se envía la facturación global del día (campo Noche).

---

## 3. Fecha y URL

- La fecha inicial puede venir de la **URL**. Como la app usa **hash** (`#preguntas`, `#preguntas?date=2025-02-12`), la fecha se lee con **getDateFromHash()**: se toma el query del hash (`?date=yyyy-MM-dd`) y se valida el formato. Si no hay fecha en el hash, se usa `window.location.search` por compatibilidad.
- Enlaces desde Dashboard con `#preguntas?date=YYYY-MM-DD` abren directamente ese día.

---

## 4. Estado en el cliente

- **state = { dayData: null, activeShiftIndex: 0 }**
  - **dayData:** objeto del día actual: `date`, `total_revenue`, `total_hours_worked`, `staff_total`, `notes`, clima del día (`weather_code`, `weather_temp_max`, etc.) y **shifts** (array de 3 turnos).
  - **activeShiftIndex:** 0 = Mediodía, 1 = Tarde, 2 = Noche.
- Cada elemento de **shifts** tiene: `shift_name`, `revenue`, `hours_worked`, `staff_floor`, `staff_kitchen`, `feedback_q1`..`feedback_q5`, `recorded_by`, `edited_by`, y **clima por turno**: `weather_code`, `weather_temp_avg`, `weather_precip_mm`, `weather_wind_max_kmh`.
- Los datos del API se normalizan con **normalizeDayData(apiData)** para rellenar `state.dayData` (nombres de turno Mediodia/Tarde/Noche, feedback_q1..q5, clima por turno, etc.).

---

## 5. Carga del día (loadDay)

1. Se llama **GET /api/execution/{date}**.
2. **404:** no existe día. Se usa `defaultDayData(dateStr)` (tres turnos vacíos), se pinta el formulario, se actualiza semana y se llama **fetchWeatherForDate(dateStr)** para obtener y mostrar clima (y guardarlo en estado para ese día).
3. **200:** se aplica **normalizeDayData(data)** y se elige el turno activo (primer turno con feedback completo, o según hora con getShiftByCurrentTime()). Se actualizan fecha, día de la semana, **Semana N** (`preguntas-week-label`) y el contenido del formulario. Se hace **bind** y **updateStatus()**. Se llama **fetchWeatherForDate(state.dayData.date)** para rellenar clima por turno si no venía ya; al terminar se actualizan UI de clima e indicadores de tabs.
4. En caso de error de red se muestra mensaje de error en el wrap.

---

## 6. Interfaz

- **Cabecera:** título "Feedback diario", **Semana N** (preguntas-week-label), selector de fecha (día de la semana + [◀] fecha [▶]), pestañas **Mediodía | Tarde | Noche**, y **Clima:** texto del turno activo (o "Clima no disponible para este día" si no hay datos).
- **Formulario (por turno):** Personal sala, Personal cocina; en Noche además "Facturación del día entero (€)". Cinco bloques de pregunta (Q1–Q5) en grid 2 columnas. Cada bloque se marca con clase `preguntas-block--answered` cuando tiene respuesta.
- **Indicador en tabs:** cada pestaña de turno muestra un punto verde (clase `preguntas-shift-tab--complete`) cuando ese turno tiene las 5 preguntas respondidas.
- **Botón "Guardar Lucas"** y mensaje de estado (Guardando… / Día guardado / error). El mensaje tiene `aria-live="polite"` y, en error, `role="alert"`; tras guardar el foco va al mensaje (`tabindex="-1"` + `focus()`). El botón usa `aria-busy` durante la petición.

---

## 7. Cambio de turno

- Al hacer clic en otra pestaña se llama **collectFormFromShift(activeShiftIndex)** para guardar el formulario actual en estado. Se actualiza **activeShiftIndex** y las clases active de los tabs.
- Solo se reemplaza el **contenido interior** de la card (**preguntas-card-body**) con **getFormInnerHtml()**, sin reemplazar todo el wrap ni volver a hacer **bind(container)**. Se rellenan campos con **fillFormForShift(idx)**, se actualizan resaltados y estado, y se enlazan de nuevo solo el botón Guardar y los radios (**bindRadioChanges()**). Así se evita doble binding de fecha, prev/next y tabs.

---

## 8. Guardado (save)

1. **collectFormFromShift(activeShiftIndex)** para persistir el turno actual en `state.dayData.shifts`.
2. Se construye **shiftsPayload** con los 3 turnos: shift_name, revenue, hours_worked, staff_floor, staff_kitchen, feedback_q1..q5, recorded_by, edited_by, y **clima por turno** (weather_code, weather_temp_avg, weather_precip_mm, weather_wind_max_kmh) si existen.
3. Se hace **GET** al mismo día: si **404** → **POST /api/execution** (crear día); si **200** → **PATCH /api/execution/{date}** (actualizar). En ambos casos se envían shifts (con clima) y total_revenue, total_hours_worked, staff_total.
4. Tras éxito: mensaje "Día guardado", foco al mensaje, y **loadDay(dateStr, { preserveShift: true })**. En error: mensaje con `role="alert"` y foco al mensaje.

---

## 9. Clima

- **Visualización:** Se muestra el clima del **turno activo** (o del día si no hay por turno). Si no hay datos se muestra "Clima no disponible para este día".
- **Obtención:** Tras cargar el día (200 o 404) se llama **fetchWeatherForDate(dateStr)**. Se hace **GET /api/weather/for-date?date=yyyy-MM-dd** (cualquier usuario autenticado). La respuesta trae `day` (clima del día) y `shifts` (clima por turno Mediodia/Tarde/Noche). Se fusiona en `state.dayData` sin sobrescribir clima ya guardado (p. ej. desde execution).
- **Guardado:** Al guardar, cada turno envía en el payload sus campos de clima (weather_code, weather_temp_avg, weather_precip_mm, weather_wind_max_kmh). El backend (**ExecutionController.ToShift**) los persiste en **ShiftFeedback**.

---

## 10. API Backend

- **GET /api/execution/{date}:** devuelve el día con shifts (incl. feedback_q1..q5, clima por turno). 404 si no existe.
- **POST /api/execution**, **PATCH /api/execution/{date}:** body con shifts (con clima por turno), total_revenue, total_hours_worked, staff_total. **ToShift** mapea también WeatherCode, WeatherTempAvg, WeatherPrecipMm, WeatherWindMaxKmh a la entidad ShiftFeedback.
- **GET /api/weather/for-date?date=yyyy-MM-dd:** devuelve clima del día y por turno para esa fecha (para mostrar y completar estado en Feedback). Requiere usuario autenticado (roles admin, manager, master, user).

---

## 11. Resumen

| Aspecto | Detalle |
|--------|---------|
| **Ruta** | `#preguntas`; fecha opcional en hash: `#preguntas?date=yyyy-MM-dd`. |
| **Qué recoge** | Por turno: 5 preguntas (Q1–Q5), personal sala/cocina, clima; en Noche además facturación del día. |
| **Estado** | `state.dayData.shifts` (normalizeDayData); turno activo en `activeShiftIndex`. |
| **Carga** | GET /api/execution/{date}; luego fetchWeatherForDate para clima. |
| **Guardado** | POST o PATCH con shifts (incl. clima) y totales. |
| **UX** | Semana en cabecera; indicador de turno completo en tabs; solo se actualiza contenido del form al cambiar de turno; accesibilidad (aria-live, role alert, foco). |

Con esto queda descrito el funcionamiento actual de la pestaña Feedback diario: 5 preguntas, estado, URL, semana, clima por turno (ver y guardar), refactor de binding y accesibilidad.
