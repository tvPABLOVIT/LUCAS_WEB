# Auditoría completa — Pestaña Dashboard

**Fecha:** 26 de febrero de 2026  
**Alcance:** Cada botón, espacio, dato e información de la pestaña Dashboard (frontend, backend, estilos, flujos).

---

## 1. Resumen ejecutivo

El Dashboard es la vista principal tras el login. Muestra datos de una semana seleccionada (KPIs, tabla de días, resumen, importación Excel/PDF y gráfico de facturación últimos 30 días). Esta auditoría recorre **cada elemento** de la interfaz y del flujo de datos para detectar inconsistencias, bugs, mejoras de UX y alineación con la API.

---

## 2. Estructura HTML generada (dashboard.js)

La vista se genera por `LUCAS_DASHBOARD_VIEW.render(container)` y rellena `#dashboard-content`. El HTML generado incluye:

| Elemento | ID / clase | Descripción |
|----------|------------|-------------|
| Fila título | `.dashboard-title-row` | Contiene título, subtítulo y barra de semana |
| Título | `h2.view-title` | Texto: "Dashboard" |
| Subtítulo | `p.dashboard-subtitle` | Texto: "Datos de la semana seleccionada" |
| Botón Actualizar | `#dashboard-cargar` | Clase: `btn-primary dashboard-week-btn-actualizar`. Texto: "Actualizar" |
| Flecha anterior | `#dashboard-prev` | Clase: `dashboard-week-arrow`. Símbolo: ◀. title: "Semana anterior" |
| Rango de fechas | `#dashboard-week-range` | Clase: `dashboard-week-range dashboard-week-range--clickable`. title: "Seleccionar semana" |
| Flecha siguiente | `#dashboard-next` | Clase: `dashboard-week-arrow`. Símbolo: ▶. title: "Semana siguiente" |
| Badge "En curso" | `#dashboard-semana-en-curso` | Clase: `dashboard-week-status hidden`. Visible solo si es semana actual |
| Input fecha (oculto) | `#dashboard-week-start` | `type="date"`, `aria-hidden="true"`, `tabindex="-1"` |
| Grid KPIs | `#dashboard-kpis` | Clase: `kpi-grid`. 4 columnas (forzado en JS) |
| Bloque días | `#dashboard-days-wrap` | `.card`. Contiene h3 "Días de la semana", tabla y cards (móvil) |
| Tabla días | `#dashboard-days-table-wrap` | Contenedor de `<table class="dashboard-table">` |
| Cards días (móvil) | `#dashboard-days-cards-wrap` | Clase: `dashboard-days-cards-wrap` |
| Bloque resumen | `#dashboard-resumen` | `.card`. Título "Resumen", clasificación y texto |
| Bloque importar | `.dashboard-import-card` | h3 "Importar datos", Excel y PDF |
| Input Excel | `#dashboard-excel-file` | `accept=".xlsx,.xls"`, `multiple` |
| Botón Cargar Excel | `#dashboard-import-excel` | Clase: `btn-secondary btn-sm` |
| Estado Excel | `#dashboard-excel-status` | Clase: `dashboard-import-status` |
| Input PDF | `#dashboard-pdf-file` | `accept=".pdf,application/pdf"` |
| Botón Cargar PDF | `#dashboard-import-pdf` | Clase: `btn-secondary btn-sm` |
| Estado PDF | `#dashboard-pdf-status` | Clase: `dashboard-import-status` |
| Bloque gráfico 30d | `#dashboard-bloque-ampliacion` | h3 "Facturación últimos 30 días" |
| Contenedor gráfico | `#dashboard-chart-30d` | Clase: `dashboard-chart-30d` |

---

## 3. Botones y acciones (uno por uno)

### 3.1 Botón "Actualizar" (`#dashboard-cargar`)

- **Función:** Llama a `load()` → recarga datos de la API y re-renderiza KPIs, tabla, resumen y gráfico.
- **Estado:** Siempre habilitado (no se deshabilita durante la carga).
- **Problema:** Durante "Cargando…" el usuario puede pulsar de nuevo; no hay debounce. **Recomendación:** deshabilitar mientras `load()` está en curso o ignorar clics duplicados.

### 3.2 Flecha "Semana anterior" (`#dashboard-prev`)

- **Función:** Resta 7 días a `weekStart`, actualiza el input y el texto del rango, llama a `load()`.
- **Accesibilidad:** `title="Semana anterior"`. No tiene `aria-label` (recomendable añadir).

### 3.3 Rango de fechas clicable (`#dashboard-week-range`)

- **Función:** Al hacer clic intenta `weekInput.showPicker()` (o `weekInput.click()`). Al cambiar el input se normaliza a lunes y se llama a `load()`.
- **Problema:** El input está oculto y sin `aria-label`; el rango no tiene rol de "button" ni relación explícita con el input para lectores de pantalla. **Recomendación:** `aria-label="Seleccionar lunes de la semana"` en el span o en el input.

### 3.4 Flecha "Semana siguiente" (`#dashboard-next`)

- **Función:** Suma 7 días, actualiza valor y llama a `load()`.
- **Accesibilidad:** Igual que prev: tener `aria-label` además de title.

### 3.5 Botón "Cargar Excel (uno o varios)" (`#dashboard-import-excel`)

- **Función:** Dispara `excelFile.click()`. El `change` del input envía cada archivo a `POST /api/import/excel?weekStart=...`.
- **Estado:** Se deshabilita durante el envío (`excelBtn.disabled = true`) y se restaura al terminar (éxito o error). Correcto.
- **Mensajes:** Muestra "Enviando 1/N…" y al final un resumen (días creados, actualizados, turnos; errores si los hay).

### 3.6 Botón "Cargar PDF" (`#dashboard-import-pdf`)

- **Función:** Dispara `pdfFile.click()`. El `change` envía el archivo a `POST /api/import/cuadrante-pdf?weekStart=...`.
- **Estado:** Se deshabilita durante el envío y se restaura en `finally`. Correcto.
- **Mensajes:** "Enviando…" y luego mensaje del servidor o error.

### 3.7 Botón "+ Evento" (solo en cards móvil)

- **Ubicación:** Dentro de cada `.dashboard-day-card`, en `#dashboard-days-cards-wrap`.
- **Función:** `prompt` para nombre, impacto y descripción; luego `POST /api/events` con `{ date, name, impact, description }`. Tras éxito llama a `load()`.
- **Problema:** En la **tabla de escritorio** no hay botón "+ Evento"; solo enlaces "Registro" y "Feedback". Los eventos se muestran en la columna Tendencia pero no se pueden crear desde la tabla. **Inconsistencia:** misma acción solo disponible en vista móvil.

### 3.8 Enlaces "Registro" y "Feedback" (tabla y cards)

- **Tabla:** `<a href="#registro?date=...">Registro</a>` y `<a href="#preguntas?date=...">Feedback</a>`.
- **Cards:** `<a href="#registro?date=...">Abrir registro</a>` y `<a href="#preguntas?date=...">Abrir feedback</a>`.
- **Comportamiento:** Navegación por hash; correcto.

---

## 4. Bloques de información y datos

### 4.1 KPIs (4 tarjetas)

| Orden | Label (frontend) | Origen de datos (API) | Subtextos mostrados |
|-------|------------------|------------------------|---------------------|
| 1 | Facturación total | `data.totalRevenue` | % vs sem. ant., % vs fact. objetivo |
| 2 | Productividad media | `data.avgProductivity` | % vs sem. ant., % vs prod. objetivo |
| 3 | Horas totales | `data.totalHours` | Histórico: `data.avgHoursHistoric` |
| 4 | Coste personal | `data.costePersonalEur` | % vs facturación, vs histórico, Contrato (€) |

- **Valores por defecto:** Si no hay dato se muestra "—".
- **Objetivos:** Facturación objetivo desde `data.facturacionObjetivo` o `data.FacturacionObjetivo`; productividad desde `data.productividadObjetivo` o `data.ProductividadObjetivo` o `data.productividadIdealEurHora`. El backend envía camelCase; el frontend contempla PascalCase por compatibilidad.
- **Estilos subtexto:** `.kpi-card-sub--up` (verde), `.kpi-card-sub--down` (rojo), `.kpi-card-sub--coste-bueno/asumible/alto`, `.kpi-card-sub--muted`.

### 4.2 Tabla "Días de la semana"

**Columnas:** Día | Fecha | Clima | Facturación | Horas | Productividad (€/h) | Personal | Tendencia | Acciones

- **Día:** `d.dayName` o `dayNameFromDate(d.date)`.
- **Fecha:** `formatDateShort(d.date)` (dd/mm).
- **Clima:** `weatherText(d)` → emoji + temp max/min °C + precip mm + viento km/h. Códigos WMO mapeados a emoji (0=☀️, 1-3=⛅, 45/48=🌫️, etc.).
- **Facturación:** `d.revenue` en € o "—".
- **Horas:** `d.effectiveHours ?? d.hoursWorked`, una decimal.
- **Productividad:** `d.effectiveProductivity ?? d.productivity`.
- **Personal:** Si hay `staffSummarySala` y `staffSummaryCocina`: "Sala: X | Cocina: Y" + horas cuadrante o calculadas; si no, `staffTotal`.
- **Tendencia:** vs media histórica (con % "hoy"), `trendLabel`, `trendVsPrevWeek`, y debajo eventos del día (nombre + impacto, hasta 2, "+N más").
- **Acciones:** Enlaces Registro y Feedback.

**Tooltips en cabecera:**  
- Personal: texto largo sobre Sala/Cocina, PDF vs manual, horas.  
- Tendencia: explicación de "vs media", "hoy ±%", tendencia 12 sem., vs sem. ant.

**Estado vacío:** Si `days.length === 0` se muestra mensaje con enlace a `#registro` ("Registro de ejecución") y hint sobre semana actual y Actualizar. El enlace está implementado (`.dashboard-link-registro`).

### 4.3 Cards (vista móvil)

- Se muestran solo en `@media (max-width: 767px)` (tabla oculta, cards visibles).
- Cada card incluye: día, fecha, KPIs (Facturación, Horas, Prod.), Clima, Personal, Horas equipo (si aplica), Tendencia, botones Abrir registro / Abrir feedback, "+ Evento", y bloque de eventos si existen.
- El botón "+ Evento" hace binding con `data-date` y llama a la API de eventos.

### 4.4 Bloque Resumen

- **Título:** "Resumen".
- **Clasificación:** `data.resumenClasificacion` (🟢 Semana buena / 🟡 Semana normal / 🔴 Semana baja).
- **Texto:** `data.resumenTexto` o "Sin datos para esta semana.".
- El backend construye el texto con facturación total, horas, productividad y comparativas (sem. ant., media 12 sem.).

### 4.5 Bloque "Importar datos"

- **Labels:**  
  - Excel: "Excel (facturación + horas reales por turno). Puede elegir varios archivos (Ctrl+clic)."  
  - PDF: "PDF cuadrante (personal y horas programadas por turno)"
- **Estados:** `.dashboard-import-status.success` (verde), `.dashboard-import-status.error` (rojo).
- **Excel:** Envía archivos en secuencia; acumula creados/actualizados/turnos y errores; al final muestra resumen y hasta 5 errores (o "+N más").
- **PDF:** Un solo archivo; mensaje del servidor o lista de errores (máx. 3 + "+N más").

### 4.6 Gráfico "Facturación últimos 30 días"

- **Datos:** `data.last30Days` (array de `{ date, revenue }`). Si viene vacío desde el backend, se rellenan 30 días con revenue 0 desde (hoy - 29).
- **Escala:** Máximo = max(revenue) * 1.25; etiqueta "Escala: 0 – X € · Media 30d: Y €".
- **Barras:** 30 divs con altura en % del máximo; fecha corta (dd/mm), nombre del día; tooltip con fecha y €.
- **Estilos:** `.dashboard-chart-bar-wrap` con variantes `is-weekend`, `week-alt`, `week-boundary`. Fin de semana = viernes, sábado, domingo (código: `getDay() === 0 || 5 || 6`).
- **Estado vacío:** Si `rawItems.length === 0`: mensaje "Sin datos para los últimos 30 días" y enlace a `#configuracion` para datos de muestra.

---

## 5. APIs utilizadas

| Método y ruta | Uso en Dashboard |
|---------------|-------------------|
| `GET /api/dashboard/week?weekStart=yyyy-MM-dd&asOf=yyyy-MM-dd` | Carga principal: KPIs, días, resumen, last30Days. |
| `GET /api/events?from=...&to=...` | Eventos del rango de la semana; se mezclan por fecha en la columna Tendencia y en las cards. |
| `POST /api/import/excel?weekStart=...` | Importación Excel (FormData con `file`). |
| `POST /api/import/cuadrante-pdf?weekStart=...` | Importación PDF. |
| `POST /api/events` | Crear evento desde el botón "+ Evento" (solo cards). |

**No usado en esta vista:** `GET /api/dashboard/daily-revenue?days=30`. El gráfico 30d usa `last30Days` incluido en la respuesta de `GET week`.

---

## 6. Backend (DashboardController y DTOs)

### 6.1 GET /api/dashboard/week

- **Parámetros:** `weekStart` (opcional), `asOf` (opcional). Si no se pasan, se usa hoy y se normaliza a lunes.
- **Lógica:** Semana = 7 días desde el lunes; solo se cuentan días hasta `asOf` para totales (semana en curso).
- **Cálculos:** TotalRevenue, TotalHours, AvgProductivity (con effectiveHours si aplica), semana anterior (mismo nº de días), históricos 12 semanas (avgRevenueHistoric, avgProductivityHistoric, avgHoursHistoric), tendencia por día de la semana (mitad reciente vs antigua), coste personal (CostePersonalPorHora, HorasSemanalesContrato), objetivos (FacturacionObjetivoSemanal, ProductividadIdealEurHora), last30Days.
- **Respuesta:** `DashboardWeekResponse` con todos los campos usados por el frontend; días como `DashboardDayItemDto` con dayName, revenue, effectiveHours, effectiveProductivity, staff, clima, tendencias, etc.

### 6.2 DTOs

- **DashboardDayItemDto:** Incluye dayName, revenue, hoursWorked, productivity, staffSummarySala/Cocina, calculatedStaffHours, plannedHoursFromPdf/Breakdown, effectiveHours, effectiveProductivity, avgRevenueHistoric, pctVsAvgHistoric, trendLabel, trendVsPrevWeek, campos weather.
- **DashboardWeekResponse:** totalRevenue, avgProductivity, totalHours, avgStaff, avgRevenueHistoric, costePersonalEur, costePersonalPctFacturacion, facturacionObjetivo, productividadObjetivo, resumenClasificacion, resumenTexto, days, last30Days, etc.
- **DailyRevenueItemDto:** date, revenue (usado en last30Days).

### 6.3 Posibles mejoras backend

- Normalizar `weekStart` a lunes si el cliente envía otro día (hoy ya se hace con `GetMonday` en el controller).
- `GET /api/dashboard/daily-revenue` está implementado pero no se usa en el dashboard; podría usarse para no incluir last30Days en GET week y reducir payload, o documentar que es alternativo.

---

## 7. Estilos (CSS) — elementos del dashboard

- **Pantalla:** `#screen-dashboard` flex column, min-height 100vh.
- **Header:** `.dashboard-header` (logo, nav, user-role, logout). Logo con `transform: scale(3)`.
- **Contenido:** `.dashboard-content` flex:1, padding 1.5rem, overflow auto.
- **Barra semana:** `.dashboard-week-bar` flex wrap; flechas, rango, badge; input oculto.
- **KPIs:** `.kpi-grid` 4 columnas (JS), en 900px → 3, en 500px → 2.
- **Tabla:** `.dashboard-table` con th/td, action-link, weather, events-inline.
- **Importación:** `.dashboard-import-card`, `.dashboard-import-row`, inputs ocultos, status success/error.
- **Gráfico:** `.dashboard-chart-30d`, `.dashboard-chart-bars`, `.dashboard-chart-bar-wrap`, `.dashboard-chart-bar`, labels.
- **Cards móvil:** `.dashboard-days-cards-wrap` oculto por defecto; en max-width 767px la tabla se oculta y las cards se muestran.
- **Badge:** `.dashboard-week-status::before { content: '✓\00a0'; }`, `.dashboard-badge--current` en verde.

---

## 8. Flujos y errores

### 8.1 Carga inicial y 401

- Al cargar la vista se llama a `load()`. Si `GET week` devuelve 401, se hace `global.LUCAS_APP.onUnauthorized()` y se sale (return). Correcto.
- Si `GET events` devuelve 401 se devuelve null; 403 → []. No se redirige a login desde events (solo desde week). Aceptable si el dashboard sigue mostrando datos sin eventos.

### 8.2 Errores de red / 500

- En el `catch` de `load()`: se restaura el texto del rango, se quita loading, se muestra "Error al cargar." (o err.message) en `#dashboard-kpis` y mensaje genérico en el gráfico. No hay botón "Reintentar" explícito (el usuario puede pulsar Actualizar).

### 8.3 Importación Excel

- Si un archivo falla se añade a `allErrors` y se continúa con el siguiente. Al final se muestra el resumen y hasta 5 errores. Correcto.
- Si la respuesta no es JSON (ej. 500 HTML), el catch añade "Error al enviar" por archivo.

### 8.4 Importación PDF

- Un solo archivo. Si `!r.ok` se lanza Error con mensaje del servidor o genérico. En catch se muestra "Error al enviar: {detail}". En finally se rehabilita el botón.

---

## 9. Accesibilidad

| Elemento | Estado | Recomendación |
|----------|--------|----------------|
| Botón Actualizar | Sin aria-label | Añadir `aria-label="Actualizar datos de la semana"` |
| Flechas ◀ ▶ | Solo title | Añadir `aria-label="Semana anterior"` / "Semana siguiente" |
| Rango de fechas | Click abre input oculto | Relacionar con input (aria-describedby o label oculto); input con `aria-label="Lunes de la semana"` |
| Input week-start | aria-hidden, tabindex -1 | Si se usa para picker, dar `aria-label` y no ocultar para AT cuando se abre el picker (o anunciar cambio de semana) |
| Tabla días | th con scope | Correcto; tooltips en Personal y Tendencia ayudan |
| Mensajes de estado (Excel/PDF) | Solo texto | Considerar `role="status"` o `aria-live="polite"` para anuncios |
| Gráfico 30d | Barras con title | No hay descripción de conjunto; añadir `aria-label` al contenedor del gráfico |

---

## 10. Checklist de elementos revisados

- [x] Título y subtítulo
- [x] Botón Actualizar
- [x] Flecha anterior / siguiente
- [x] Rango de fechas (click → date picker)
- [x] Badge "En curso"
- [x] Input fecha oculto
- [x] 4 KPIs (facturación, productividad, horas, coste personal) y todos sus subtextos
- [x] Tabla de días (9 columnas, tooltips, estado vacío, enlaces)
- [x] Cards móvil (mismo contenido + botón "+ Evento")
- [x] Bloque Resumen (clasificación + texto)
- [x] Bloque Importar (Excel y PDF: labels, inputs, botones, estados)
- [x] Gráfico 30 días (escala, barras, fines de semana, vacío)
- [x] APIs (week, events, import excel/pdf, POST events)
- [x] Backend (week, DTOs, last30Days)
- [x] Estilos y responsive (tabla vs cards)
- [x] Manejo 401 y errores
- [x] Accesibilidad (resumen y recomendaciones)

---

## 11. Hallazgos prioritarios

1. **Botón "+ Evento" solo en cards:** En escritorio no se puede crear evento desde la tabla; solo verlos en Tendencia. Valorar añadir enlace o botón "Añadir evento" por fila en la tabla.
2. **Actualizar sin deshabilitar:** Evitar doble clic o deshabilitar botón mientras `load()` corre.
3. **Accesibilidad:** Añadir aria-labels a botones de semana, al input de fecha y al contenedor del gráfico.
4. **Endpoint daily-revenue:** No usado; documentar o usar para aligerar GET week.
5. **Consistencia frontend:** Mantener alineados `lucas-web-app/js/views/dashboard.js` y `LucasWeb.Api/wwwroot/js/views/dashboard.js` (o un solo origen de verdad).

---

*Auditoría realizada sobre: dashboard.js (wwwroot), index.html, app.js, DashboardController.cs, DashboardDtos.cs, styles.css, y flujos de importación y eventos.*
