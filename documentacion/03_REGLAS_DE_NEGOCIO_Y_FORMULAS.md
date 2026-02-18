# 03 — Reglas de negocio y fórmulas

Cálculos, scoring (V/R/M/D), KPIs, predicciones y bandas de confianza. Extraído del proyecto actual (TurnoScoringService, InteligenciaService, DashboardController, etc.).

---

## 1. Turnos

- **Nombres normalizados:** "Mediodia", "Tarde", "Noche" (sin tilde en Mediodía en BD).
- **Orden:** Mediodía → Tarde → Noche.
- **Un día tiene como máximo tres turnos** (uno por nombre). Si se envía otro nombre, normalizar: "mediodía"/"Mediodía" → "Mediodia"; "tarde" → "Tarde"; "noche" → "Noche".

---

## 2. Feedback Q1–Q4 (preguntas por turno)

Cada turno tiene cuatro preguntas; las respuestas se guardan como **texto exacto** (una de las opciones de la lista).

### Q1 — Volumen (¿Cuánto trabajo hubo?)

Opciones (orden 1–5):

1. Pocas mesas  
2. Media sala  
3. Sala completa  
4. Sala y terraza completas  
5. Sala y terraza completas y doblamos mesas  

### Q2 — Ritmo (¿Cómo fue el ritmo de entradas?)

1. Muy espaciadas, sin acumulación  
2. Entradas tranquilas  
3. Flujo constante  
4. Muchas entradas juntas  
5. Entradas continuas sin margen  

### Q3 — Margen (¿Cuánto margen hubo?)

1. Siempre adelantado  
2. Generalmente con margen  
3. Justo  
4. Poco margen  
5. Ningún margen  

### Q4 — Dificultad (¿Qué tan duro fue el turno?)

1. Muy fácil  
2. Fácil  
3. Normal  
4. Difícil  
5. Muy difícil  

---

## 3. Scoring por turno (SGT — Índice 1–5 por eje)

- **V (Volumen):** índice 1–5 según la opción elegida en Q1 (orden de la lista).  
- **R (Ritmo):** índice 1–5 según Q2.  
- **M (Margen):** índice 1–5 según Q3.  
- **D (Dificultad):** índice 1–5 según Q4.  

**Fórmula SGT (Score Global Turno):**  
`SGT = (V×2) + R + (6−M) + D`  
Rango: 6–31.

- SGT 6–10: Infrautilizado / Tranquilo.  
- SGT 11–14: Tranquilo.  
- SGT 15–18: Equilibrado.  
- SGT 19–22: Productivo.  
- SGT 23–26: Exigente.  
- SGT 27–31: Crítico.  

**Fuerza de feedback del día (0.2–1.0):**  
Para cada turno con al menos un Q rellenado:  
`(V + R + (6−M) + D) / 20`  
Luego promediar entre turnos y limitar a [0.2, 1.0]. Si no hay feedback, usar 0.5 (neutral).

---

## 4. KPIs del Dashboard (semana)

- **Semana:** Lunes a domingo (7 días). `weekStart` = lunes en `yyyy-MM-dd`.
- **TotalRevenue:** Suma de `ExecutionDays.TotalRevenue` de los días de la semana.
- **TotalHours:** Suma de `TotalHoursWorked` de la semana.
- **AvgProductivity:** TotalRevenue / TotalHours (€/h). Si TotalHours = 0 → 0.
- **AvgStaff:** Media de `StaffTotal` por día con datos.
- **AvgRevenueHistoric:** Media de facturación semanal (solo semanas “completas”: ≥5 días con datos), calculada sobre histórico.
- **PrevWeekRevenue / PrevWeekProductivity:** Misma semana pero la semana anterior (lunes a domingo).

**Comparativa por día:**  
Para cada día de la semana, comparar su `TotalRevenue` con la media histórica del mismo día de la semana (mismo DayOfWeek). Si la diferencia es >5% al alza → "↑ Al alza"; si >5% a la baja → "↓ A la baja"; si no → "→ Estable".

---

## 5. Predicción de la semana siguiente

**Semana “siguiente”:** Lunes a domingo de la semana que viene (después de hoy).

**Fuentes de datos para estimar:**

1. **Semanas completas:** Solo semanas con 7 días con datos (TotalRevenue > 0, TotalHoursWorked > 0, y los 3 turnos con revenue/horas).
2. **Base por día de la semana (DOW):** Media de facturación por día de la semana (Lunes… Domingo) sobre histórico completo.
3. **Tendencia 4 vs 4:** Comparar últimas 4 semanas vs 4 anteriores; porcentaje de cambio (trendPct). Se limita a [-5, 25] % y se aplica como factor (ej. 1 + trendPct/100 * 0.35).
4. **Nivel reciente:** Media de los últimos 7 días / media global; factor limitado a [0.95, 1.1].
5. **Estacionalidad por mes:** Media del mismo mes / media global; factor limitado a [0.9, 1.1].
6. **Sesgo por DOW (aprendizaje):** Si existe `PredictionBiasJson` (7 valores, uno por día), se aplica como corrección (biasFactor).
7. **Bandas min/max:** A partir del coeficiente de variación (CV) del mismo DOW en histórico: `min = est * (1 - 1.5*CV)`, `max = est * (1 + 1.5*CV)`, con límites 0.85–1.15 respecto a `est`.

**Facturación del día entero:**  
Se asigna por defecto en tres partes iguales a Mediodía, Tarde y Noche si no hay desglose por turno.

**Etiqueta de confianza (por día):**  
Si `(max - min) / revenue < 0.30` → "Alta"; si < 0.50 → "Media"; si no → "Baja".

---

## 6. Esquema de personal (sala/cocina) por día

A partir de la facturación estimada por turno y la **productividad ideal (€/h)** y **horas por turno**:

- **Total personas por turno:** `round(PredictedRevenueTurno / (ProductividadIdealEurHora * HorasPorTurno))`.
- **Reparto sala/cocina (TotalToCocinaSala):**  
  Total 2 → (1 cocina, 1 sala); 3 → (2, 1); 4 → (2, 2); 5 → (3, 2); 6 → (3, 3). Máximo cocina 3, máximo sala 3.
- **Umbrales:**  
  - Día ≥ 2400 € (o max del rango ≥ 2400): al menos 2 sala y 2 cocina por turno.  
  - Día ≥ 3000 €: hasta 3 cocina; día ≥ 3500 €: hasta 3 sala.  
  - Turno ≥ 600 €: al menos 2 sala en ese turno.

---

## 7. Coste de personal vs facturación

- **CostoPersonalPorHora:** Parámetro de configuración (€/h por persona).
- **Horas totales (semana):** Suma de horas trabajadas o estimadas.
- **Coste total personal:** Horas × CostoPersonalPorHora.
- **Porcentaje vs facturación:** (Coste total / Facturación total) × 100.

En estimaciones se usa la **facturación promedio semanal** histórica y la **productividad objetivo (€/h)** para calcular “horas necesarias para alcanzar la productividad objetivo” con la facturación estimada.

---

## 8. Alertas / bloques “Qué puede afectar la semana siguiente”

1. **Semana anterior:** Datos reales (facturación, horas, productividad €/h) de la semana pasada.  
2. **Tendencia:** % más o menos que la semana anterior respecto a la estimación de la semana siguiente.  
3. **Clima:** Resumen de la semana; destacar días con lluvia (texto que contenga "lluvia", "rain", "Llovizna", "drizzle").  
4. **Festivos:** Solo si hay festivo: nombre y fecha.  
5. **Misma semana, mes anterior:** Mismas fechas numéricas (ej. 3–9 feb) comparadas con 3–9 ene; facturación del mes pasado y % más/menos esperado.  
6. **Eventos / Obras:** Eventos y obras en radio 300 m (Open Data BCN, etc.); solo mostrar si hay resultados.

---

## 9. Ajuste por lluvia en estimación

Si se dispone de **patrón histórico** “días lluviosos facturan X% menos que soleados”, aplicar un factor de reducción a la estimación del día cuando la previsión climática indique lluvia para ese día. (Implementación exacta en InteligenciaService: factor aplicado por día según weather.)

---

## 10. Referencia al código actual

- **TurnoScoringService.cs:** GetVolumenIndex, GetRitmoIndex, GetMargenIndex, GetDificultadIndex, GetSgt, GetEstadoTurno, GetTipoTurno, BuildResumenNivel3.
- **InteligenciaService.cs:** GetFeedbackStrength, GetConfidenceLabel, GetSalaCocinaScheme, TotalToCocinaSala, GetCompleteWeeks, IsFinanciallyComplete, BuildNextWeekPredictionAsync, GetNextWeekDiagramacionAsync, GetAlertasDiagramacionAsync.
- **DashboardController.cs:** GetWeek, GetWeekStart, GenerarResumen.
- **ConfiguracionService / settings:** HorasPorTurno, ProductividadIdealEurHora, CostoPersonalPorHora, PredictionBiasJson.
