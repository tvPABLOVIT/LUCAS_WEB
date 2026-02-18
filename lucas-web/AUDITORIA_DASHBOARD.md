# Auditoría profunda — Pestaña Dashboard

**Fecha:** Febrero 2026  
**Alcance:** Backend (API, lógica), frontend (vista, UX), documentación y consistencia.

---

## 1. Resumen ejecutivo

El Dashboard cumple su función básica: selector de semana, KPIs (facturación, productividad, horas, personal, semana anterior), bloque de importación Excel/PDF, resumen clasificado (🟢/🟡/🔴) y tabla de días. Hay **gaps frente a la documentación** (avgRevenueHistoric, tendencia por día, coste personal), **oportunidades de UX** (feedback de carga, vacíos, accesibilidad) y **pequeñas robusteces** (manejo de fechas, 401). A continuación se detallan hallazgos y mejoras propuestas priorizadas.

---

## 2. Backend (API y lógica)

### 2.1 Lo que está bien

- **GET /api/dashboard/week** con `weekStart` y `asOf`: correcto. Solo se suman los días de la semana hasta `asOf`, lo que permite ver la “semana en curso” con datos parciales.
- Cálculo de **TotalRevenue, TotalHours, AvgProductivity, AvgStaff** para el rango efectivo.
- **Semana anterior** (prevStart/prevEnd) alineada con el mismo número de días que la semana seleccionada hasta `asOf`.
- **Resumen clasificado** por productividad (>&gt;80 🟢, &gt;50 🟡, resto 🔴).
- **DTOs** con `JsonPropertyName` en camelCase para el frontend.

### 2.2 Gaps e incoherencias

| Hallazgo | Detalle | Doc de referencia |
|----------|---------|--------------------|
| **avgRevenueHistoric no calculado** | El DTO tiene `avgRevenueHistoric` pero el controlador nunca lo asigna. La documentación (04_APIS, 09_ESPECIFICACION_PANTALLAS) indica “Facturación promedio semanal” histórico. | documentacion/04, 09; 03_REGLAS (KPIs Dashboard) |
| **Sin tendencia por día** | Doc 03: “Comparativa por día: comparar TotalRevenue del día con media histórica del mismo día de la semana; si &gt;5% al alza → ↑ Al alza; &gt;5% a la baja → ↓ A la baja; si no → Estable”. No implementado. | documentacion/03_REGLAS_DE_NEGOCIO_Y_FORMULAS.md |
| **Sin dayName en días** | El DTO `DashboardDayItemDto` no incluye `dayName`; el frontend lo deriva con `dayNameFromDate(d.date)`. Funciona pero la API documentada (04) devuelve `dayName` por día. | documentacion/04_APIS_Y_ENDPOINTS.md |
| **Sin “coste personal vs facturación”** | Doc 09: Dashboard debería mostrar “% coste personal vs facturación (y opcionalmente coste en €)”. No hay lectura de `CostePersonalPorHora` ni cálculo en el endpoint. | documentacion/09_ESPECIFICACION_PANTALLAS_WEB.md |

### 2.3 Robustez

- **weekStart inválido:** si `weekStart` no es fecha, se usa “hoy” y se calcula el lunes. Correcto.
- **Semana sin datos:** se devuelve totalRevenue=0, avgProductivity=null, days=[]. El frontend maneja “—” y mensaje de “sin datos”. Correcto.
- **Normalización de lunes:** el backend no fuerza que `weekStart` sea lunes; si el cliente envía miércoles, se toman 7 días desde ese día. Sería más seguro normalizar en backend a “lunes de esa semana” para consistencia (opcional).

---

## 3. Frontend (vista Dashboard)

### 3.1 Estructura y flujo

- **Selector de semana:** ◀, input date, ▶, “Cargar”. El rango de fechas se muestra como texto (ej. “03 feb – 09 feb 2026”) y el badge “✓ En curso” para la semana actual. Correcto.
- **Importación:** dos bloques (Excel y PDF) con input file oculto, botón que dispara el file input, y span de estado. Tras importar, se llama a `load()` para refrescar. Correcto.
- **KPIs:** 6 tarjetas (Facturación total, Productividad media, Horas totales, Personal medio, Semana anterior fact., Semana anterior prod.) con subtexto “vs sem. ant.” en facturación y productividad. Correcto.
- **Resumen:** título “Resumen”, clasificación (emoji + texto) y párrafo con totales. Correcto.
- **Tabla de días:** columnas Día, Fecha, Facturación, Horas, Productividad (€/h), Personal. Si no hay días, mensaje con enlace conceptual a “Registro de ejecución”. Correcto.

### 3.2 UX y usabilidad

| Hallazgo | Severidad | Propuesta |
|----------|-----------|-----------|
| **Botón “Cargar” ambiguo** | Media | El botón “Cargar” está al lado del selector de semana; puede confundirse con “Cargar datos de la semana” vs “Cargar Excel”. Renombrar a “Actualizar” o “Ver semana” y dejar “Cargar Excel” / “Cargar PDF” solo en importación. |
| **Sin indicador de carga en selector** | Baja | Al cambiar semana (◀/▶ o input) solo el bloque de KPIs muestra “Cargando…”; la barra de semana no indica estado. Añadir un pequeño spinner o estado “Cargando…” junto al rango de fechas. |
| **Importación sin deshabilitar botón** | Baja | Durante “Enviando…” el usuario puede volver a pulsar “Cargar Excel”/“Cargar PDF”. Deshabilitar el botón hasta que termine la petición. |
| **Estado de error genérico** | Media | En catch se muestra “Error al cargar” o err.message; en 403/500 no se distingue. Mostrar mensaje según código (ej. “Sesión expirada” en 401) y opción de reintentar. |
| **Tabla de días sin enlace al registro** | Baja | El texto “Puedes añadirlos desde **Registro de ejecución**” no es clicable. Añadir enlace que ponga `window.location.hash = 'registro'` para ir a la pestaña Registro. |
| **Número de semana no mostrado** | Baja | La doc menciona “número de semana” en cabecera. Añadir ISO week (ej. “Semana 6”) junto al rango de fechas puede ayudar en contextos multi-sede. |

### 3.3 Accesibilidad y responsive

- **Contraste:** uso de `--text-muted` y `--success`/`--error` en KPIs; razonable. Revisar contraste del badge “En curso” en el tema oscuro.
- **Labels:** el `<input type="date">` no tiene `<label>` asociado por id; el “Semana” está en un span. Añadir `aria-label="Lunes de la semana"` al input.
- **Focus:** botones y enlaces reciben focus; no hay trampa de teclado. Aceptable.
- **Responsive:** `.kpi-grid` pasa a 3 y 2 columnas en breakpoints 900px y 500px; `.dashboard-import-row` hace wrap. La tabla de días puede hacer scroll horizontal en móvil; considerar `overflow-x: auto` en el contenedor si no está.

### 3.4 Consistencia de código

- **Duplicación:** `lucas-web-app/js/views/dashboard.js` y `LucasWeb.Api/wwwroot/js/views/dashboard.js` deben mantenerse iguales. Hoy están alineados; conviene un único origen (p. ej. build que copie desde lucas-web-app a wwwroot) para no divergir.
- **Fechas:** uso de `d + 'T12:00:00'` para evitar desfases por zona; correcto. `getWeekStart` con `day === 0 ? -6 : 1` para lunes como primer día; correcto.

### 3.5 Manejo de 401

- Si la API devuelve 401, `fetchWithAuth` devuelve la respuesta y el código hace `if (r.status === 401) return null;` y no llama a `onUnauthorized`. Depende de si `auth.js` redirige a login en 401 a nivel global; si no, el usuario puede quedarse en pantalla “vacía” o con “Error al cargar”. Asegurar que en 401 se llame a `LUCAS_APP.onUnauthorized()` o equivalente para redirigir a login.

---

## 4. Documentación vs implementación

| Documento | Qué dice | Estado |
|-----------|----------|--------|
| 04_APIS | Dashboard week con avgRevenueHistoric, y por día: dayName, avgRevenueHistoric, trendLabel, context | Parcial: faltan avgRevenueHistoric, trendLabel, context, dayName en API |
| 09_ESPECIFICACION_PANTALLAS | KPIs: facturación promedio, productividad, horas, **% coste personal** | Coste personal no implementado |
| 03_REGLAS | Comparativa por día vs histórico (↑ Al alza / ↓ A la baja / → Estable) | No implementado |
| IMPORTACION_EXCEL_Y_PDF | Dashboard con Cargar Excel y Cargar PDF | Implementado |

---

## 5. Mejoras propuestas (priorizadas)

### Prioridad alta (alinear con doc y UX básica)

1. **Backend: calcular y devolver avgRevenueHistoric**  
   Media de facturación semanal histórica (solo semanas “completas”, p. ej. ≥5 días con datos). Incluir en `DashboardWeekResponse` y, si se quiere, en cada `DashboardDayItemDto` la media histórica de ese día de la semana.

2. **Backend: tendencia por día (trendLabel)**  
   Para cada día con datos, comparar su TotalRevenue con la media histórica del mismo día de la semana (mismo DayOfWeek). Asignar “↑ Al alza” si &gt;5% por encima, “↓ A la baja” si &gt;5% por debajo, “→ Estable” en caso contrario. Incluir `trendLabel` (y opcionalmente `dayName`) en el DTO de cada día.

3. **Frontend: 401 → redirigir a login**  
   En la petición de dashboard, si `r.status === 401`, llamar a `global.LUCAS_APP.onUnauthorized()` (o la función que muestre pantalla de login y limpie token) antes de salir, para no dejar la vista a medias.

4. **Frontend: botón “Cargar” → “Actualizar”**  
   Renombrar el botón junto al selector de semana a “Actualizar” (o “Ver semana”) para no confundir con “Cargar Excel”.

### Prioridad media (valor y claridad)

5. **Backend: KPI coste personal**  
   Leer setting `CostePersonalPorHora` (o `CostoPersonalPorHora` según modelo). Calcular coste total = TotalHours × coste/hora y % = (coste / TotalRevenue)×100. Añadir al response `costePersonalEur` y `costePersonalPctFacturacion` (nullable si no hay setting). Frontend: séptima tarjeta KPI “Coste personal” con valor en € y “X% vs facturación”.

6. **Frontend: enlace “Registro de ejecución”**  
   En el mensaje “Aún no hay días registrados… Puedes añadirlos desde **Registro de ejecución**”, hacer que “Registro de ejecución” sea un enlace que ponga `location.hash = 'registro'`.

7. **Frontend: deshabilitar botones de importación mientras “Enviando…”**  
   Deshabilitar “Cargar Excel” y “Cargar PDF” durante la petición y volver a habilitar al terminar (éxito o error).

8. **Backend: incluir dayName en DashboardDayItemDto**  
   En el mapeo de días, asignar el nombre del día (Lunes, Martes, …) para no depender solo del frontend y alinear con la API documentada.

### Prioridad baja (pulido)

9. **Frontend: indicador de carga en la barra de semana**  
   Mostrar “Cargando…” o un spinner pequeño junto a `dashboard-week-range` mientras corre `load()`.

10. **Frontend: número de semana ISO**  
    Calcular semana del año (ISO) a partir de `weekStart` y mostrarla, ej. “Semana 6 · 03 feb – 09 feb 2026”.

11. **Accesibilidad: aria-label en input fecha**  
    Añadir `aria-label="Lunes de la semana"` al input `#dashboard-week-start`.

12. **Backend: normalizar weekStart a lunes**  
    Si el query `weekStart` no es lunes, convertir a lunes de esa semana antes de calcular, para que el resultado sea siempre “lun–dom”.

---

## 6. Checklist de implementación sugerido

- [ ] Backend: avgRevenueHistoric en GET week (y opcional por día).
- [ ] Backend: trendLabel por día (↑ Al alza / ↓ A la baja / → Estable).
- [ ] Backend: dayName en cada día del response.
- [ ] Backend: coste personal (CostePersonalPorHora) → costePersonalEur, costePersonalPctFacturacion.
- [ ] Frontend: 401 → onUnauthorized().
- [ ] Frontend: botón “Cargar” → “Actualizar”.
- [ ] Frontend: enlace a #registro en mensaje “sin días”.
- [ ] Frontend: deshabilitar botones Excel/PDF durante envío.
- [ ] Frontend: tarjeta KPI “Coste personal” (si backend la envía).
- [ ] Opcional: spinner en barra de semana, semana ISO, aria-label, normalizar weekStart a lunes.

---

*Documento generado a partir de revisión de DashboardController, DashboardDtos, dashboard.js (lucas-web-app y wwwroot), documentacion/ y lucas-web/.*
