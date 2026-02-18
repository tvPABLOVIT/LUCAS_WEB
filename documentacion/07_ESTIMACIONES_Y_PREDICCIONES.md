# 07 — Estimaciones y predicciones

Lógica detallada de la predicción de la semana siguiente, factores, uso de clima y festivos, y construcción de la vista de estimaciones y alertas.

---

## 1. Objetivo

- **Entrada:** Histórico de días de ejecución (ExecutionDays + ShiftFeedbacks) y parámetros (productividad objetivo, horas por turno, ubicación para clima/festivos).
- **Salida:** Para la “semana siguiente” (lunes a domingo): facturación estimada por día (y por turno), bandas min–max, etiqueta de confianza, esquema sala/cocina, clima previsto, festivos; y bloques de alertas (tendencia, clima, festivos, misma semana mes anterior, eventos/obras).

---

## 2. Semana “siguiente” y semana “completa”

- **Semana siguiente:** Lunes a domingo de la semana que viene (el primer lunes &gt; hoy, más 6 días).
- **Semana completa (para histórico):** Semana con 7 días distintos con datos “financieramente completos”: TotalRevenue &gt; 0, TotalHoursWorked &gt; 0, y los tres turnos (Mediodía, Tarde, Noche) con Revenue y HoursWorked &gt; 0. Solo estas semanas se usan para tendencias y promedios por día de la semana.

---

## 3. Cálculo de la predicción por día (sin predicción guardada)

Si no existe una fila en `WeeklyPredictions` para el lunes de la semana siguiente (o existe pero DailyPredictionsJson está vacío o todos los días salen a 0), se estima a partir del histórico:

1. **Semanas completas:** Obtener todas las semanas completas del histórico; ordenar por fecha descendente. Últimas 4 semanas = last4Weeks; 4 anteriores = prev4Weeks.
2. **Promedio global:** overallAvg = media(TotalRevenue) de todos los días completos.
3. **Tendencia 4 vs 4:** last4Sum = suma revenue last4Weeks; prev4Sum = suma revenue prev4Weeks. trendPct = (last4Sum - prev4Sum) / prev4Sum * 100. Limitar trendPct a [-5, 25]. trendFactor = 1 + (trendPct/100)*0.35 (solo aplicar positivo; no bajar por tendencia negativa de forma agresiva).
4. **Nivel reciente:** recent7 = últimos 7 días con datos. recentLevelFactor = media(recent7) / overallAvg. Limitar a [0.95, 1.1].
5. **Sesgo por DOW:** Si existe configuración PredictionBiasJson (7 valores, uno por día de la semana 0–6 o 1–7), biasFactor = 1 - (bias/100)*0.35, limitado.
6. **Por cada día de la semana siguiente (L–D):**
   - dow = día de la semana (0=Dom, 1=Lun… o 1=Lun, 7=Dom según convención).
   - avgByDow[dow] = media(TotalRevenue) de todos los días históricos con ese mismo día de la semana.
   - est = avgByDow[dow] * trendFactor * recentFactor * biasFactor * monthFactor (estacionalidad mes: media del mismo mes / overallAvg, limitada 0.9–1.1).
   - Bandas: CV = desviación típica / media para ese DOW (si hay ≥3 datos); min = est*(1 - 1.5*CV), max = est*(1 + 1.5*CV); límites adicionales 0.85*est y 1.15*est.
   - Turnos: por defecto est/3 para Mediodía, Tarde, Noche.
7. **Enriquecimiento:** Por cada día, llamar a clima (previsión para esa fecha) y festivos (lat/lon del restaurante); rellenar WeatherDescription, IsHoliday, HolidayName. Si hay patrón “lluvia vs soleado”, aplicar factor de reducción en días con lluvia.
8. **Esquema sala/cocina:** Con productividad ideal (€/h) y horas por turno, calcular personas por turno y reparto sala/cocina (TotalToCocinaSala); aplicar umbrales (2400 € día → 2 sala 2 cocina por turno; 3000/3500 € → hasta 3 cocina/sala; 600 € por turno → al menos 2 sala). Ver 03.

---

## 4. Predicción guardada (WeeklyPredictions)

Si existe una fila en `WeeklyPredictions` para el lunes de la semana siguiente con `DailyPredictionsJson` válido:

- Leer el JSON: array de 7 elementos (o con clave "date") con revenue, min, max por día y por turno (mediodia, tarde, noche).
- Rellenar la vista con esos valores y enriquecer con clima y festivos por día.
- Las alertas (tendencia, semana anterior, misma semana mes anterior, etc.) se calculan igual usando la predicción total de la semana.

---

## 5. Alertas / bloques “Qué puede afectar la semana siguiente”

1. **Semana anterior:** GET ExecutionDays donde Date ∈ [previousWeekMonday, previousWeekSunday]. Sumar TotalRevenue, TotalHoursWorked; productividad = revenue/hours. Texto: “Semana del DD/MM al DD/MM: X € facturados, Y h trabajadas, Z €/h de productividad.”
2. **Tendencia:** Comparar nextWeekRevenue (estimación total semana siguiente) con previousWeekRevenue. pct = (next - prev)/prev*100. Si pct &gt; 1: “Esperamos facturar un X% más que la semana anterior.” Si pct &lt; -1: “Esperamos facturar un X% menos.” Si no: “En línea con la semana anterior.”
3. **Clima:** Por cada día de la semana siguiente con previsión de lluvia (texto que contenga “lluvia”, “rain”, “Llovizna”, “drizzle”): añadir “Lluvia prevista: Lunes 08/02, …”. Si ninguno: “Sin días de lluvia previstos.”
4. **Festivos:** Solo si algún día tiene IsHoliday y HolidayName: “&lt;Nombre&gt; el &lt;Día&gt; DD/MM.”
5. **Misma semana, mes anterior:** Fechas de la semana siguiente (ej. 9–15 feb). Calcular mismas fechas numéricas en el mes anterior (9–15 ene). Sumar TotalRevenue de esos días. Texto: “Esperamos facturar un X% más/menos que los mismos días del mes pasado. El mes pasado, para esas fechas facturaste Y €.”
6. **Eventos / Obras:** Consultar Open Data BCN (y opcionalmente GuiaBCN) para eventos y obras en radio 300 m (y/o filtro por barrio/zona). Solo mostrar bloque si hay al menos un evento u obra. Listar nombres y fechas.

---

## 6. Versión de datos (refresco)

- Endpoint GET /api/recommendations/version devuelve un valor que cambia cuando se actualiza ExecutionDays, Recommendations o WeeklyPredictions (ej. max(UpdatedAt, CreatedAt)).
- La web/tablet puede llamarlo cada 60 s y, si cambia, recargar la vista de estimaciones.

---

## 7. Ajuste por lluvia en la estimación del día

- Si se tiene un patrón histórico “días lluviosos facturan Z% menos que soleados”, para cada día de la semana siguiente con previsión de lluvia aplicar un factor &lt; 1 a la estimación de ese día (ej. est * (1 - Z/100)).
- La previsión de lluvia viene de la integración de clima (Open-Meteo). Ver 08.

---

## 8. Referencia al código actual

- **InteligenciaService.cs:** GetNextWeekDiagramacionAsync (líneas ~262–438: fallback sin predicción guardada; enriquecimiento EnrichDaysWithWeatherAndHolidayAsync; GetSalaCocinaScheme, GetConfidenceLabel), GetAlertasDiagramacionAsync (tendencia, clima, festivos, misma semana mes anterior, eventos/obras), BuildNextWeekPredictionAsync (guardado en WeeklyPredictions).
- **EstimacionesController.cs:** GET/POST cache (archivo estimaciones-cache.json en LocalAppData).
- **DashboardController.cs:** GetWeek (avgRevenueHistoric, prevWeekRevenue, prevWeekProductivity).
