# Análisis profundo — Pestaña de Estimaciones

Documento que describe **cómo funciona**, **qué calcula** y **por qué** la pestaña de Estimaciones de Lucas (Windows y vista tablet). Basado en el código actual: `RecomendacionesViewModel`, `InteligenciaService`, `DashboardService`, `RecomendacionesView.xaml`.

---

## 1. Objetivo de la pestaña

La pestaña de Estimaciones sirve para **planificar la semana siguiente**: facturación esperada por día y por turno, horas de personal necesarias, coste de personal en relación a la facturación, y **alertas** (tendencia, clima, festivos, misma semana del mes anterior, eventos y obras cerca) que pueden afectar el resultado.

**Por qué existe:** El restaurante necesita anticipar demanda y personal para la próxima semana. La predicción se apoya en histórico (semanas completas), tendencia reciente, estacionalidad por día de la semana y por mes, y factores externos (clima, festivos, eventos) aprendidos o configurados.

---

## 2. Flujo general al abrir la pestaña

1. **RecomendacionesViewModel.LoadAsync()** se ejecuta al entrar en la pestaña (y al cambiar la semana seleccionada en el selector).
2. Se lanzan en paralelo:
   - **GetRecommendationsAsync()** — lista de recomendaciones (pendientes/aplicadas).
   - **GetNextWeekDiagramacionAsync()** — predicción de la **semana siguiente** (lunes a domingo): total, días, turnos, clima, festivos.
   - **GetWeekSummaryAsync(WeekStart)** — resumen de la **semana seleccionada** (la que el usuario elige con anterior/siguiente) para los KPIs históricos.
3. Con el resultado de **GetNextWeekDiagramacionAsync** se llama a **GetAlertasDiagramacionAsync(nextWeek)** para generar los bloques "Qué puede afectar la semana siguiente".
4. Se rellenan las propiedades del ViewModel (KPIs, párrafo de estimación, tarjetas de días, alertas) y se publica la vista al Backend (**PushEstimacionesToBackendAsync**) para que la tablet muestre los mismos datos vía `/api/estimaciones`.

La **semana siguiente** es siempre la misma: el primer lunes futuro (si hoy es lunes, la semana que empieza el próximo lunes). La **semana seleccionada** (WeekStart) es la que se usa solo para los **cuatro KPIs de promedios históricos** (facturación promedio semanal, productividad promedio, horas promedio, coste de personal %). Es decir: los KPIs superiores reflejan el histórico de la semana que el usuario ha elegido; el bloque central (párrafo + tarjetas de días) es siempre para la **semana siguiente**.

---

## 3. Fuentes de datos

| Dato | Origen | Uso |
|------|--------|-----|
| **Semana siguiente (lunes–domingo)** | Fecha: primer lunes &gt; hoy. | Rango de la predicción. |
| **Predicción guardada** | Tabla `WeeklyPredictions` (WeekStartMonday = lunes semana siguiente). | Si existe y tiene `DailyPredictionsJson`, se usa para rellenar facturación por día/turno, min/max; si no, se calcula en vivo desde histórico. |
| **Histórico de días** | Tabla `ExecutionDays` + `ShiftFeedbacks`. | Solo días **financieramente completos**: TotalRevenue &gt; 0, TotalHoursWorked &gt; 0, los 3 turnos (Mediodía, Tarde, Noche) con Revenue y HoursWorked &gt; 0. |
| **Semanas completas** | Agrupación por lunes; solo semanas con **7 días distintos** con datos completos. | Base para tendencia (4 vs 4), promedios por día de la semana (DOW), y para generar la predicción cuando no hay fila en `WeeklyPredictions`. |
| **Resumen semana seleccionada** | `DashboardService.GetWeekSummaryAsync(WeekStart)`. | TotalRevenue, AvgProductivity, TotalHours, **AvgRevenueHistoric**, **AvgProductivityHistoric**, **AvgHoursHistoric**, PrevWeekRevenue, PrevWeekProductivity, Days. |
| **Configuración** | `ConfiguracionService`: HorasPorTurno, ProductividadIdealEurHora, CostoPersonalPorHora, Employees (horas semanales por empleado), Lat/Lon restaurante, BarrioInteres, ZonaInteres. | Productividad objetivo, horas por turno, coste/hora, esquema sala/cocina; ubicación para clima, festivos, eventos/obras. |
| **Clima** | `ClimaService.GetWeatherForDateAsync(fecha)` (Open-Meteo). | Descripción por día y factor de ajuste por lluvia (patrón aprendido). |
| **Festivos** | `FestivosService.GetHolidayInfoAsync(fecha, lat, lon)` (Nager + Nominatim). | IsHoliday, HolidayName por día; factor festivo en la predicción. |
| **Eventos / Obras** | Tabla `Events` + `OpenDataBcnService.GetEventosYObrasCercaAsync` (300 m) + opcionalmente GuiaBCN. | Alertas "Eventos esta semana" y "Obras cerca" (solo si hay resultados). |
| **Patrones aprendidos** | Tabla `DetectedPatterns`: "Impacto clima lluvioso", "Impacto temperatura", "Impacto festivos". | Factores de ajuste (rainFactor, holidayFactor, tempFactor) con peso según confianza del patrón. |

---

## 4. Qué se muestra en pantalla (y de dónde sale)

### 4.1. Cuatro KPIs superiores (promedio histórico)

- **Facturación promedio semanal:** `summary.AvgRevenueHistoric` (DashboardService). Es la **media de la facturación total por semana** sobre todas las semanas con al menos 5 días con datos. **Por qué:** Da una referencia de “cuánto se suele facturar por semana” para comparar con la estimación de la semana siguiente.
- **Productividad promedio semanal:** `summary.AvgProductivityHistoric` (€/h). Media global de productividad (TotalRevenue/TotalHoursWorked) sobre esas mismas semanas. **Por qué:** Referencia de eficiencia histórica.
- **Horas promedio semanal:** `summary.AvgHoursHistoric`. Media de horas trabajadas por semana. **Por qué:** Referencia de volumen de personal en el pasado.
- **Costo de personal:** Se muestra el **porcentaje** respecto a la facturación promedio histórica (`KpiCostoPersonalPctFacturacion`) y debajo el coste en € (`KpiCostoPersonalPredPrincipal`). El coste en € se calcula como: `totalHorasContrato * CostoPersonalPorHora`, donde `totalHorasContrato` es la suma de las horas semanales de contrato de todos los empleados (Configuración). **Por qué:** Permite ver qué parte de la facturación se va en personal; el % se entiende respecto al nivel de facturación habitual.

### 4.2. Párrafo de estimación (semana siguiente)

Generado por **GenerarEstimacionSemanaSiguiente()** en el ViewModel:

1. **Facturación total estimada:** `nextWeek.PredictedRevenue` o suma de `days.PredictedRevenue`. Frase: "Para la semana del [rango], se estima una facturación total de X €."
2. **Horas necesarias para la productividad objetivo:** Si `ProductividadIdealEurHora` &gt; 0: `horasNecesarias = revenueTotal / ProductividadIdealEurHora`; "La cantidad de horas necesarias para alcanzar la productividad objetivo (50 €/h) con la facturación estimada es de X horas (unas Y al día)." **Por qué:** El negocio tiene un objetivo de €/h; con la facturación estimada se puede saber cuántas horas de personal encajan en ese objetivo.
3. **Coste de personal y %:** Si hay CostoPersonalPorHora y horas (contrato o predichas): `costePersonal = horas * costoHora`, `pct = costePersonal / revenueTotal * 100`. "El coste de personal se ubica en un X% vs la facturación estimada."
4. **Días con mayor estimación:** Los 2 (o 1) días con mayor `PredictedRevenue`; frase del tipo "Los días con mayor estimación son el Sábado (X €) y el Viernes (Y €)." **Por qué:** Ayuda a enfocar refuerzos o atención en esos días.

### 4.3. Tarjetas por día (Lunes–Domingo)

Para cada día de la **semana siguiente** se muestra:

- **Nombre y fecha:** DayName, Date (dd/MM).
- **PredictedRevenue (€):** Facturación estimada del día.
- **Rango min–max (€):** Bandas de incertidumbre (véase más abajo cómo se calculan).
- **Confianza:** "Alta" / "Media" / "Baja" según el ancho relativo del rango: si `(max - min) / revenue < 0,30` → Alta; si &lt; 0,50 → Media; si no → Baja. **Por qué:** Indica hasta qué punto la estimación es estable o incierta.
- **Desglose por turno:** Mediodía, Tarde, Noche con su `PredictedRevenue` (y en el modelo `SuggestedStaff`; en la UI de tarjetas se muestra sobre todo el revenue).
- **Sala / Cocina:** Esquemas tipo "2-2-2" (mediodía-tarde-noche). Calculados con **GetSalaCocinaScheme**: a partir de la facturación estimada por turno y la productividad ideal (€/h) y horas por turno se obtiene un “total de personas” por turno; se convierte en reparto sala/cocina con **TotalToCocinaSala** (2→(1,1), 3→(2,1), 4→(2,2), 5→(3,2), 6→(3,3)) y se aplican umbrales: día ≥ 2400 € → al menos 2 sala y 2 cocina por turno; día ≥ 3000 € → hasta 3 cocina; ≥ 3500 € → hasta 3 sala; turno ≥ 600 € → al menos 2 sala. **Por qué:** Da una guía operativa de dotación por turno.
- **Contexto del día:** Clima (descripción texto) y Festivo (Sí/No + nombre si es festivo). Vienen de **EnrichDaysWithWeatherAndHolidayAsync** (ClimaService + FestivosService).

### 4.4. Bloque "Qué puede afectar la semana siguiente"

Son las **alertas** devueltas por **GetAlertasDiagramacionAsync**:

1. **Semana anterior:** Datos reales de la semana pasada (lunes a domingo): facturación total, horas trabajadas, productividad €/h. **Por qué:** Contexto inmediato antes de la semana que se estima.
2. **Tendencia:** Comparación de la **estimación de la semana siguiente** con la **facturación real de la semana anterior**. Si nextWeekRevenue &gt; previousWeekRevenue en más de un 1%: "Esperamos facturar un X% más que la semana anterior." Si es menor: "X% menos." Si está en torno: "En línea con la semana anterior." **Por qué:** Resume si la siguiente semana se espera mejor, peor o igual que la última ya cerrada.
3. **Clima:** Lista de días con lluvia prevista (texto que contenga "lluvia", "rain", "Llovizna", "drizzle") en la semana siguiente. Si no hay ninguno: "Sin días de lluvia previstos." **Por qué:** La lluvia suele afectar a la demanda; el usuario puede preparar personal o oferta.
4. **Festivos:** Solo si hay algún día festivo en la semana siguiente: nombre del festivo y fecha. Si no hay, el bloque puede ocultarse o mostrarse vacío. **Por qué:** Los festivos cambian demanda y horarios.
5. **Misma semana, mes anterior:** Se comparan las **mismas fechas numéricas** del mes pasado (ej. 3–9 feb vs 3–9 ene). Se suma la facturación de esos días del mes pasado y se compara con la estimación de la semana siguiente: "Esperamos facturar un X% más/menos que los mismos días del mes pasado. El mes pasado, para esas fechas facturaste Y €." **Por qué:** Estacionalidad “misma semana del mes”; útil cuando no hay un año entero de datos.
6. **Eventos esta semana:** Eventos de la tabla `Events` + Open Data BCN (radio 300 m) + GuiaBCN, deduplicados. Solo se muestra el bloque si hay al menos un evento. **Por qué:** Eventos cerca pueden aumentar o desviar demanda.
7. **Obras cerca:** Obras en espacio público (Open Data BCN, 300 m). Solo si hay resultados. **Por qué:** Obras pueden afectar acceso o ambiente.

---

## 5. Cómo se calcula la predicción (sin predicción guardada)

Cuando **no** hay fila en `WeeklyPredictions` para el lunes de la semana siguiente, o la hay pero `DailyPredictionsJson` está vacío o todos los días salen a 0, **GetNextWeekDiagramacionAsync** calcula la predicción en vivo así:

1. **Histórico válido:** Días con `Date < currentWeekMonday` (antes de la semana en curso) que cumplan **IsFinanciallyComplete** (TotalRevenue y TotalHoursWorked &gt; 0, 3 turnos con revenue y hours &gt; 0).
2. **Semanas completas:** Agrupar por `GetWeekStartMonday`; quedarse con las que tienen 7 días distintos. Ordenar por fecha descendente. **last4Weeks** = primeras 4, **prev4Weeks** = siguientes 4.
3. **Promedios y factores:**
   - **overallAvg** = media(TotalRevenue) de todos los días completos.
   - **trendPct** = (suma revenue last4Weeks − suma revenue prev4Weeks) / suma prev4Weeks × 100. **trendPctClamped** = limitado a [-5, 25] para no recortar demasiado por tendencia negativa ni disparar por positiva.
   - **recentLevelFactor** = media(TotalRevenue) de los últimos 7 días / overallAvg; limitado a [0,95, 1,1].
   - **avgByDow** = para cada día de la semana (L–D), media(TotalRevenue) de los días históricos con ese mismo DOW.
   - **biasByDow** = sesgo por día de la semana desde configuración (PredictionBiasJson); se aplica como corrección según error histórico aprendido.
   - **monthAvg** = para el mes de cada día de la semana siguiente, media(TotalRevenue) de ese mes / overallAvg; limitado [0,9, 1,1] (estacionalidad mensual).
4. **Por cada día de la semana siguiente:**
   - **est** = avgByDow[dow] × trendFactor × biasFactor × recentFactor × monthFactor (todos los factores limitados; product final entre 0,96 y 1,12).
   - Se suma una **pendiente por DOW** (dowSlope) si hay suficientes datos, solo positiva.
   - **Corrección central +1%** (est *= 1,01) para compensar estimaciones sistemáticamente bajas.
   - **Bandas:** CV = desviación típica / media para ese DOW (o 0,15 por defecto). min = est×(1 − 1,5×CV), max = est×(1 + 1,5×CV); límites adicionales 0,85×est y 1,15×est.
   - Turnos: por defecto est/3 para cada turno (Mediodía, Tarde, Noche).
5. **Enriquecimiento:** Llamar a clima y festivos por cada día (**EnrichDaysWithWeatherAndHolidayAsync**). En este flujo “en vivo” no se aplica aquí el factor de lluvia en la estimación (sí se aplica cuando la predicción se guarda en **BuildNextWeekPredictionAsync**).

**Por qué cada factor:**

- **Base por DOW:** Los lunes no facturan igual que los sábados; la base debe ser por día de la semana.
- **Tendencia 4 vs 4:** Si las últimas 4 semanas facturan más que las 4 anteriores, tiene sentido subir un poco la estimación (y al revés, pero limitado para no bajar en exceso).
- **Nivel reciente:** Si los últimos 7 días están por encima de la media, la siguiente semana puede mantener parte de ese nivel.
- **Estacionalidad por mes:** Algunos meses son más fuertes o más flojos; el factor modera o refuerza según el mes.
- **Sesgo por DOW:** Aprendizaje de error pasado (por ejemplo “los lunes hemos tendido a sobrestimar” → bias negativo para lunes).
- **Bandas:** Reflejan la variabilidad histórica del mismo día de la semana; más datos y más estables → banda más estrecha y confianza “Alta”.

---

## 6. Cómo se calcula la predicción guardada (RunWeeklyAnalysisAsync → BuildNextWeekPredictionAsync)

El análisis semanal (**RunWeeklyAnalysisAsync**) se ejecuta en segundo plano (al guardar Registro o al iniciar la app). Incluye:

1. **Evaluar predicciones** de semanas ya cerradas (EvaluatePredictionsAsync).
2. **Patrones estacionales:** Por cada día de la semana (L–D), media y desv. típica de facturación; se guardan en `DetectedPatterns` (tipo "Estacional").
3. **Patrón clima lluvioso:** Días con código WMO de lluvia vs días despejados; si hay suficientes, se calcula pct_diff (lluvia vs sol) y se guarda en "Impacto clima lluvioso". Sirve para ajustar la predicción cuando el pronóstico indica lluvia.
4. **Tendencias:** Últimas 4 semanas vs 4 anteriores; si la diferencia es &gt; 5%, se guarda en `DetectedTrends` (Facturación, up/down/stable).
5. **Predicción próxima semana (BuildNextWeekPredictionAsync):**
   - Misma idea de semanas completas y last4Weeks / prev4Weeks.
   - **Por cada día** de la semana siguiente:
     - Media ponderada por día de la semana (con decay por antigüedad y peso por **GetFeedbackStrength** del día: feedback V/R/M/D da más peso a días “más activos”).
     - **Factores:** trendFactor, biasFactor, recentFactor, monthFactor (igual que antes).
     - **Factores operativos aprendidos:** Si existe patrón "Impacto clima lluvioso", se lee pct_diff y se define **rainFactor** (ej. 0,88–1,08). Para cada día se consulta el clima; si el código es de lluvia (51–67, 80–82), se aplica **weatherFactors[k] = rainFactor**. **rainBlend** (0,25–0,75) según confianza del patrón: el factor final es 1 + (weatherFactor − 1) × rainBlend. **Por qué:** Aprender del pasado “cuánto bajan los días de lluvia” y aplicarlo cuando el pronóstico dice lluvia.
     - **Festivos:** Si existe patrón "Impacto festivos", se usa su pct_diff como holidayFactorDefault. Para cada día se consulta si es festivo; si lo es, holidayFactors[k] = ese factor. **Por qué:** Los festivos pueden subir o bajar la demanda según el tipo de local.
     - **Temperatura extrema:** Patrón "Impacto temperatura" y códigos de clima; si temp &lt; 5 °C o &gt; 30 °C se aplica tempFactor. **Por qué:** Olas de frío/calor afectan terraza y afluencia.
     - **Eventos (tabla Events):** Por día, si hay evento con impacto Alto/Medio/Bajo se aplica un factor (ej. 1,15 / 1,0 / 0,98).
   - **Bandas:** std del mismo DOW o MAE aprendido (PredictionMaeJson); halfBand = max(1,5×std, 1,5×learnedMae); min/max con límites 0,85–1,15 respecto a la estimación.
   - **Por turno:** Se obtiene media de revenue por turno del histórico del mismo DOW; se aplican trendFactor y biasFactor; **personal sugerido** = ceil(shiftRev / productividadIdeal / horasPorTurno), refinado con promedio real de personal del histórico (60% modelo, 40% histórico) si hay datos.
   - Se normaliza la suma de revenue por turno al revenue del día si no cuadra.
   - **EstimatedStaffHours:** Suma de (staff por turno × horasPorTurno) para los 7 días; se guarda en `WeeklyPredictions.EstimatedStaffHours`.
   - Se persiste en `WeeklyPredictions`: WeekStartMonday, PredictedRevenue, DailyPredictionsJson (array por día con date, revenue, min, max, mediodia/tarde/noche con revenue y staff), HistoricalStatsJson.
6. **Recomendaciones:** Si la tendencia (last4 vs prev4) varía ≥ 5%, se crea una recomendación "Facturación al alza" o "Facturación a la baja" para que el usuario la vea y opcionalmente la marque como aplicada.

**Por qué se guarda:** Para no recalcular cada vez que se abre la pestaña; la vista lee de `WeeklyPredictions` y solo se recalcula cuando se vuelve a ejecutar el análisis semanal (por ejemplo tras guardar más datos en Registro).

---

## 7. Esquema sala/cocina (detalle)

- **Entrada:** Lista de 3 turnos (Mediodía, Tarde, Noche) con `PredictedRevenue` cada uno; productividad ideal (€/h); horas por turno; revenue del día y max del día (para umbrales).
- **Personas por turno:** total = round(PredictedRevenue_turno / (productividadIdeal × horasPorTurno)). Si es 0 se fuerza al menos 1 donde aplique.
- **Reparto sala/cocina:** TotalToCocinaSala(total) → 2→(1,1), 3→(2,1), 4→(2,2), 5→(3,2), 6→(3,3). Máximos 3 cocina y 3 sala.
- **Umbrales:**
  - Si día ≥ 2400 € (o max ≥ 2400): **requiere2PorTurno** → mínimo 2 sala y 2 cocina en cada turno.
  - Si día &gt; 3000 €: máximo cocina = 3; si día ≥ 3500 €: máximo sala = 3.
  - Si revenue del turno &gt; 600 €: mínimo sala en ese turno = 2.
- **Salida:** Dos cadenas "X-Y-Z" (mediodía-tarde-noche) para sala y cocina. **Por qué:** Traducir facturación esperada en una guía operativa de dotación sin pasarse (límites 2–3 por área y umbrales de volumen).

---

## 8. Horas necesarias para la productividad objetivo

En el párrafo de estimación:

- **horasNecesarias = facturaciónEstimadaSemana / ProductividadIdealEurHora**
- Si ProductividadIdealEurHora es 50 €/h y la facturación estimada es 12.000 €, horasNecesarias = 240 h (unas 34 h/día). **Por qué:** Si el objetivo es ganar 50 € por cada hora de personal, con 12.000 € de facturación “cabrían” 240 h de personal para estar justo en ese objetivo. Es una referencia para decidir cuántas personas poner (teniendo en cuenta horas por turno y días).

---

## 9. Coste de personal y %

- **Coste total (€):** totalHorasContrato × CostoPersonalPorHora (horas de contrato de todos los empleados × coste/hora configurado).
- **% respecto a facturación:** En la pestaña se muestra respecto a la **facturación promedio semanal histórica** (AvgRevenueHistoric) en los KPIs, y en el párrafo respecto a la **facturación estimada** de la semana siguiente. **Por qué:** Ver si la masa salarial es sostenible respecto al nivel de facturación (histórico o esperado).

---

## 10. Sincronización con la tablet

Al cargar la pestaña, **PushEstimacionesToBackendAsync** serializa el estado actual (KPIs, resumen, días, alertas) en **EstimacionesCachePayload** y hace POST a `http://localhost:5261/api/estimaciones/cache` (y opcionalmente a la URL del Backend configurada). El Backend guarda ese JSON en `%LocalAppData%\ManagerOS\estimaciones-cache.json`. La vista tablet (o cualquier cliente) que llame a GET `/api/estimaciones` recibe ese mismo JSON. **Por qué:** La tablet no tiene acceso a la lógica de InteligenciaService ni a la BD del PC; ve una “foto” de la última vez que se abrió la pestaña en la app Windows. Si se quiere ver datos actualizados, hay que volver a abrir la pestaña en el PC o implementar un endpoint en el Backend que recalcule (por ejemplo con la misma lógica de GetNextWeekDiagramacionAsync y GetAlertasDiagramacionAsync).

---

## 11. Resumen de “por qué” de cada cálculo

| Qué se calcula | Por qué |
|-----------------|--------|
| Semana siguiente como “primer lunes futuro” | La planificación es siempre para la próxima semana completa, no para la actual (que puede estar a medias). |
| Solo semanas/días “completos” | Evitar que semanas a medias o días sin horas/revenue distorsionen promedios y tendencias. |
| Base por día de la semana (DOW) | El patrón L–D es el más estable (lunes vs sábado). |
| Tendencia 4 vs 4 | Capturar si la demanda va al alza o a la baja en las últimas 8 semanas sin reaccionar en exceso a un solo dato. |
| Límite de tendencia negativa (-5%) | Evitar que una mala racha baje demasiado la estimación; se asume cierta reversión. |
| Nivel reciente (últimos 7 días) | Si la demanda reciente es alta/baja, la semana siguiente puede parecerse un poco. |
| Estacionalidad por mes | Meses con más o menos actividad (verano, vacaciones, etc.). |
| Sesgo por DOW (PredictionBiasJson) | Corregir errores sistemáticos por día de la semana aprendidos al evaluar predicciones pasadas. |
| Bandas min/max (CV o MAE) | Cuantificar incertidumbre; más datos y menos dispersión → confianza “Alta”. |
| Factor lluvia (patrón aprendido) | Los días lluviosos históricamente facturan distinto; si el pronóstico dice lluvia, se ajusta. |
| Factor festivos/temperatura/eventos | Factores operativos que en el pasado mostraron impacto y que se conocen para la semana siguiente. |
| Esquema sala/cocina con umbrales | Traducir facturación esperada en dotación mínima razonable (2–3 por área, umbrales 600 €/turno, 2400/3000/3500 €/día). |
| Horas para productividad objetivo | Responder: “con esta facturación estimada, ¿cuántas horas de personal encajan en mi objetivo de €/h?”. |
| Alertas (tendencia, clima, festivos, misma semana mes anterior, eventos, obras) | Dar contexto ejecutivo para la semana siguiente sin duplicar el detalle que ya está en el párrafo o en las tarjetas. |

---

## 12. Referencia de archivos

- **ViewModel y vista:** `RecomendacionesViewModel.cs` (LoadAsync, GenerarEstimacionSemanaSiguiente, PushEstimacionesToBackendAsync), `RecomendacionesView.xaml`.
- **Diagramación y alertas:** `InteligenciaService.cs` — GetNextWeekDiagramacionAsync, GetAlertasDiagramacionAsync, EnrichDaysWithWeatherAndHolidayAsync, GetConfidenceLabel, GetSalaCocinaScheme, TotalToCocinaSala, IsFinanciallyComplete, GetCompleteWeeks.
- **Predicción guardada:** `InteligenciaService.RunWeeklyAnalysisAsync`, BuildNextWeekPredictionAsync (líneas ~905–1204), GetFeedbackStrength.
- **Resumen semanal (KPIs):** `DashboardService.GetWeekSummaryAsync`, GetWeekStart, GenerarResumen.
- **Modelos:** `IInteligenciaService.cs` — NextWeekDiagramacion, NextWeekDayItem, NextWeekShiftPrediction, AlertaDiagramacionItem.
- **Caché tablet:** `EstimacionesCachePayload.cs`, `EstimacionesController.cs` (GET/POST cache).
