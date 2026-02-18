# Análisis: Formas de predicción para estimaciones realistas

**Fecha:** Febrero 2026  
**Objetivo:** Resumir todas las vías que usa ManagerOS para obtener predicciones y estimaciones realistas de la semana siguiente (facturación, personal, bandas de confianza).

---

## 1. Resumen ejecutivo

El sistema combina **varias fuentes y métodos** para llegar a una estimación realista:

| Origen | Qué aporta | Dónde se usa |
|--------|------------|--------------|
| **Histórico (semanas completas)** | Base por día de la semana (L–D), desviación, medias por turno | Predicción día a día |
| **Tendencia 4 vs 4** | Si la facturación va al alza/baja (últimas 4 vs 4 anteriores) | Factor de ajuste (solo al alza) |
| **Aprendizaje (bias y MAE)** | Sesgo y error por día de la semana tras evaluar predicciones pasadas | Corrección y bandas min–max |
| **Patrones operativos** | Impacto lluvia, festivos, temperatura aprendido del histórico | Factores por día según pronóstico |
| **Nivel reciente y estacionalidad** | Últimos 7 días y mes | Factores moderados |
| **Eventos y clima/festivos externos** | Pronóstico semana siguiente y calendario | Factores y alertas |
| **Predicción guardada** | Resultado de RunWeeklyAnalysisAsync (completa) | Vista si existe; si no, fallback en vivo |

Hay **dos flujos** de predicción:

1. **Predicción completa (RunWeeklyAnalysisAsync):** Se ejecuta en segundo plano (al guardar Registro o al iniciar). Usa histórico + patrones + clima/festivos/eventos de la semana siguiente, aplica todos los factores y **guarda** en `WeeklyPredictions`. Es la vía más rica.
2. **Fallback en vivo (GetNextWeekDiagramacionAsync):** Si no hay predicción guardada o está vacía, se calcula **en el momento** solo con histórico (tendencia, sesgo, nivel reciente, estacionalidad, pendiente por DOW). **No** aplica en ese cálculo clima/festivos/eventos a la cifra (sí se enriquecen los días para mostrar descripción de clima y festivos).

Para una **estimación lo más realista posible**, conviene que exista predicción guardada (es decir, que se haya ejecutado el análisis semanal con clima, festivos y eventos).

---

## 2. Formas concretas de predicción

### 2.1. Base por día de la semana (DOW)

- **Fuente:** Días históricos **financieramente completos** del mismo día de la semana (Lunes, Martes, …, Domingo).
- **Criterio “completo”:** `TotalRevenue > 0`, `TotalHoursWorked > 0`, tres turnos con `Revenue` y `HoursWorked > 0`.
- **Cálculo:**
  - Se toman las **últimas 4 semanas completas**; por DOW, hasta 8 días ordenados por fecha descendente.
  - **Media ponderada:** peso = (1 − idx×decay) × (0,92 + 0,08 × GetFeedbackStrength).  
    - Decay: 0,05 Lunes, 0,1 resto. Los días más recientes pesan más.  
    - GetFeedbackStrength (V/R/M/D) hace que días con más “actividad” pesen hasta un 8 % más.
  - Si no hay histórico para ese DOW, se usa la media global de todos los días completos.
- **Uso:** Es el **dayAvg** inicial por día de la semana siguiente. Es la base sobre la que se aplican el resto de factores.

### 2.2. Tendencia (últimas 4 vs 4 anteriores)

- **Fuente:** Suma de facturación de **últimas 4 semanas completas** vs **4 anteriores**.
- **Cálculo:** `trendPct = (last4 − prev4) / prev4 × 100`, limitado a **[-5, 25]**.
- **Factor:** `trendFactor = max(1, 1 + (trendPct/100)×0,35)`. Solo se aplica la parte positiva (si la tendencia es negativa, no se baja más allá del límite).
- **Uso:** Ajuste de la estimación para reflejar “momentum” reciente sin castigar en exceso una mala racha.

### 2.3. Sesgo aprendido por día de la semana (bias)

- **Fuente:** **EvaluatePredictionsAsync**: cuando una semana ya pasó, se compara predicción vs realidad por día. Para cada DOW se actualiza una **media móvil del error porcentual** (sobrestimamos → bias positivo, subestimamos → negativo).
- **Persistencia:** Configuración `PredictionBiasJson` (arrays avg y count por DOW).
- **Cálculo en predicción:** `biasFactor = max(0,99, 1 − (biasClamped/100)×0,35)`, con bias limitado a [-20, 20].
- **Uso:** Corregir errores sistemáticos por día (ej. “los lunes siempre los sobrestimamos” → bajar un poco el próximo lunes).

### 2.4. MAE aprendido por día de la semana

- **Fuente:** Misma evaluación; para cada DOW se actualiza la **media móvil del error absoluto en euros**.
- **Persistencia:** Configuración `PredictionMaeJson`.
- **Cálculo en predicción:** `halfBand = max(1,5×std, 1,5×learnedMae)`; con eso se definen min y max del día.
- **Uso:** Ensanchar la banda de confianza en días de la semana que han sido más impredecibles.

### 2.5. Nivel reciente (últimos 7 días)

- **Fuente:** Media de facturación de los **últimos 7 días** completos vs media global.
- **Cálculo:** `recentLevelFactor = media(recent7) / overallAvg`, limitado a [0,95, 1,1].  
  `recentFactor = max(0,99, 1 + (recentLevelFactor − 1)×0,4)`.
- **Uso:** Ajustar si el nivel reciente está por encima o por debajo del promedio, sin que un mal tramo baje demasiado la predicción.

### 2.6. Estacionalidad por mes

- **Fuente:** Media de facturación de los días del **mismo mes** en el histórico vs media global.
- **Cálculo:** `monthAvg = media(mes) / overallAvg`, limitado a [0,9, 1,1].  
  `monthFactor = 1 + (monthAvg − 1)×0,4`.
- **Uso:** Reflejar que algunos meses suelen ser más fuertes o más flojos.

### 2.7. Pendiente por día de la semana (DOW slope)

- **Fuente:** Para cada DOW, últimos días en last4WeeksByDow; pendiente lineal (diferencia entre el más reciente y el más antiguo) / (n−1).
- **Cálculo:** `slope = (último − primero) / max(1, n−1)`; solo se usa si slope > 0 (×0,35).
- **Uso:** Término **aditivo** a la estimación del día cuando ese día de la semana lleva tendencia al alza en el histórico reciente.

### 2.8. Patrones operativos (lluvia, festivos, temperatura)

- **Fuente:** Tabla `DetectedPatterns` (tipo "Operativo"), calculados en **RunWeeklyAnalysisAsync** a partir del histórico:
  - **Impacto clima lluvioso:** media facturación días lluviosos vs soleados → `pct_diff`.  
    Por día de la semana siguiente: si el pronóstico dice lluvia → `rainFactor = 1 + pct_diff/100` (limitado 0,88–1,08).  
    Peso: `rainBlend = 0,25 + (Confidence/100)×0,5` (0,25–0,75).
  - **Impacto festivos:** media festivos vs no festivos → `pct_diff`.  
    Si el día es festivo (FestivosService) → `holidayFactorDefault`; peso `holidayBlend` igual que rainBlend.
  - **Impacto temperatura:** media días temp extrema (<5°C o >30°C) vs suaves (15–25°C).  
    Si pronóstico indica temp extrema → `tempExtremeFactor`; peso `tempBlend`.
- **Uso:** Solo en la **predicción guardada** (RunWeeklyAnalysisAsync). Cada día de la semana siguiente recibe weatherFactor, holidayFactor, tempFactor según pronóstico y calendario. El producto total de factores se limita a **[0,96 – 1,12]**.

### 2.9. Eventos (tabla Events)

- **Fuente:** Eventos con `EventDate` en la semana siguiente e impacto "Alto" / "Bajo".
- **Cálculo:** Por día: impacto "Alto" → 1,15; "Bajo" → 0,98; si no → 1,0.  
  `eventFactor = 1 + (eventFactors[i] − 1)×0,5`.
- **Uso:** Solo en la predicción guardada. Ajuste por eventos conocidos que pueden subir o bajar la facturación.

### 2.10. Bandas min–max (incertidumbre)

- **Fuente:** Desviación típica por DOW del histórico y, si existe, MAE aprendido.
- **Cálculo:** `halfBand = max(1,5×std, 1,5×learnedMae)`; min = dayAvg − halfBand, max = dayAvg + halfBand; luego se fuerzan límites 0,85×dayAvg y 1,15×dayAvg (y análogo con MAE por DOW en configuración).
- **Uso:** Rango de confianza por día; etiqueta de confianza en la vista según anchura de la banda.

### 2.11. Corrección central (+1 %)

- **Cálculo:** Al final, `dayAvg *= 1,01` (y coherencia en min/max).
- **Uso:** Compensar una ligera tendencia histórica a subestimar.

### 2.12. Por turno (Mediodía, Tarde, Noche)

- **Facturación por turno:** Media histórica de `ShiftFeedback.Revenue` para ese DOW y turno; se aplican trend y bias; se escala la suma al dayAvg del día para que cuadre.
- **Personal sugerido:** `staff = ceil(shiftRev / (productividadIdeal × horasPorTurno))`. Si hay al menos 2 días con personal real (StaffFloor+StaffKitchen) en ese turno: `staff = 0,6×staff + 0,4×avgHistStaff` (mínimo 1).
- **Horas estimadas:** Suma de (staff por turno × horasPorTurno) para los 7 días → `EstimatedStaffHours`.

---

## 3. Dónde se aplica cada cosa

| Método / factor | Predicción guardada (RunWeeklyAnalysisAsync) | Fallback en vivo (GetNextWeekDiagramacionAsync) |
|-----------------|----------------------------------------------|--------------------------------------------------|
| Base por DOW (media ponderada + feedback) | Sí | Sí (media por DOW, sin ponderación feedback en el fallback actual) |
| Tendencia 4 vs 4 | Sí | Sí |
| Bias por DOW | Sí | Sí |
| MAE por DOW | Sí (bandas) | Sí (bandas) |
| Nivel reciente | Sí | Sí |
| Estacionalidad por mes | Sí | Sí |
| Pendiente por DOW | Sí | Sí |
| Patrones lluvia/festivos/temperatura | Sí (con pronóstico) | No (solo enriquecimiento visual) |
| Eventos (Events) | Sí | No |
| Clima/festivos para vista | Sí (EnrichDaysWithWeatherAndHolidayAsync) | Sí |
| Producto de factores limitado [0,96–1,12] | Sí | Sí (solo trend/bias/recent/month) |
| Corrección central +1 % | Sí | Sí |

**Conclusión:** La **estimación más realista** se obtiene cuando existe **predicción guardada**, porque incluye clima, festivos, temperatura y eventos. El fallback es útil cuando aún no se ha ejecutado el análisis semanal o no hay suficientes semanas completas.

---

## 4. Flujo recomendado para estimación realista

1. **Registro completo y constante:** Días con TotalRevenue, TotalHoursWorked y los tres turnos completos para que cuenten como “financieramente completos” y entren en semanas completas.
2. **Configuración:** Dirección (lat/lon) para clima y festivos; productividad ideal y horas por turno; opcional: eventos en la tabla Events.
3. **Dejar correr el análisis en segundo plano:** Al guardar un día o al iniciar la app se ejecuta RunFullBackgroundAnalysisAsync → EvaluatePredictionsAsync → RunWeeklyAnalysisAsync. Así se actualizan bias/MAE, patrones, tendencia y se **guarda** la predicción de la semana siguiente con todos los factores.
4. **Abrir Estimaciones:** GetNextWeekDiagramacionAsync leerá la predicción guardada (con clima, festivos, eventos aplicados) y la mostrará; las alertas se calculan sobre esa misma semana.

Si se quiere **forzar recálculo** con datos actualizados (p. ej. nuevo pronóstico), habría que volver a ejecutar RunWeeklyAnalysisAsync (o equivalente) para regenerar y guardar la predicción.

---

## 5. Resumen en una frase

Las estimaciones realistas se apoyan en **histórico por día de la semana** (ponderado por antigüedad y feedback), **tendencia 4 vs 4**, **sesgo y MAE aprendidos** por DOW, **nivel reciente**, **estacionalidad por mes**, **patrones de lluvia, festivos y temperatura** (con peso por confianza), **eventos** del calendario, y **productividad objetivo + histórico de personal por turno**. Todo está moderado (límites en factores y producto) y la **estimación más completa** es la que se guarda en `WeeklyPredictions` tras el análisis semanal, que incluye clima, festivos y eventos; el fallback en vivo es una aproximación sin esos factores aplicados a la cifra.
