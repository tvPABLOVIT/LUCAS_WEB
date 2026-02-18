# Fuentes de datos, búsqueda de patrones, tendencias y predicción del futuro

Documento que explica **con lujo de detalle** todas las formas en que la pestaña de Estimaciones obtiene la información, cómo se buscan patrones y tendencias, y cómo se construye la predicción del futuro. Basado en `InteligenciaService`, `ConfiguracionService`, `EvaluatePredictionsAsync` y las integraciones externas (clima, festivos, eventos).

---

## 1. Resumen del flujo de información

La información que alimenta la pestaña de Estimaciones viene de:

1. **Base de datos (histórico):** Días de ejecución (`ExecutionDays` + `ShiftFeedbacks`) con facturación, horas, personal y feedback Q1–Q4; clima y festivo guardados por día; patrones y tendencias ya detectados (`DetectedPatterns`, `DetectedTrends`); predicciones guardadas (`WeeklyPredictions`).
2. **Análisis semanal (RunWeeklyAnalysisAsync):** Se ejecuta en segundo plano (al guardar Registro o al iniciar la app). A partir del histórico **completo** se detectan **patrones** (estacionales por día de la semana, impacto clima lluvioso, impacto festivos, impacto temperatura) y **tendencias** (facturación al alza/baja/estable, últimas 4 semanas vs 4 anteriores). Luego se **construye y guarda** la predicción de la semana siguiente en `WeeklyPredictions`.
3. **Evaluación de predicciones (EvaluatePredictionsAsync):** Cuando una semana ya pasó, se comparan predicción vs realidad por día y se **aprende** un sesgo por día de la semana (bias) y un error absoluto medio (MAE) por día de la semana. Ese aprendizaje se guarda en configuración (`PredictionBiasJson`, `PredictionMaeJson`) y se usa en la **siguiente** predicción para corregir y para las bandas min–max.
4. **Integraciones externas:** Clima (Open-Meteo) y festivos (Nager) para la **semana siguiente** (previsión y días festivos); eventos y obras (Open Data BCN, GuiaBCN) para las alertas "Eventos esta semana" y "Obras cerca".

A continuación se detalla cada bloque: qué datos se usan, cómo se filtran, cómo se calculan patrones y tendencias, cómo se aprende el sesgo y el MAE, y cómo se combina todo para predecir.

### Criterio conservador (hostelería)

La predicción sigue un **criterio conservador** alineado con la buena práctica de hostelería (“no pasarse”): evitar sobrestimar para no incurrir en exceso de personal, mermas y coste. En la implementación actual:

- **Tendencia simétrica:** `trendPct` se limita a **[-15, 15]** y `trendFactor = 1 + (trendPctClamped/100)*0,35` (puede ser &lt; 1 cuando la tendencia es a la baja).
- **Sin +1% fijo:** No se aplica ningún factor del tipo `dayAvg *= 1,01`.
- **Pendiente por DOW en ambos sentidos:** Si un día de la semana lleva tendencia a la baja, se reduce la predicción; si lleva tendencia al alza, se sube.
- **Base por DOW y overall reciente:** La media por día de la semana (`avgByDow`, `stdByDow`) y la media global usada para nivel reciente y estacionalidad mensual se calculan sobre los **últimos 84 días** (12 semanas). Si hay menos de 5 días en esa ventana, se usa todo el histórico. Así la predicción refleja el nivel actual del local.
- **Nivel reciente:** Se usan los **últimos 14 días** y el factor se limita a **[0,90 , 1,05]** (hasta -10% o +5%, nivelado para poder bajar más cuando el tramo reciente va bajo).
- **Bias por DOW:** El factor de corrección por sesgo aprendido está en **[0,93 , 1,07]** (simétrico: si se sobrestima se puede bajar hasta ~7%, si se subestima subir hasta ~7%). No hay suelo fijo en 0,99.
- **Semanas para tendencia:** Se exigen **≥ 6 días** por semana; si hay menos de 2 semanas así, se usa fallback **≥ 5 días**.
- **Enriquecimiento:** El producto de factores (clima, festivos, temperatura, eventos) se limita a **[0,90 , 1,06]** (hasta -10% o +6%) para que clima/festivos adversos puedan bajar más la predicción. **Eventos:** impacto ALTO = 1,10, BAJO = 0,90 (simétrico ±10% con blend 0,5).
- **Patrones:** Solo se guardan si hay **≥ 6 muestras** en cada grupo y **|pct_diff| ≥ 15%**. Los factores se leen en rango **[0,90 , 1,10]** y el blend se atenúa cuando la muestra es pequeña (&lt; 10).

---

## 2. Qué datos entran en el análisis (y cómo se filtran)

### 2.1. Días de ejecución (ExecutionDays + ShiftFeedbacks)

- **Origen:** Tablas `ExecutionDays` y `ShiftFeedbacks`. Cada día tiene: `Date`, `TotalRevenue`, `TotalHoursWorked`, `StaffTotal`, `Notes`, `WeatherCode`, `WeatherTemp`, `IsHoliday`, y por cada turno (Mediodía, Tarde, Noche): `Revenue`, `HoursWorked`, `StaffFloor`, `StaffKitchen`, `FeedbackQ1`–`FeedbackQ4`.
- **Restricción temporal:** Para la **predicción de la semana siguiente** solo se usan días **anteriores a la semana en curso**. Es decir: si hoy es miércoles 12 de febrero, la semana en curso es 10–16 feb; la semana siguiente es 17–23 feb. Los días que se usan para calcular patrones, tendencias y predicción son todos los que cumplen `Date < lunes de la semana en curso` (en el ejemplo, `Date < 10 feb`). **Por qué:** La semana en curso puede tener días aún sin registrar; si la incluyéramos, las medias y tendencias quedarían distorsionadas.
- **Día “financieramente completo” (IsFinanciallyComplete):** Un día solo se considera válido para patrones/tendencias/predicción si cumple **todas** estas condiciones:
  - `TotalRevenue > 0` y `TotalHoursWorked > 0`
  - Tiene **tres** turnos (Mediodía, Tarde, Noche) en `ShiftFeedbacks`
  - En **cada** turno: `Revenue > 0` y `HoursWorked > 0`
- **Por qué:** Queremos semanas y días con datos completos (facturación y horas por turno) para no mezclar días a medias con días completos en los promedios.

### 2.2. Semanas completas (GetCompleteWeeks)

- **Definición (en código):** Una semana se considera válida para tendencia y predicción si tiene **≥ 6 días** con datos (configurable; fallback **≥ 5** si con 6 hay menos de 2 semanas).
- **Cálculo:** Se agrupan los días por `GetWeekStartMonday(date)`. Se filtran grupos con `Count >= MinDaysPerWeekForTrend` (6); si resultan menos de 2 semanas, se usa `MinDaysPerWeekFallback` (5).
- **Uso:** Las **tendencias** se calculan con “últimas 4 semanas” vs “4 anteriores” (con ese criterio de días por semana). La **predicción** usa solo esas semanas para trend 4 vs 4 y medias por DOW.

### 2.3. Índice de día de la semana (DOW)

- **Convención en código:** Lunes = 0, Martes = 1, …, Domingo = 6 (en algunos sitios se usa `DayOfWeek` de .NET: Sunday = 0, Monday = 1, …, Saturday = 6; entonces se traduce: si `DayOfWeek == Sunday` entonces índice 6, si no `(int)DayOfWeek - 1`). Los arrays de **bias** y **MAE** por día de la semana tienen 7 posiciones: índice 0 = Lunes, …, 6 = Domingo.
- **Uso:** Casi toda la predicción se hace **por día de la semana**: el “Lunes” de la semana siguiente se estima a partir de los “Lunes” del histórico; igual para Martes, etc. **Por qué:** El patrón más estable es “qué día de la semana factura más o menos” (lunes vs sábado).

---

## 3. Búsqueda de patrones (DetectedPatterns)

Los **patrones** se detectan en **RunWeeklyAnalysisAsync**, una sola vez por ejecución del análisis semanal, y se guardan en la tabla `DetectedPatterns`. Luego la **predicción** los lee para aplicar factores de ajuste (lluvia, festivos, temperatura). Todos usan solo días **financieramente completos** y con `Date < lunes de la semana en curso`.

### 3.1. Patrones estacionales por día de la semana (tipo "Estacional")

- **Qué se busca:** Para cada día de la semana (Lunes, Martes, …, Domingo), la **facturación media** y la **desviación típica** sobre el histórico completo (todos los días que son ese día de la semana).
- **Cálculo exacto:**
  - Se filtra `completeHistoricalDays` por `Date.DayOfWeek == dayOfWeek` (por ejemplo todos los lunes).
  - Si hay **menos de 2** días, no se crea patrón para ese DOW.
  - `avg = media(TotalRevenue)` de esos días.
  - `std = sqrt(media((TotalRevenue - avg)²))` si hay al menos 3 días; si no, std = 0.
  - Se guarda un `DetectedPattern` con: Type = "Estacional", Name = "Facturación Lunes" (o Martes, etc.), Description = "Facturación media los Lunes: X € (N registros)", Confidence = min(95, 50 + N), Severity = "Alta" si Confidence ≥ 80, "Media" si ≥ 60, "Baja" si no. Payload = JSON con `avg_revenue`, `std_dev`, `count`. ValidFrom = fecha mínima de esos días.
- **Uso en la predicción:** No se lee directamente de `DetectedPatterns` para la “base” del día; la base por DOW se calcula en el mismo `BuildNextWeekPredictionAsync` con los últimos días históricos de ese DOW (véase más abajo). Los patrones estacionales sirven sobre todo para **consulta** (pantalla Patrones) y para tener avg/std por DOW disponibles; la lógica de predicción usa `last4WeeksByDow` (últimos días por DOW) y opcionalmente podría usar estos patrones si se quisiera. En el código actual la **media por DOW** en la predicción se obtiene de `hist` (histórico del mismo DOW, ordenado por fecha descendente, hasta 8 días) y se aplica una **media ponderada** (decay + feedback); no se lee el patrón "Facturación Lunes" para ese valor.

### 3.2. Patrón “Impacto clima lluvioso” (tipo "Operativo")

- **Qué se busca:** Si los días **lluviosos** del histórico facturan de media más o menos que los días **soleados**, y en qué porcentaje.
- **Clasificación de días:**
  - **Lluviosos:** Días con `WeatherCode` (WMO) en: 51–67 (llovizna, lluvia), 71–77 (nieve, granizo), 80–82 (chubascos), 95–96 (tormenta). Es decir, cualquier código que indique precipitación o tormenta.
  - **Soleados:** Códigos 0, 1, 2 (despejado, mayormente despejado, parcialmente nublado).
- **Condición:** Solo se crea o actualiza el patrón si hay **al menos 6 días lluviosos** y **al menos 6 días soleados** (criterio conservador: más muestras para evitar ruido).
- **Cálculo:**  
  - `avgRainy = media(TotalRevenue)` de los días lluviosos.  
  - `avgSunny = media(TotalRevenue)` de los días soleados.  
  - `pctDiff = (avgRainy - avgSunny) / avgSunny * 100`.  
  - Solo se guarda si `|pctDiff| >= 15` (diferencia de al menos 15 %).
- **Guardado:** Payload = JSON con `avg_rainy`, `avg_sunny`, `pct_diff`, `rainFactor`, `count_rainy`, `count_sunny` (los counts permiten atenuar el blend cuando la muestra es pequeña en el enriquecimiento).
- **Uso en la predicción:** Se lee el patrón "Impacto clima lluvioso". `rainFactor = 1 + (pct_diff/100)`, limitado a **[0,90 , 1,10]** (simétrico). **rainBlend** = 0,25 + (Confidence/100)*0,5; si el payload tiene `count_rainy` y `count_sunny`, se multiplica el blend por `min(1, minCount/10)` (mínimo 0,5) para no aplicar factores fuertes con pocos datos. El producto final de factores del día se limita a **[0,94 , 1,06]** (±6%).

### 3.3. Patrón “Impacto festivos” (tipo "Operativo")

- **Qué se busca:** Si los días **festivos** del histórico facturan de media más o menos que los **no festivos**, y en qué porcentaje.
- **Clasificación:** Días con `IsHoliday == true` vs `IsHoliday == false` en `completeHistoricalDays`.
- **Condición:** Al menos **6** días festivos y **6** no festivos. Solo se guarda si `|pctDiffHoliday| >= 15`.
- **Guardado:** Payload con `pct_diff`, `holidayFactor`, `count_holiday`, `count_not_holiday`.
- **Uso en la predicción:** `holidayFactorDefault` limitado a **[0,90 , 1,10]**. Blend atenuado por muestra (count_holiday, count_not_holiday) igual que en lluvia. Producto final del día en [0,94 , 1,06].

### 3.4. Patrón “Impacto temperatura” (tipo "Operativo")

- **Qué se busca:** Si los días con **temperatura extrema** (muy frío o muy calor) facturan de media más o menos que los días **suaves**.
- **Clasificación:**
  - **Extremos:** Días con `WeatherTemp == null` o `WeatherTemp < 5` o `WeatherTemp > 30` (grados Celsius).
  - **Suaves:** `WeatherTemp` entre 15 y 25 °C.
- **Condición:** Al menos **6** días extremos y **6** suaves. Solo se guarda si |pctDiffTemp| ≥ **15**.
- **Guardado:** Payload con `pct_diff`, `tempFactor`, `count_extreme`, `count_mild`.
- **Uso en la predicción:** `tempExtremeFactor` limitado a **[0,90 , 1,10]**. Blend atenuado por muestra. Producto final del día en [0,94 , 1,06].

---

## 4. Detección de tendencias (DetectedTrends)

- **Qué se busca:** Si la **facturación** en las **últimas 4 semanas completas** está al alza, a la baja o estable respecto a las **4 semanas completas anteriores**.
- **Datos:** `completeWeeks` (semanas completas ordenadas por fecha descendente). `last4Weeks` = primeras 4, `prev4Weeks` = siguientes 4. `last4Days` = todos los días de last4Weeks, `prev4Days` = todos los días de prev4Weeks.
- **Cálculo:**  
  - `last4 = suma(TotalRevenue)` de last4Days.  
  - `prev4 = suma(TotalRevenue)` de prev4Days.  
  - Si prev4 > 0 y last4 > 0:  
    - `pct = (last4 - prev4) / prev4 * 100`.  
    - `direction = "up"` si pct > 5, `"down"` si pct < -5, `"stable"` si no.  
    - `slope = (last4 - prev4) / max(1, last4Weeks.Count)` (€/semana, pendiente lineal).  
  - Se guarda o actualiza un **DetectedTrend** con: Metric = "Facturación", Direction, StrengthPercent = |pct|, Slope, FromDate = lunes de la primera semana de last4Weeks, ToDate = domingo de la última semana de last4Weeks. Payload con weeks_analyzed, avg_start, avg_end, total_growth_pct.
- **Limpieza:** Antes de crear tendencias nuevas, se eliminan tendencias duplicadas (mismo Metric, FromDate, ToDate), dejando solo la más reciente por grupo.
- **Uso en la predicción:** **trendPct** = (last4 - prev4) / prev4 * 100. Se limita a **[-15, 15]** (simétrico). **trendFactor = 1 + (trendPctClamped/100)*0,35** (puede ser &lt; 1 cuando la tendencia es a la baja). Así la predicción reacciona igual ante subidas y bajadas (criterio conservador). El **nivel reciente** usa los **últimos 14 días** y un factor en **[0,90 , 1,05]**. La **base por DOW** y el **overall** para nivel reciente y mes se calculan con ventana de 84 días (véase criterio conservador). No se aplica ningún +1% fijo. La **pendiente por DOW** se aplica en ambos sentidos (subida y bajada).

---

## 5. Aprendizaje del sesgo y del MAE por día de la semana (EvaluatePredictionsAsync)

Cuando una **semana ya pasó**, se compara la predicción que teníamos guardada con la **realidad** (ExecutionDays de esa semana). Eso permite aprender un **sesgo** (si solemos sobrestimar o subestimar por día de la semana) y un **error absoluto medio (MAE)** por día de la semana.

### 5.1. Cuándo se ejecuta

- **EvaluatePredictionsAsync** se llama al inicio de **RunFullBackgroundAnalysisAsync** y también al inicio de **RunWeeklyAnalysisAsync**.
- Busca una **WeeklyPrediction** cuya semana sea la **semana pasada** (domingo pasado = último domingo completo) y que aún no tenga `CompletedAt` (no evaluada). Si no hay tal predicción, no hace nada.

### 5.2. Cálculo de lo real

- Se cargan todos los `ExecutionDays` con `Date` entre el lunes y el domingo de esa semana.
- `actualTotal = suma(TotalRevenue)`, `actualStaffHours = suma(TotalHoursWorked)`.
- Se actualiza la predicción: `ActualRevenue = actualTotal`, `CompletedAt = now`.

### 5.3. Error global y métricas

- `errorPct = |PredictedRevenue - actualTotal| / PredictedRevenue * 100`.  
- `accuracy = max(0, 100 - errorPct)`.  
- Si hay EstimatedStaffHours y actualStaffHours, se calcula `staffConfidence` (100 - error % de horas).  
- Todo se guarda en `AccuracyMetricsJson` (overall_error_percent, accuracy_percent, days_in_range, staff_estimated, staff_actual, staff_confidence_percent, etc.).

### 5.4. Por cada día de la semana pasada: aprendizaje de bias y MAE

- Se recorre el array de la predicción diaria (`DailyPredictionsJson`). Para cada día:
  - Se obtiene la fecha, revenue predicho, min, max.
  - Se busca el día real en `ExecutionDays` para esa fecha; `realRev = TotalRevenue` (o 0 si no existe).
  - **Dentro del rango:** se cuenta si realRev está entre min y max (inRange++).
  - **Sesgo por DOW:** si hay configuración (`_config`) y la fecha es válida:  
    - `dowIndex` = 0 para Lunes, …, 6 para Domingo.  
    - `dayErrorPct = (predRev - realRev) / predRev * 100` (positivo = sobrestimamos, negativo = subestimamos).  
    - Se llama **UpdatePredictionBiasForDayOfWeek(dowIndex, dayErrorPct)**.
  - **MAE por DOW:** **UpdatePredictionMaeForDayOfWeek(dowIndex, |predRev - realRev|)** (error absoluto en euros).

### 5.5. Cómo se actualiza el bias (ConfiguracionService.UpdatePredictionBiasForDayOfWeek)

- En `settings.json` se guarda un JSON con dos arrays: `avg` (7 números) y `count` (7 enteros), uno por día de la semana.
- Para el `dowIndex` dado:  
  - `n = count[dowIndex]` (cuántas veces hemos evaluado ya ese día de la semana).  
  - **Nueva media:** `avg[dowIndex] = (avg[dowIndex] * n + errorPct) / (n + 1)`.  
  - `count[dowIndex] = n + 1`.  
- Es decir: **media móvil** del error porcentual por día de la semana. Si los lunes hemos sobrestimado un 5 % de media, avg[0] ≈ 5; si los sábados hemos subestimado un 10 %, avg[5] ≈ -10.

### 5.6. Cómo se actualiza el MAE (ConfiguracionService.UpdatePredictionMaeForDayOfWeek)

- En `settings.json` se guarda otro JSON: `avg_mae` (7 números) y `count` (7 enteros).
- Para el `dowIndex`:  
  - `avgMae[dowIndex] = (avgMae[dowIndex] * n + |predRev - realRev|) / (n + 1)`,  
  - `count[dowIndex] = n + 1`.  
- Es la **media móvil del error absoluto en euros** por día de la semana. Sirve para las **bandas** min–max: un día con MAE alto tendrá banda más ancha.

### 5.7. Uso del bias y del MAE en la predicción

- **Bias:** En NextWeekPredictionService, para cada día de la semana siguiente se obtiene `bias = biasByDow[dowIndex]` (en %). Se limita a **[-20, 20]** (biasClamped). **biasFactor = clamp(1 - (biasClamped/100)*0,35 , 0,93 , 1,07)**. Si solemos sobrestimar los lunes (bias positivo), biasFactor puede bajar hasta 0,93 (~7% de corrección a la baja); si subestimamos, puede subir hasta 1,07. Simétrico para nivelar subida y bajada. **Por qué:** Corregir errores sistemáticos por día de la semana.
- **MAE:** A la hora de calcular las **bandas** min–max del día, se usa `halfBand = max(1,5 * std, 1,5 * learnedMae)`. Si tenemos un MAE aprendido alto para ese DOW, la banda se ensancha. **Por qué:** Reflejar que ese día de la semana ha sido más impredecible en el pasado.

### 5.8. Evaluación automática (PredictionEvaluationHostedService)

- Un **HostedService** en segundo plano ejecuta **una vez al día** (tras un delay inicial de unos segundos): `EvaluateLastWeekIfPendingAsync()` (evalúa la semana pasada y actualiza bias/MAE en Settings) y después `ComputeAndSavePatternsAsync()` (recalcula patrones lluvia, festivos, temperatura). Así el bias y los patrones se mantienen actualizados sin depender de que el usuario llame a "Evaluar predicción" o "Calcular patrones" desde la API.

### 5.9. Ventana en bias y MAE (últimas 12 evaluaciones por DOW)

- El bias y el MAE por día de la semana se almacenan con una **ventana móvil** de las últimas **12** evaluaciones por DOW (en Settings: `PredictionBiasJson` y `PredictionMaeJson` incluyen `recent_0` … `recent_6` con hasta 12 valores cada uno). La media usada en la predicción es la media de esa ventana. Así el sistema se adapta a cambios recientes del local y no arrastra errores muy antiguos.

### 5.10. Factor predicción conservadora (Setting)

- Si en Configuración se define **PrediccionConservadoraFactor** (valor entre 0,01 y 1, por ejemplo 0,97), todas las predicciones (total y por día: revenue, min, max, turnos) se multiplican por ese factor. Sirve para bajar de forma uniforme la predicción cuando el usuario prefiere ser más conservador.

---

## 6. Cómo se construye la predicción día a día (BuildNextWeekPredictionAsync)

Aquí se junta todo: base por DOW, tendencia, nivel reciente, estacionalidad por mes, sesgo aprendido, factores operativos (lluvia, festivos, temperatura, eventos) y pendiente por DOW. Los pasos son los siguientes (resumidos y luego detallados).

### 6.1. Preparación de datos

- **Semana siguiente:** `nextMonday` = primer lunes estrictamente después de hoy.
- **Histórico válido:** `historicDays` (días con Date < lunes semana actual, TotalRevenue > 0, TotalHoursWorked > 0), agrupados en semanas completas para tendencia; **recentHistoricDays** = últimos 84 días de historicDays (ventana para base y overall).
- **trendPct** = (last4 - prev4) / prev4 * 100; **trendPctClamped** = Clamp(trendPct, -15, 15) (simétrico).
- **biasByDow**, **maeByDow** desde configuración (aprendizaje previo).
- **overallAvg** = media(TotalRevenue) de historicDays; **overallAvgRecent** = media de recentHistoricDays (si hay ≥ 5 días en ventana), si no se usa overallAvg. **daysForBase** = recentHistoricDays si hay ≥ 5 días, si no historicDays.
- **recentDays** = últimos 14 días (ordenados por fecha descendente). **recentLevelFactor** = media(recentDays) / overallAvgRecent, limitado a [0,90, 1,05].
- **avgByDow, stdByDow:** calculados a partir de **daysForBase** agrupado por DOW (ventana reciente cuando hay suficientes datos).
- **byMonth:** media por mes de daysForBase; **monthAvg** = monthRev / overallForBase, limitado [0,9, 1,1].
- **last4ByDow:** Para cada DOW, días de last4Weeks de ese DOW, ordenados por fecha descendente.
- **dowTrendSlope[7]:** Para cada DOW, si hay al menos 2 días en last4ByDow, pendiente (más reciente − más antiguo) / (n−1). La pendiente se aplica en **ambos sentidos** con coeficiente 0,35.

### 6.2. Factores operativos por día (lluvia, festivos, temperatura, eventos)

- **weatherFactors[7], holidayFactors[7], tempFactors[7]:** Inicializados a 1,0.
- **rainFactor, rainBlend:** Del patrón "Impacto clima lluvioso" (véase 3.2). Para cada día k de la semana siguiente se llama a ClimaService; si el código es de lluvia, weatherFactors[k] = rainFactor.
- **holidayFactorDefault, holidayBlend:** Del patrón "Impacto festivos". Para cada día k se llama a FestivosService; si es festivo, holidayFactors[k] = holidayFactorDefault.
- **tempExtremeFactor, tempBlend:** Del patrón "Impacto temperatura". Si la temperatura prevista (TempMax/TempMin) es < 5 °C o > 30 °C, tempFactors[k] = tempExtremeFactor.
- **eventFactor:** De la tabla `Events` (eventos internos con fecha e impacto). Si ese día tiene un evento con Impact = "Alto", factor 1,10; si "Bajo", 0,90 (simétrico); si no, 1,0. Se aplica blend 0,5 en el enriquecimiento.

### 6.3. Por cada día de la semana siguiente (0..6)

- **dow** = día de la semana (Lunes, …, Domingo), **dowIndex** = 0..6.
- **hist** = last4WeeksByDow[dow]: lista de días históricos del mismo DOW (máx. 8), ordenados del más reciente al más antiguo.

**Base (dayAvg) y bandas (dayMin, dayMax):**

- Si **hist.Count > 0:**
  - **Pesos por día:** Para cada día en hist se asigna un peso = (1 - idx*decay) * (0,92 + 0,08 * GetFeedbackStrength(e)). `decay` = 0,05 para Lunes, 0,1 para el resto (los días más recientes pesan más). **GetFeedbackStrength(e)** devuelve un valor entre 0,2 y 1,0 según las respuestas Q1–Q4 del día: más “actividad” reportada (V, R altos; M bajo; D alto) da más peso. Así los días con feedback de mucha carga pesan un poco más en la media.
  - **dayAvg** = suma(TotalRevenue * peso) / suma(pesos).
  - **std** = desviación típica de TotalRevenue en hist (o 0,15*dayAvg si hay menos de 3).
  - **learnedMae** = maeByDow[dowIndex].
  - **halfBand** = max(1,5 * std, 1,5 * learnedMae). **dayMin** = max(0, dayAvg - halfBand), **dayMax** = dayAvg + halfBand; luego se fuerzan límites 0,85*dayAvg y 1,15*dayAvg.
- Si hist está vacío: dayAvg = overallAvg, dayMin = dayAvg*0,85, dayMax = dayAvg*1,15.

**Estacionalidad por mes:**

- **monthAvg** = (media de TotalRevenue de los días de completeHistoricalDays que son del mismo **mes** que el día d) / overallAvg. Limitado a [0,9, 1,1]. **monthFactor** = 1 + (monthAvg - 1)*0,4. **Por qué:** Algunos meses son más fuertes o más flojos; no queremos que el factor sea demasiado agresivo (por eso 0,4).

**Combinación de factores:**

- **trendFactor** = 1 + (trendPctClamped/100)*0,35 (puede ser &lt; 1 cuando la tendencia es a la baja).  
- **biasFactor** = max(0,99, 1 - (biasClamped/100)*0,35).  
- **recentFactor** = recentLevelFactor (ya limitado a [0,97, 1,05]).  
- **weatherFactor**, **holidayFactor**, **tempFactor**, **eventFactor** como antes; el **producto** de todos los factores aplicado en enriquecimiento se limita a **[0,94, 1,06]** (±6%).  
- **slope** = dowTrendSlope[dowIndex] * 0,35 (positivo o negativo).  
- **dayAvg** = (base por DOW * trendFactor * recentLevelFactor * monthAvg * biasFactor) + slope; **no** se aplica +1% fijo. dayMin y dayMax según bandas (halfBand); se limitan a 0,85*dayAvg y 1,15*dayAvg.

**Por turno (Mediodía, Tarde, Noche):**

- Para cada turno se toma el histórico de ese turno en hist (ShiftFeedbacks con ese ShiftName). **shiftRev** = media(Revenue) de ese histórico, o dayAvg/3 si no hay datos. Se aplican trendFactor y biasFactor. **staff** = techo(shiftRev / (productividadIdeal * horasPorTurno)); si hay al menos 2 días con personal real (StaffFloor+StaffKitchen) en ese turno, se refina: staff = 0,6*staff + 0,4*avgHistStaff (mínimo 1). La suma de revenue por turno se escala al dayAvg si no cuadra. Se guarda en DailyPredictionsJson (date, revenue, min, max, mediodia/tarde/noche con revenue y staff).

### 6.4. EstimatedStaffHours y persistencia

- **EstimatedStaffHours** = suma sobre los 7 días de (staff por turno × horasPorTurno) para cada turno.
- Se guarda o actualiza **WeeklyPrediction** para nextMonday: PredictedRevenue = suma de dayAvg, DailyPredictionsJson, HistoricalStatsJson, EstimatedStaffHours.

---

## 7. GetFeedbackStrength (peso del feedback V/R/M/D en la media)

- **Objetivo:** Dar un valor entre 0,2 y 1,0 que represente “cuánta actividad o demanda” hubo ese día según las respuestas Q1–Q4 de sus turnos. Se usa para **ponderar** los días en la media por DOW: los días con más “carga” reportada pesan hasta un 8 % más.
- **Cálculo:** Por cada turno con al menos un Q rellenado: se obtienen V, R, M, D (índices 1–5; si falta, se usa 3 como neutro). **sum += (V + R + (6-M) + D) / 20**. Se promedia entre turnos y se limita a [0,2, 1,0]. Si no hay feedback, 0,5. **Por qué:** Un día con mucho volumen y ritmo y poco margen y mucha dificultad (alto “estrés”) puede ser más representativo del potencial de facturación; darle un poco más de peso en la media refina la base por DOW.

---

## 8. Resumen: de dónde sale cada tipo de información en Estimaciones

| Tipo de información | Dónde se obtiene | Cómo se usa en la predicción o en la vista |
|---------------------|------------------|--------------------------------------------|
| **Base por día de la semana** | Histórico (ExecutionDays) filtrado por DOW; media ponderada con decay y GetFeedbackStrength | dayAvg por día de la semana siguiente |
| **Desviación típica por DOW** | Mismo histórico; std(TotalRevenue) | Bandas min–max (halfBand = max(1,5*std, 1,5*MAE)) |
| **Tendencia 4 vs 4** | Suma revenue last4Weeks vs prev4Weeks | trendFactor simétrico (Clamp -15 % a +15 %); puede &lt; 1 |
| **Nivel reciente** | Últimos 14 días; media / overallAvgRecent | recentFactor [0,90, 1,05] |
| **Estacionalidad por mes** | Días del mismo mes en histórico; media(mes)/overallAvg | monthFactor |
| **Patrón lluvia** | DetectedPatterns "Impacto clima lluvioso"; pct_diff histórico lluvia vs sol | rainFactor + pronóstico por día → weatherFactor |
| **Patrón festivos** | DetectedPatterns "Impacto festivos"; pct_diff festivos vs no festivos | holidayFactorDefault + FestivosService por día → holidayFactor |
| **Patrón temperatura** | DetectedPatterns "Impacto temperatura"; pct_diff extremos vs suaves | tempExtremeFactor + pronóstico por día → tempFactor |
| **Eventos internos** | Tabla Events por fecha; impacto Alto/Medio/Bajo | eventFactor por día |
| **Sesgo aprendido (bias)** | EvaluatePredictionsAsync; ventana últimas 12 evaluaciones por DOW (Settings PredictionBiasJson) | biasFactor por DOW [0,93, 1,07] |
| **MAE aprendido** | EvaluatePredictionsAsync; ventana últimas 12 evaluaciones por DOW (Settings PredictionMaeJson) | halfBand en bandas min–max |
| **Factor conservadora** | Setting PrediccionConservadoraFactor (0,01–1) en Configuración | Multiplica toda la predicción (total y por día) si está configurado |
| **Historial precisión** | GET api/predictions/accuracy-history (WeeklyPredictions con CompletedAt) | Métricas por semana: predictedRevenue, actualRevenue, errorPercent, accuracyPercent |
| **Pendiente por DOW** | Últimos días por DOW en last4WeeksByDow; (último - primero)/n | slope sumado a dayAvg (solo si > 0) |
| **Clima semana siguiente** | ClimaService.GetWeatherForDateAsync (Open-Meteo) | weatherFactors, descripción en vista |
| **Festivos semana siguiente** | FestivosService.GetHolidayInfoAsync (Nager) | holidayFactors, nombre en vista |
| **Semana anterior (datos reales)** | ExecutionDays de la semana pasada | Bloque "Semana anterior" en alertas |
| **Tendencia (% más/menos)** | Comparación nextWeekRevenue vs previousWeekRevenue | Bloque "Tendencia" en alertas |
| **Misma semana mes anterior** | ExecutionDays de las mismas fechas numéricas del mes pasado | Bloque "Misma semana, mes anterior" en alertas |
| **Eventos / Obras** | Events + OpenDataBcnService (300 m) + GuiaBCN | Bloques "Eventos esta semana" y "Obras cerca" (solo si hay datos) |

---

## 9. Orden de ejecución y dependencias

1. **Al guardar un día en Registro** (o al iniciar la app): RunFullBackgroundAnalysisAsync.  
2. **EvaluatePredictionsAsync:** Evalúa la semana pasada si hay predicción sin evaluar; actualiza bias y MAE por DOW en configuración.  
3. **RunWeeklyAnalysisAsync:** Limpia tendencias/recomendaciones duplicadas; calcula patrones (Estacional, Clima, Festivos, Temperatura); calcula tendencia (4 vs 4) y la guarda en DetectedTrends; construye la predicción de la semana siguiente (BuildNextWeekPredictionAsync) usando patrones, tendencia, bias, MAE, clima y festivos de la semana siguiente, y la guarda en WeeklyPredictions; crea recomendaciones si la tendencia varía ≥ 5 %.  
4. **Al abrir la pestaña Estimaciones:** GetNextWeekDiagramacionAsync lee la predicción de WeeklyPredictions (o la calcula en vivo si no hay o está vacía); GetAlertasDiagramacionAsync construye los bloques de alertas con datos de BD y de la predicción ya cargada.

Con esto quedan cubiertas **todas** las formas en que se obtiene la información (BD, patrones, tendencias, aprendizaje bias/MAE, clima, festivos, eventos) y **cómo** se combinan para predecir el futuro (facturación por día y por turno, bandas, personal sugerido) y para mostrar las alertas de la pestaña Estimaciones.
