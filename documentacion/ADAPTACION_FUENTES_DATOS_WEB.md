# Adaptación de FUENTES_DATOS_PATRONES_TENDENCIAS_Y_PREDICCION a la app web

Este documento indica cómo los métodos descritos en **FUENTES_DATOS_PATRONES_TENDENCIAS_Y_PREDICCION.md** (app Windows) están implementados en la **app web** (LucasWeb.Api).

---

## 1. Flujo de información

| Origen (documento) | App web |
|--------------------|---------|
| BD: ExecutionDays + ShiftFeedbacks | `ExecutionDays`, `ShiftFeedbacks` (EF). Histórico: `Date < lunes semana actual`, `TotalRevenue > 0`, `TotalHoursWorked > 0`. |
| RunWeeklyAnalysisAsync | **POST /api/estimaciones/compute-patterns**: primero evalúa semana pasada (bias/MAE), luego calcula patrones. |
| EvaluatePredictionsAsync | **EvaluatePredictionsService.EvaluateLastWeekIfPendingAsync()**. Se invoca desde compute-patterns o **POST /api/estimaciones/evaluate-predictions**. |
| Clima / Festivos / Eventos | **WeatherService** (Open-Meteo), **NagerHolidaysService**, **EventsService** (tabla Events). |

---

## 2. Filtrado de datos

| Concepto (documento) | App web |
|----------------------|---------|
| Día financieramente completo | Criterio documentado: TotalRevenue > 0, TotalHoursWorked > 0, 3 turnos con Revenue/HoursWorked > 0. En web se usa el criterio simplificado (solo TotalRevenue y TotalHoursWorked) para la predicción en vivo; el filtro estricto por turnos se puede añadir cuando se carguen turnos. |
| Semanas completas | Agrupación por lunes; semanas con **≥ 5 días** (documento pide 7; en web se usa 5 para tener más semanas con datos). |
| DOW (Lunes=0 … Domingo=6) | **NextWeekPredictionService**: `Dow(d) => (int)d.DayOfWeek == 0 ? 6 : (int)d.DayOfWeek - 1`. |

---

## 3. Patrones (DetectedPatterns)

| Patrón (documento) | App web |
|---------------------|---------|
| Estacional por DOW | **DetectedPatternsService.ComputeAndSavePatternsAsync()**: por cada día de la semana (Lunes–Domingo) se guarda un patrón tipo "Estacional" con Key = "Lunes"…"Domingo", JsonData con `avg_revenue`, `std_dev`, `count`. |
| Impacto clima lluvioso | Códigos lluvia: 51–67, 71–77, 80–82, 95–96. Soleados: 0, 1, 2. Solo se guarda si **\|pct_diff\| ≥ 10%** y hay ≥3 días lluviosos y ≥3 soleados. |
| Impacto festivos | IsHoliday vs no; solo se guarda si \|pctDiff\| ≥ 10% y ≥3 festivos y ≥3 no festivos. |
| Impacto temperatura | Extremos (temp &lt; 5 o &gt; 30 o null) vs suaves (15–25 °C). Solo se guarda si \|pctDiff\| ≥ 10%. |

---

## 4. Tendencias

| Documento | App web |
|-----------|---------|
| last4Weeks vs prev4Weeks, trendPct, trendFactor | **NextWeekPredictionService**: suma last4 vs prev4, `trendPctClamped = Clamp(trendPct, -5, 25)`, **trendFactor = max(1, 1 + (trendPctClamped/100)*0.35)** (solo parte positiva). |
| DetectedTrends (tabla) | No se persiste tabla DetectedTrends; la tendencia se calcula en vivo en la predicción. |

---

## 5. Aprendizaje bias y MAE (EvaluatePredictionsAsync)

| Documento | App web |
|-----------|---------|
| Evaluar semana pasada | **EvaluatePredictionsService**: busca `WeeklyPrediction` con `WeekStartMonday = lastMonday` y `CompletedAt == null`. |
| ActualRevenue, CompletedAt, AccuracyMetricsJson | **WeeklyPrediction**: campos `ActualRevenue`, `CompletedAt`; se rellenan al evaluar. `AccuracyMetricsJson` con error %, accuracy %. |
| Bias por DOW (media móvil error %) | Se guarda en **Settings** con clave `PredictionBiasJson`: `{ "avg": [7], "count": [7] }`. **UpdatePredictionBiasForDayOfWeek**: avg[dow] = (avg[dow]*n + errorPct) / (n+1), count[dow]++. |
| MAE por DOW (media móvil \|error\| €) | **Settings** `PredictionMaeJson`: `{ "avg_mae": [7], "count": [7] }`. |
| Uso en predicción | **NextWeekPredictionService**: lee PredictionBiasJson y PredictionMaeJson; **biasFactor = max(0.99, 1 - (biasClamped/100)*0.35)**; **halfBand = max(1.5*std, 1.5*learnedMae)** para bandas min–max. |

---

## 6. Construcción de la predicción (BuildNextWeekPredictionAsync → ComputeLivePredictionAsync)

| Documento | App web |
|-----------|---------|
| overallAvg, trendFactor, recentLevelFactor | **NextWeekPredictionService**: media histórico, trendFactor (max 1, …), últimos 7 días → recentLevelFactor [0.95, 1.1]. |
| avgByDow, std por DOW, monthFactor | Por DOW: media y CV (std/mean). Por mes: media(mes)/overallAvg [0.9, 1.1]. |
| biasFactor, learnedMae → bandas | biasFactor por DOW; halfBand = max(1.5*stdEur, 1.5*learnedMae); min/max con límites 0.85*dayAvg, 1.15*dayAvg. |
| Factores lluvia/festivos/temperatura | **PredictionEnrichmentService**: tras añadir clima y festivos por día, se leen patrones "Impacto clima lluvioso", "Impacto festivos", "Impacto temperatura"; **rainBlend/holidayBlend/tempBlend** = 0.25 + (Confidence/100)*0.5; se aplica weatherFactor, holidayFactor, tempFactor a la **revenue** de cada día (límite [0.96, 1.12]). |
| totalRevenue tras enriquecimiento | **PredictionsController**: después de enriquecer, se re-suma `revenue` de cada día del JSON y se devuelve como `totalRevenue`. |

---

## 7. Endpoints y orden de ejecución

| Acción | Endpoint / uso |
|--------|----------------|
| Evaluar predicción semana pasada (bias/MAE) | **POST /api/estimaciones/evaluate-predictions** |
| Calcular patrones (estacional, lluvia, festivos, temp) | **POST /api/estimaciones/compute-patterns** (ejecuta primero evaluate, luego patrones) |
| Predicción semana siguiente (guardada o en vivo) | **GET /api/predictions/next-week** (opcional lat, lon, countryCode para enriquecer) |
| Alertas (semana anterior, tendencia, clima, festivos, eventos, obras, misma semana mes ant.) | **GET /api/estimaciones/alertas** |

**Orden recomendado (equivalente a RunWeeklyAnalysisAsync):**  
1) **POST /api/estimaciones/evaluate-predictions** (o incluido en compute-patterns).  
2) **POST /api/estimaciones/compute-patterns**.  
3) Al abrir Estimaciones: **GET /api/dashboard/week**, **GET /api/predictions/next-week**, **GET /api/estimaciones/alertas**.

---

## 8. Diferencias respecto al documento (Windows)

- **Semanas completas:** en web se usan semanas con ≥ 5 días (no obligatorio 7) para disponer de más datos.
- **Día financieramente completo:** en la predicción en vivo no se exige aún 3 turnos con Revenue/HoursWorked > 0; solo TotalRevenue y TotalHoursWorked.
- **DetectedTrends:** no hay tabla; la tendencia se calcula en vivo en NextWeekPredictionService.
- **GetFeedbackStrength (peso Q1–Q4):** no implementado en la predicción en vivo; la media por DOW es simple (sin ponderar por feedback).
- **Eventos (tabla Events) con factor por impacto:** en el enriquecimiento no se aplica eventFactor por día; los eventos se usan en alertas. Se puede añadir eventFactor leyendo Events por fecha e impacto.

---

## 9. Bloque "Qué puede afectar la semana siguiente" (ANALISIS_BLOQUE_QUE_PUEDE_AFECTAR_SEMANA_SIGUIENTE.md)

La lógica del bloque está en **EstimacionesController.GetAlertas()** y la vista en **wwwroot/js/views/estimaciones.js**. Orden y formatos alineados con el documento de análisis (programa Windows).

| # | Tipo (doc) | Título en web | Cómo se obtiene en web |
|--:|------------|---------------|-------------------------|
| 1 | Semana anterior | Semana anterior | **ExecutionDays** semana pasada (prevMonday..prevEnd). Texto: "Semana del dd/MM al dd/MM: X € facturados, Y h trabajadas, Z €/h de productividad." o "No hay datos de la semana anterior (dd/MM al dd/MM)." |
| 2 | Tendencia | Facturación al alza / a la baja / estable | nextWeekRevenue (predicción guardada o ComputeLivePredictionAsync) vs previousWeekRevenue. Umbrales ±1 %. Texto: "Esperamos facturar un X% más/menos..." o "En línea con la semana anterior." o "No hay datos de la semana anterior para comparar." |
| 3 | Clima | Clima semana siguiente | **WeatherService** (Open-Meteo) para nextMonday..nextEnd. Días con lluvia (IsRainy): "Lluvia prevista: Lunes 09/02, ..." o "Sin días de lluvia previstos para la semana siguiente." |
| 4 | Festivos | Festivos | **NagerHolidaysService** (CountryCode). Solo se añade si hay al menos un festivo con nombre no vacío. Formato: "{HolidayName} el {Día} dd/MM." |
| 5 | Misma semana, mes anterior | Misma semana, mes anterior | **ExecutionDays** en fechas numéricas del mes anterior (sameStart = nextMonday.AddMonths(-1), sameEndPrev = nextEnd.AddMonths(-1)). Tres casos: sin datos, con estimación (%), solo datos pasados. |
| 6 | Eventos | Eventos esta semana | **EventsService** (tabla Events). Solo si hay eventos. Formato: "{Nombre} (dd/MM, impacto Alto/Bajo)." (impacto solo si ≠ Medio). Open Data BCN / GuiaBCN no implementados en web. |
| 7 | Obras | Obras cerca | **EventsService.GetWorksNearbyAsync(lat, lon, 300)**. Por ahora stub (vacío); se puede conectar a Open Data BCN obres. Formato: "{Nombre} (dd/MM)." si hay fecha. |

**Frontend:** El bloque muestra el subtítulo "Tendencia, clima, festivos, semana anterior y misma semana del mes anterior." y lista solo alertas con **texto no vacío** (las que tienen descripción vacía se ocultan).
