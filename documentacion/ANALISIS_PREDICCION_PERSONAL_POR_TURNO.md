# Análisis: predicción real de personal por turno (Sala y Cocina)

## Flujo en dos pasos

Predecir el futuro es difícil; por eso usamos **todos los datos** disponibles y luego calculamos el **personal necesario** para cubrir esa predicción.

### Paso 1: Predecir la facturación de la semana siguiente

- **Objetivo:** Estimar la facturación (y por día/turno) de la semana siguiente con todo lo que tenemos.
- **Datos usados:** Histórico de facturación por día de la semana (DOW), tendencia, bias/MAE aprendidos, estacionalidad por mes, nivel reciente. Distribución por turno (Mediodía, Tarde, Noche) desde histórico de `ShiftFeedbacks` por DOW.
- **Enriquecimiento (PredictionEnrichmentService):** Sobre esa predicción base se aplican:
  - **Clima / lluvia:** previsión por día, patrón “Impacto clima lluvioso” (cómo nos afecta la lluvia) → `weatherFactor` sobre revenue y sobre med/tar/noc.
  - **Festivos:** patrón “Impacto festivos” → `holidayFactor`.
  - **Temperatura extrema:** patrón “Impacto temperatura” → `tempFactor`.
  - **Eventos:** impacto (alto/bajo) → `eventFactor`.
- **Resultado:** Por cada día, `revenue`, `mediodia`, `tarde`, `noche` (y min/max) **ya incluyen** el efecto de lluvia, festivos, temperatura y eventos. Es la **predicción de facturación por turno** que vamos a cubrir con personal.

### Paso 2: Calcular el personal necesario (sala y cocina) por turno

- **Pregunta:** ¿Cuántas personas necesitamos en cada turno (Mediodía, Tarde, Noche) para **cubrir** esa predicción de facturación?
- **Entrada:** La predicción ya enriquecida: revenue por día y por turno (`mediodia`, `tarde`, `noche`) — con lluvia y el resto de factores ya aplicados.
- **Cálculo del personal (StaffByTurnoPredictionService):**
  1. **Parte baja del rango para diagramar al equipo:** Si el día tiene rango de facturación estimada (min–max), se usa la **parte más baja del rango (min)** para calcular el personal: el total del día y la facturación por turno se escalan a ese mínimo (min × proporción por turno), de modo que no se sobre-dimensione el equipo si la facturación real baja.
  2. **Umbrales de facturación por personal (igual para sala y cocina):** La facturación necesaria para subir de 1 a 2 personas es la base (prod × horas_turno). Para subir de 2 a 3 personas se exige **al menos un 50% más** de facturación que para 1→2; los siguientes escalones usan el mismo incremento. La regla aplica por igual a sala y cocina.
  3. **Tope por facturación del día:** Con equipo 2-2-2 (sala y cocina) se puede facturar hasta 3000 € en todo el día. Si la facturación del día es **menor de 3000 €**, no se recomienda más de 2 personas en sala ni 2 en cocina por turno (se mantiene el tope 2-2-2 para ambos).
  4. **Combinaciones permitidas:** Solo se recomiendan esquemas predefinidos. **Sala (Med-Tar-Noc):** 1-1-1, 1-1-2, 2-1-2, 2-2-2, 1-2-1, 2-1-1, 3-1-3, 1-2-3, 3-1-2 (no 3-2-2). **Cocina:** 1-1-1, 2-1-2, 1-1-2, 2-2-2, 3-1-3, 1-2-3. El resultado se ajusta a la combinación permitida más cercana (priorizando la que cubre la necesidad con menor total). **Consistencia con facturación:** solo se eligen combinaciones que respetan el orden de facturación por turno (si hay más facturación al mediodía que a la tarde, en sala/cocina no puede haber menos personas al mediodía que por la tarde).
  5. **Sala y cocina por separado:** Reparto con ratio histórico (DOW, turno) o `TotalToCocinaSalaByShift(total, turno)` si no hay ratio. **Por turno:** en mediodía y noche cocina tiene la misma cantidad o más que sala; en tarde cocina tiene la misma cantidad o menos que sala (ej. real: sala 1-1-2, cocina 2-1-2). Si hay histórico, se aplica el mismo criterio al rebalancear.
  6. **Histórico por (DOW, turno):** Mediana de StaffFloor y StaffKitchen; si es menor que el mínimo por productividad, usamos el mínimo.
  7. **Límites cómodos:** No superar €/camarero y €/cocinero por encima del límite aprendido (StaffRevenueComfortService); si se supera, subir sala o cocina al siguiente esquema permitido.
- **Resultado:** Por día: `staffSala` (ej. 2-2-3), `staffCocina` (ej. 1-2-2) — **cuántas personas en sala y en cocina en cada turno para cubrir la predicción**, teniendo en cuenta lluvia (ya en la revenue), límites cómodos y productividad objetivo.

---

## 1. Situación actual

Cada tarjeta del **Plan por día** (semana siguiente) muestra:
- **Sala:** X-X-X (personal en sala para Mediodía, Tarde, Noche)
- **Cocina:** X-X-X (personal en cocina para Mediodía, Tarde, Noche)

### 1.1 Origen actual de Sala y Cocina (frontend)

En `estimaciones.js`:

1. **Revenue por turno** (`med`, `tar`, `noc`) viene de la predicción diaria:
   - `d.mediodia`, `d.tarde`, `d.noche` (o reparto `rev/3` si no hay).
2. **Esquema Sala/Cocina** se calcula con:
   - **Prioridad 1:** `getSalaCocinaSchemeFromComfort(med, tar, noc, comfortBySchema, comfortByCocina)`  
     - Usa límites cómodos por esquema (sala) y por número de cocineros (cocina) del endpoint `/api/analytics/staff-revenue-comfort`.
     - Recorre esquemas `1-1`, `1-2`, `2-1`, `2-2`, `2-3`, `3-2`, `3-3` y elige el primero donde:
       - max(med/S, tar/S, noc/S) ≤ límite_sala × 1,05
       - max(med/C, tar/C, noc/C) ≤ límite_cocina × 1,05
     - **Limitación:** devuelve el **mismo** número para los tres turnos (S-S-S, C-C-C), no personal distinto por turno.
   - **Prioridad 2 (fallback):** `getSalaCocinaScheme(med, tar, noc, prodObj, horasPorTurno, rev)`  
     - Calcula personas por turno con productividad (rev / (prod×horas)), reparto TotalToCocinaSala y umbrales (día ≥ 2400 €, máx sala/cocina según facturación).
     - **Sí** devuelve sala y cocina **por turno** (ej. 2-2-3, 1-2-2).

### 1.2 Backend existente

- **NextWeekPredictionService:** calcula distribución de **revenue** por turno (Med/Tar/Noc) desde histórico de `ShiftFeedbacks` por DOW; no calcula personal.
- **StaffRevenueComfortService:** agrega por esquema sala-cocina y por cocineros; devuelve `comfort_limit_approx` (€/camarero o €/cocinero hasta el cual la dificultad media suele estar por debajo del umbral).
- **SalaCocinaService (estático):** `GetSalaCocinaScheme(revMed, revTar, revNoc, prod, horas, revDia)` — lógica heurística por turno (productividad + TotalToCocinaSala + umbrales); no usa histórico ni comfort.

### 1.3 Datos disponibles en histórico

En **ShiftFeedback** por cada turno real:
- `ShiftName` (Mediodia, Tarde, Noche)
- `StaffFloor`, `StaffKitchen`
- `Revenue`, `RevenuePerWaiterSala`, `RevenuePerWaiterCocina`
- `DifficultyScore`, `DifficultyScoreKitchen`
- `ExecutionDay.Date` → se puede obtener DOW (0=Lunes … 6=Domingo)

Es decir: **sí tenemos histórico de cuánta sala y cocina se usó por turno y por día de la semana.**

---

## 2. Objetivo: predecir “de verdad” la cantidad de personal por turno

Objetivo: que las etiquetas **Sala: X-X-X** y **Cocina: X-X-X** se basen en:

1. **Histórico por DOW y turno:** para cada (día de la semana, turno) usar la mediana o media de `StaffFloor` y `StaffKitchen` de las últimas N semanas.
2. **Respetar límite cómodo:** no recomendar un esquema que implique €/camarero o €/cocinero por encima del límite cómodo (cuando haya datos de comfort).
3. **Cubrir revenue estimado:** el personal recomendado debe ser suficiente para la revenue por turno estimada (productividad objetivo); si el histórico da menos, subir al mínimo necesario (o marcar como “por debajo de objetivo”).
4. **Fallback:** cuando no haya suficiente histórico para (DOW, turno), usar la lógica actual (productividad + TotalToCocinaSala + comfort si aplica).

---

## 3. Enfoque propuesto (híbrido)

### 3.1 Nuevo servicio: StaffByTurnoPredictionService (o integrado en NextWeekPredictionService)

- **Entrada:** semana a predecir (lunes), revenue por día/turno ya calculada (med, tar, noc por día).
- **Paso 1 – Histórico por DOW y turno:**  
  Últimas 8–12 semanas, agrupar por (DOW, ShiftName) y calcular:
  - Mediana (o media) de `StaffFloor` y `StaffKitchen`.
  - Mínimo de muestras por (DOW, turno): p.ej. 3; si no se alcanza, no usar ese dato para ese (DOW, turno).
- **Paso 2 – Por cada día de la predicción:**  
  Para cada turno (Mediodía, Tarde, Noche):
  - Si hay histórico para (DOW, turno): usar mediana/media como base (sala_hist, cocina_hist).
  - Si no: dejar null para ese turno (se rellenará en el fallback).
- **Paso 3 – Ajuste por revenue y comfort:**
  - **Mínimo por productividad:** con revenue del turno y productividad ideal (€/h, ej. 50), calcular personas mínimas = Ceiling(revenue/(prod*horas)); **sala y cocina por separado**: si hay ratio histórico por (DOW, turno) usarlo; si no, `TotalToCocinaSala`. Si histórico < mínimo → usar mínimo.
  - **Límite cómodo:** si tenemos comfort_limit por esquema/cocina, comprobar que con la sala/cocina recomendada el €/camarero y €/cocinero del turno no superen el límite (con margen). Si se supera, subir sala o cocina al siguiente esquema que quede dentro del límite.
- **Paso 4 – Resultado por día:**  
  Para cada día: `staffSalaMed`, `staffSalaTar`, `staffSalaNoc`, `staffCocinaMed`, `staffCocinaTar`, `staffCocinaNoc` y opcionalmente `staffSource: "historic" | "heuristic" | "mixed"`.

### 3.2 Dónde integrar el resultado

- **Opción A (recomendada):** Añadir al JSON de cada día en `dailyPredictionsJson` (en enriquecimiento o al generar la predicción) los campos de personal por turno. Así el frontend solo pinta lo que envía el backend.
- **Opción B:** Endpoint aparte que, dado `weekStart` y el JSON de días (o solo weekStart y que el backend reconstruya la predicción), devuelva `{ "days": [ { "date", "sala": "2-2-3", "cocina": "1-2-2", "source": "historic" }, ... ] }`. El frontend seguiría teniendo que combinar predicción + este endpoint.

La **Opción A** simplifica el frontend y mantiene una sola fuente de verdad (el objeto día con revenue, factores y personal).

### 3.3 Frontend

- Si el día tiene `staffSalaMed`, `staffSalaTar`, `staffSalaNoc` (y cocina), formatear:
  - **Sala:** `staffSalaMed-staffSalaTar-staffSalaNoc`
  - **Cocina:** `staffCocinaMed-staffCocinaTar-staffCocinaNoc`
- Si no (predicción antigua o sin enriquecimiento), mantener la lógica actual: `getSalaCocinaSchemeFromComfort` + `getSalaCocinaScheme`.
- Opcional: mostrar badge “Histórico” / “Heurístico” / “Mixto” según `staffSource`.

---

## 4. Fórmulas y reglas concretas

### 4.1 Histórico por (DOW, turno)

- Ventana: últimas 12 semanas (84 días), mismos criterios que distribución de revenue (días con `!IsFeedbackOnly`, revenue > 0).
- Agrupar: `ShiftFeedbacks` por `(ExecutionDay.Date.DOW, NormalizeShiftName(ShiftName))`.
- Por grupo: mediana de `StaffFloor`, mediana de `StaffKitchen` (la mediana es más robusta que la media a outliers).
- Mínimo de turnos por grupo: 3; si no se alcanza, no usar histórico para ese (DOW, turno).

### 4.2 Productividad ideal (ej. 50 €/h) y mínimo de personal

- La **productividad ideal** (configurada, ej. 50 €/h) es **por todo el equipo completo (sala + cocina)**: 50 € por cada hora trabajada del conjunto sala+cocina.
- Para calcular la productividad (en Dashboard o en histórico) hay que **sumar las horas de sala y cocina** (horas totales del equipo); el ratio es facturación / (horas sala + horas cocina).
- Personas necesarias en el turno (sala + cocina):  
  `personas_turno = ceil(revenue_turno / (productividad_eur_hora * horas_turno))`  
  con techo 6 y mínimo 1. Ese total se reparte entre sala y cocina (ratio histórico o TotalToCocinaSala). Con *Ceiling* se garantiza que no falte personal para alcanzar el objetivo de 50 €/h del equipo.

### 4.3 Cálculo de sala y cocina por separado

- **Sala** y **cocina** se calculan por separado (aunque se muestren juntos en la tarjeta).
- Si hay **ratio histórico** por (DOW, turno): `ratio_sala = StaffFloor/(StaffFloor+StaffKitchen)`, `ratio_cocina = StaffKitchen/(StaffFloor+StaffKitchen)`. Entonces:  
  `sala_min = ceil(personas_turno * ratio_sala)`, `cocina_min = personas_turno - sala_min` (con mínimo 1 en cada uno).
- Si no hay ratio para ese (DOW, turno): reparto con `TotalToCocinaSala(personas_turno)`.
- Si histórico existe pero es menor que este mínimo, usar el mínimo (o marcar “por debajo de objetivo” si se quisiera mostrar).

### 4.4 Límite cómodo

- Por turno: `eur_camarero = revenue_turno / sala`, `eur_cocinero = revenue_turno / cocina`.
- Comparar con `comfort_limit_approx` del esquema `sala-cocina` y del número de cocina (según StaffRevenueComfortService).
- Si se supera el límite (con margen 1,05): subir sala o cocina (siguiente esquema permitido) hasta que ambos queden por debajo.

### 4.5 Esquemas permitidos

Mismo orden que hoy: `1-1`, `1-2`, `2-1`, `2-2`, `2-3`, `3-2`, `3-3`. Por turno podemos tener combinaciones distintas (ej. Med 2-1, Tar 2-2, Noc 3-2).

---

## 5. Resumen de implementación

| Paso | Componente | Qué hace |
|------|------------|----------|
| **1. Predecir facturación** | NextWeekPredictionService | Histórico por DOW, tendencia, bias/MAE, distribución por turno (med/tar/noc). |
| | PredictionEnrichmentService | Aplica clima (lluvia), festivos, temperatura, eventos → revenue y med/tar/noc ya incluyen esos efectos. |
| **2. Calcular personal** | StaffByTurnoPredictionService | Personas necesarias por turno para cubrir esa facturación (productividad 50 €/h, ratio histórico sala/cocina, límites cómodos). |
| | Frontend | Si el día trae estos campos, usarlos para “Sala:” y “Cocina:”; si no, mantener lógica actual (fallback heurístico). |

Con esto: (1) se predice la facturación de la semana siguiente usando todos los datos (histórico, lluvia, festivos, etc.); (2) se calcula **cuántas personas necesitamos en cada turno** para cubrir esa predicción, teniendo en cuenta productividad, límites cómodos y ratio histórico sala/cocina.
