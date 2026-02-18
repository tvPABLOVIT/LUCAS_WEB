# Análisis del bloque "Qué puede afectar la semana siguiente"

Documento que describe **qué se muestra** en este bloque, **por qué se muestra**, **cómo se obtiene la información** y **en base a qué criterios** se construye cada alerta. La lógica está en `InteligenciaService.GetAlertasDiagramacionAsync` y la vista en la pestaña Estimaciones (RecomendacionesView).

---

## 1. Ubicación y propósito del bloque

- **Dónde:** En la pestaña **Estimaciones** (RecomendacionesView), debajo de las tarjetas de días de la semana siguiente. Título: **"Qué puede afectar la semana siguiente"** y subtítulo: "Tendencia, clima, festivos, semana anterior y misma semana del mes anterior."
- **Qué es:** Una lista de **alertas** (tarjetas con borde amarillo/naranja) que dan **contexto ejecutivo** para planificar la semana siguiente: datos reales de la semana pasada, comparación con la estimación, clima, festivos, misma semana del mes anterior, eventos y obras.
- **Por qué existe:** Resumir en un solo sitio los factores que pueden influir en el resultado (facturación, afluencia, horarios) sin duplicar el detalle que ya está en el párrafo de estimación o en las tarjetas por día. El usuario puede ver de un vistazo "qué puede afectar" antes de tomar decisiones de personal u oferta.

---

## 2. Origen de los datos: cuándo y cómo se rellenan

- **Origen:** El bloque se rellena con el resultado de **GetAlertasDiagramacionAsync(nextWeek)**.
- **Cuándo:** Al cargar la pestaña Estimaciones, el ViewModel llama en paralelo a `GetNextWeekDiagramacionAsync()` y luego a `GetAlertasDiagramacionAsync(nextWeek)`. Es decir, **siempre** se piden las alertas en base a la **diagramación de la semana siguiente** ya calculada (predicción guardada o calculada en vivo).
- **Dependencia:** El método recibe **NextWeekDiagramacion** (nextWeek). De ahí salen: rango de la semana siguiente (WeekStart, WeekEnd), **PredictedRevenue** (total estimado) y **Days** (cada día con WeatherDescription, IsHoliday, HolidayName). Si nextWeek es null, se usan valores por defecto para fechas y revenue 0.
- **Presentación en la UI:** Cada alerta es un **AlertaDiagramacionItem** con tres campos: **Tipo**, **Title**, **Description**. En la vista se muestran en un `ItemsControl` vinculado a **AlertasGenerales**. Las tarjetas con **Description** vacía se ocultan (Visibility = Collapsed). El orden en pantalla es el mismo en que se añaden al listado (ver abajo).

---

## 3. Qué se muestra: las 7 alertas (en orden)

El método **GetAlertasDiagramacionAsync** añade las alertas en un orden fijo. No todas tienen contenido visible siempre: Festivos puede tener Description vacía; Eventos y Obras solo se añaden si hay datos.

| # | Tipo (interno) | Title (título de la tarjeta) | Descripción breve de lo que se muestra |
|---|----------------|------------------------------|----------------------------------------|
| 1 | Semana anterior | Semana anterior | Datos reales de la semana pasada: facturación, horas trabajadas, productividad €/h. |
| 2 | Tendencia | Facturación al alza / a la baja / estable | Comparación: estimación semana siguiente vs facturación real semana anterior (%). |
| 3 | Clima | Clima semana siguiente | Días con lluvia prevista en la semana siguiente, o "Sin días de lluvia previstos.". |
| 4 | Festivos | Festivos | Nombres de festivos y fechas de la semana siguiente, o cadena vacía (tarjeta oculta). |
| 5 | Misma semana, mes anterior | Misma semana, mes anterior | Comparación con las mismas fechas numéricas del mes pasado y facturación de entonces. |
| 6 | Eventos | Eventos esta semana | Solo si hay eventos: BD + Open Data BCN + GuiaBCN (lista con nombre, fecha, origen). |
| 7 | Obras | Obras cerca | Solo si hay obras: Open Data BCN en 300 m (nombre y fecha si existe). |

A continuación se detalla **qué se muestra exactamente**, **por qué**, **cómo se obtiene la información** y **en base a qué reglas** para cada una.

---

## 4. Detalle por alerta

### 4.1. Semana anterior

- **Qué se muestra:** Un único texto que puede ser:
  - Si hay datos (facturación > 0 o horas > 0):  
    *"Semana del {dd/MM} al {dd/MM}: {X} € facturados, {Y} h trabajadas, {Z} €/h de productividad."*
  - Si no hay datos:  
    *"No hay datos de la semana anterior ({dd/MM} al {dd/MM})."*

- **Por qué se muestra:** Dar el **referente real** más reciente (semana ya cerrada) antes de ver la tendencia. Así el usuario sabe cuánto se facturó y cuántas horas se hicieron la semana pasada.

- **Cómo se obtiene la información:**
  - **Semana anterior** = lunes a domingo de la semana pasada. Se calcula: `thisMonday = GetWeekStartMonday(DateTime.Today)` (lunes de la semana actual), `previousWeekMonday = thisMonday.AddDays(-7)`, `previousWeekEnd = previousWeekMonday.AddDays(6)`.
  - Se consulta la BD: `ExecutionDays` con `Date >= previousWeekMonday` y `Date <= previousWeekMonday.AddDays(6)`, seleccionando `TotalRevenue` y `TotalHoursWorked`.
  - **previousWeekRevenue** = suma de TotalRevenue; **previousWeekHours** = suma de TotalHoursWorked.
  - **Productividad** = previousWeekRevenue / previousWeekHours (si hours > 0), en €/h.

- **En base a qué:** Solo a los **datos registrados** en `ExecutionDays` para ese rango de fechas. No se usa predicción ni patrones; es puro histórico de la semana pasada.

---

### 4.2. Tendencia

- **Qué se muestra:** Un texto que puede ser:
  - Si hay datos de semana anterior y estimación semana siguiente:
    - **Al alza (pct > 1 %):** *"Esperamos facturar un {pct}% más que la semana anterior."*
    - **A la baja (pct < -1 %):** *"Esperamos facturar un {|pct|}% menos que la semana anterior."*
    - **Estable (entre -1 % y +1 %):** *"Esperamos facturar en línea con la semana anterior."*
  - Si no hay datos de semana anterior o no hay estimación: *"No hay datos de la semana anterior para comparar."*  
  El **Title** incluye la dirección: "Facturación al alza", "Facturación a la baja" o "Facturación estable".

- **Por qué se muestra:** Resumir si la **próxima semana** se espera mejor, peor o igual que la **última ya cerrada**, en términos de facturación. Ayuda a calibrar expectativas (personal, compras, etc.).

- **Cómo se obtiene la información:**
  - **nextWeekRevenue** = nextWeek.PredictedRevenue (total estimado de la semana siguiente; viene de la predicción guardada o calculada en vivo).
  - **previousWeekRevenue** = mismo que en la alerta "Semana anterior" (suma de TotalRevenue de ExecutionDays de la semana pasada).
  - **pct** = (nextWeekRevenue - previousWeekRevenue) / previousWeekRevenue * 100 (en double/decimal).
  - **Umbrales:** pct > 1 → "al alza"; pct < -1 → "a la baja"; en caso contrario → "estable".

- **En base a qué:** Comparación directa entre **estimación de la semana siguiente** (modelo de predicción) y **facturación real de la semana anterior** (BD). No se usan tendencias 4 vs 4 ni patrones; solo esos dos números.

---

### 4.3. Clima (semana siguiente)

- **Qué se muestra:** Un único texto que puede ser:
  - Si hay días con lluvia prevista: *"Lluvia prevista: {DayName} {dd/MM}, {DayName} {dd/MM}, ..."* (lista de días con lluvia).
  - Si no hay ninguno: *"Sin días de lluvia previstos para la semana siguiente."*

- **Por qué se muestra:** La lluvia suele afectar a la demanda (terraza, paseos). El usuario puede preparar personal u oferta en función de qué días se espera lluvia.

- **Cómo se obtiene la información:**
  - Se usan los **días de la semana siguiente** que ya vienen en **nextWeek.Days**. Cada día tiene **WeatherDescription** (texto descriptivo del tiempo, p. ej. "Lluvia ligera", "Despejado").
  - **Día con lluvia:** aquel cuya `WeatherDescription` contiene (sin distinguir mayúsculas) alguna de: "lluvia", "rain", "Llovizna", "drizzle".
  - **rainDays** = lista de días que cumplen eso. El texto se forma concatenando para cada uno `"{DayName} {Date:dd/MM}"`.

- **En base a qué:** Los datos de clima por día se obtienen **al construir la diagramación** (GetNextWeekDiagramacionAsync → EnrichDaysWithWeatherAndHolidayAsync), que llama a **ClimaService** (Open-Meteo) para cada día de la semana siguiente y rellena WeatherDescription (y WeatherCode, etc.) en cada NextWeekDayItem. Es decir, la alerta **no vuelve a llamar** al clima; solo **lee** lo que ya está en nextWeek.Days. El origen último es la **previsión meteorológica** (Open-Meteo) para las fechas de la semana siguiente.

---

### 4.4. Festivos

- **Qué se muestra:** Solo si hay al menos un día festivo en la semana siguiente con nombre de festivo no vacío:
  - *"{HolidayName} el {DayName} {dd/MM}. {HolidayName} el {DayName} {dd/MM}. ..."*
  Si no hay festivos o no tienen nombre, **Description** queda **vacía** y la tarjeta se **oculta** en la UI (DataTrigger por Description == "").

- **Por qué se muestra:** Los festivos cambian demanda y horarios; es útil ver qué días de la semana siguiente son festivos y cómo se llaman.

- **Cómo se obtiene la información:**
  - **festivosDays** = nextWeek.Days donde `IsHoliday == true` y `HolidayName` no es null ni vacío.
  - El texto es la concatenación de "{HolidayName} el {DayName} {Date:dd/MM}" para cada uno.
  - IsHoliday y HolidayName se rellenan en **EnrichDaysWithWeatherAndHolidayAsync**, que usa **FestivosService** (Nager + Nominatim para la comunidad autónoma) para cada fecha de la semana siguiente. De nuevo, la alerta **no llama** a FestivosService; solo **lee** nextWeek.Days.

- **En base a qué:** Calendario de festivos (Nager) y ubicación del restaurante (para saber comunidad/país). Los datos ya vienen en la diagramación.

---

### 4.5. Misma semana, mes anterior

- **Qué se muestra:** Un único texto que puede ser:
  - Si **no** hay datos del mes anterior para esas fechas:  
    *"No hay datos de las mismas fechas del mes anterior ({rango}) para comparar con la semana siguiente ({rango})."*
  - Si hay datos y hay estimación (nextWeekRevenue > 0):  
    *"Esperamos facturar {un X% más / un X% menos / en línea} que los mismos días del mes pasado. El mes pasado, para esas fechas ({rango}) facturaste {Y} €."*
  - Si hay datos pero no hay estimación:  
    *"El mes pasado, para esas fechas ({rango}) facturaste {Y} €."*

- **Por qué se muestra:** Estacionalidad "misma semana del mes": comparar la semana siguiente con **las mismas fechas numéricas** del mes pasado (ej. 3–9 feb vs 3–9 ene). Útil cuando no hay un año entero de datos para estacionalidad más fina.

- **Cómo se obtiene la información:**
  - **Rango semana siguiente:** nextWeekMonday, nextWeekSunday (de nextWeek o por defecto lunes de la semana que viene + 6 días).
  - **Rango mes anterior:** mismas fechas numéricas en el mes anterior. Se calcula con cuidado por si la semana cruza dos meses (ej. 28 ene – 3 feb):
    - prevMonthStart = nextWeekMonday.AddMonths(-1).
    - startDayPrev = min(nextWeekMonday.Day, último día del mes de prevMonthStart).
    - startDatePrevMonth = primer día del rango en el mes anterior.
    - Si semana siguiente está en un solo mes: endDatePrevMonth = mismo día numérico en prevMonthStart (respetando días del mes). Si la semana cruza dos meses: se usa el mes siguiente a prevMonthStart y se ajusta el día al máximo posible.
  - Se consulta la BD: **ExecutionDays** con `Date >= startDatePrevMonth` y `Date <= endDatePrevMonth`, sumando **TotalRevenue** → **prevMonthRevenue**.
  - **pctMesAnterior** = (nextWeekRevenue - prevMonthRevenue) / prevMonthRevenue * 100. MasMenos: > 1 % → "un X% más"; < -1 % → "un X% menos"; si no → "en línea".

- **En base a qué:** Histórico **real** de facturación (ExecutionDays) en las fechas del mes anterior que coinciden numéricamente con la semana siguiente, y **estimación** de la semana siguiente (PredictedRevenue). No se usan patrones ni clima aquí; solo suma de revenue real y comparación con la predicción.

---

### 4.6. Eventos esta semana

- **Qué se muestra:** Solo si hay al menos un evento: una lista de eventos con nombre, fecha (dd/MM) y, si no es "Medio", impacto. Origen indicado cuando aplica: "[Open Data BCN]", "[GuiaBCN]". Los de BD no llevan etiqueta de origen. Formato: *"{Nombre} ({dd/MM}, impacto {Alto/Bajo}). ..."*

- **Por qué se muestra:** Eventos (ferias, conciertos, mercados, etc.) pueden aumentar o reducir la afluencia. El usuario puede anticipar demanda o competencia de ocio.

- **Cómo se obtiene la información (tres fuentes, deduplicadas):**
  1. **Base de datos (tabla Events):** Eventos con `EventDate >= nextWeekMonday` y `EventDate <= nextWeekSunday`. Para cada uno se añade: "{Name} ({EventDate:dd/MM}" + si Impact != "Medio": ", impacto {Impact}" + ")".
  2. **Open Data BCN:** Si hay configuración de ubicación (lat/lon del restaurante o barrio/zona de interés), se llama a **_openDataBcn.GetEventosYObrasCercaAsync(lat, lon, 300, nextWeekMonday, nextWeekSunday, barrioInteres, locationContains, ct)**. Radio 300 m. Los eventos devueltos se añaden con " [Open Data BCN]".
  3. **GuiaBCN (scraper):** Si está disponible **_guiaBcnScraper**, se llama a **GetEventosAsync(nextWeekMonday, nextWeekSunday, ct)**. Cada evento se añade solo si no está ya en la lista (deduplicación por nombre normalizado + fecha). Se etiqueta " [GuiaBCN]".
  - **Deduplicación:** Se mantiene un HashSet de (Nombre normalizado, Fecha). Nombre normalizado = minúsculas, espacios múltiples colapsados. Así se evita mostrar el mismo evento dos veces si sale en BD y en Open Data BCN o GuiaBCN.

- **En base a qué:** Eventos internos (tabla Events), Open Data BCN (eventos en 300 m o en barrio/zona) y GuiaBCN (agenda). La alerta **solo se añade** si hay al menos un evento; si no, no aparece la tarjeta "Eventos esta semana".

---

### 4.7. Obras cerca

- **Qué se muestra:** Solo si Open Data BCN devuelve obras en espacio público dentro de 300 m: lista de nombres de obras y, si tienen fecha, (dd/MM). Formato: *"{Nombre} ({dd/MM}). ..."*

- **Por qué se muestra:** Obras cercanas pueden afectar acceso, ruido o imagen de la zona; útil para planificar o comunicar.

- **Cómo se obtiene la información:**
  - **Misma llamada** que para eventos: **GetEventosYObrasCercaAsync** devuelve **Eventos** y **Obras**. Las obras se leen de **openDataResult.Obras**.
  - Condiciones para llamar a Open Data BCN: tener (lat/lon del restaurante) o (barrio de interés o zona de interés configurada). Radio fijo 300 m para eventos y obras.
  - La alerta se añade **solo si** openDataResult != null y openDataResult.Obras.Count > 0.

- **En base a qué:** Open Data BCN (obras en espacio público a 300 m). No se usa BD ni GuiaBCN para obras.

---

## 5. Resumen: qué se muestra, por qué, cómo se obtiene y en base a qué

| Alertas | Qué se muestra | Por qué | Cómo se obtiene | En base a qué |
|--------|------------------|--------|------------------|----------------|
| **Semana anterior** | Facturación, horas y €/h de la semana pasada | Referente real más reciente | BD: ExecutionDays, suma TotalRevenue y TotalHoursWorked de la semana pasada | Solo histórico registrado |
| **Tendencia** | % más/menos/estable vs semana anterior | Expectativa vs última semana cerrada | nextWeek.PredictedRevenue vs previousWeekRevenue; pct y umbrales ±1 % | Predicción + facturación real semana anterior |
| **Clima** | Días con lluvia prevista o "sin lluvia" | Preparar personal/oferta por lluvia | nextWeek.Days: WeatherDescription contiene lluvia/rain/Llovizna/drizzle | Clima ya en diagramación (Open-Meteo) |
| **Festivos** | Nombres y fechas de festivos de la semana | Demandas y horarios distintos | nextWeek.Days: IsHoliday y HolidayName | Festivos ya en diagramación (Nager) |
| **Misma semana, mes anterior** | % más/menos vs mismas fechas mes pasado + facturación pasada | Estacionalidad “misma semana del mes” | BD: ExecutionDays en fechas numéricas del mes anterior; nextWeek.PredictedRevenue | Histórico real + predicción |
| **Eventos esta semana** | Lista de eventos con fecha e impacto | Afluencia y competencia de ocio | BD Events + Open Data BCN 300 m + GuiaBCN; deduplicación por nombre+fecha | BD + APIs externas |
| **Obras cerca** | Lista de obras en 300 m | Acceso e imagen de la zona | Open Data BCN, mismo resultado que eventos (Obras) | Open Data BCN 300 m |

---

## 6. Flujo técnico resumido

1. Usuario abre la pestaña **Estimaciones**.
2. ViewModel ejecuta **GetNextWeekDiagramacionAsync()** → obtiene NextWeekDiagramacion (con Days enriquecidos con clima y festivos).
3. ViewModel ejecuta **GetAlertasDiagramacionAsync(nextWeek)**.
4. GetAlertasDiagramacionAsync:
   - Lee **nextWeek.PredictedRevenue** y **nextWeek.Days** (ya con clima y festivos).
   - Consulta **ExecutionDays** para semana anterior y para mismas fechas del mes anterior.
   - Consulta **Events** para la semana siguiente.
   - Si hay config de ubicación/barrio/zona, llama a **Open Data BCN** (eventos + obras 300 m) y opcionalmente **GuiaBCN** (eventos); deduplica.
   - Construye la lista de AlertaDiagramacionItem en el orden indicado y la devuelve.
5. ViewModel asigna las alertas que **no son por día** (Title no contiene nombre de día) a **AlertasGenerales** y las muestra en el bloque "Qué puede afectar la semana siguiente". Las tarjetas con Description vacía se ocultan.

Con esto queda documentado qué se muestra en el bloque, por qué, cómo se obtiene cada dato y en base a qué criterios y fuentes.

---

## 7. Revisión y mejoras (para que sea verdaderamente útil)

### 7.1. Problemas detectados

1. **Redundancia cuando no hay datos de la semana anterior:** Se mostraban dos tarjetas con el mismo mensaje: "Tendencia" (Facturación estable – "No hay datos de la semana anterior para comparar") y "Semana anterior" ("No hay datos de la semana anterior (dd/MM al dd/MM)"). El usuario veía repetido que no hay datos.
2. **Mezcla de tipos de información:** En el mismo bloque conviven (a) **factores que afectan** (clima, festivos, eventos, obras), (b) **contexto** (tendencia, semana anterior, misma semana mes anterior, concentración finde, coste personal) y (c) **metadatos** (base de la predicción, patrones aplicados). No está claro qué es accionable y qué es solo informativo.
3. **Orden:** El orden actual (primero por |pct|, luego por OrdenImpacto) hace que "Misma semana, mes anterior" con +59% salga primero (bien), pero cuando no hay datos la tarjeta "Tendencia" salía también arriba repitiendo el mensaje de "no hay datos".

### 7.2. Cambio aplicado

- **Tendencia solo cuando hay datos para comparar:** La alerta "Tendencia" (Facturación al alza/baja/estable) **solo se añade** cuando existen `nextWeekRevenue` y `prevWeekRevenue > 0`. Si no hay datos de la semana anterior, no se muestra la tarjeta "Facturación estable – No hay datos..."; basta con la tarjeta "Semana anterior" que ya indica "No hay datos de la semana anterior (fechas)". Se evita la redundancia.

### 7.3. Mejoras posibles con lo que ya tenemos

1. **Agrupar en la UI:** En el frontend se podría separar en dos bloques o pestañas: **"Factores que pueden afectar"** (clima, festivos, eventos, obras) y **"Contexto y planificación"** (tendencia, semana anterior, misma semana mes anterior, concentración finde, coste personal). La "Base de la predicción" y "Patrones aplicados" podrían ir en un pie discreto o tooltip.
2. **Priorizar lo accionable:** Ordenar de forma que clima, festivos, eventos y obras aparezcan antes cuando tienen contenido (ya tienen OrdenImpacto bajo; se podría darles un orden que los ponga por delante del "contexto" cuando no hay pct).
3. **Concentración finde:** Añadir un matiz cuando el % sea muy alto (ej. >60 %): "Refuerza mucho el personal en finde." O cuando sea bajo (<40 %): "Facturación más repartida en semana."
4. **Base de la predicción:** Si `weeksUsed < 4`, el texto ya avisa ("Revisa expectativas si la historia es corta."). Se podría mostrar como aviso (borde naranja) en lugar de tarjeta neutra.
5. **Click para saltar al día:** Las alertas que tienen `dates` (clima, festivos, eventos) ya permiten hacer click para ir al día; asegurar que las tarjetas de días concretos (lluvia, festivo, evento) tengan siempre `dates` rellenado para que el subtítulo "Haz click para saltar al día afectado" sea útil.

### 7.4. Resumen

El bloque es **correcto** en datos y fuentes; la lógica y el orden están documentados. La mejora aplicada elimina la redundancia "no hay datos" entre Tendencia y Semana anterior. Para que sea **verdaderamente útil**, conviene: (1) mostrar solo tendencia cuando hay comparación posible, (2) opcionalmente agrupar o etiquetar "factores que afectan" vs "contexto", y (3) dar más peso visual o orden a clima, festivos, eventos y obras cuando tengan contenido.
