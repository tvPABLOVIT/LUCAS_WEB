# Análisis del bloque "Límite cómodo por esquema de personal"

## Objetivo del bloque

Mostrar **por separado** para **sala** y para **cocina** hasta qué nivel de facturación por persona suele considerarse el turno "cómodo" (dificultad aceptable). Así se sabe a partir de qué facturación conviene añadir **otro personal de sala** u **otro personal de cocina**.

La métrica clave no es "facturación por camarero" ni "por cocinero" en sentido restrictivo, sino **facturación por cada personal de sala** y **facturación por cada personal de cocina**: es decir, facturación del turno dividida por el número de personas en sala y por el número de personas en cocina respectivamente.

---

## Fuente de datos

- **Turnos con feedback:** `ShiftFeedback` con facturación del turno, personal de sala (`StaffFloor`), personal de cocina (`StaffKitchen`), y las 5 preguntas (V, R, M, D, Q5 cocina).
- **Cálculo al guardar turno** (`ExecutionController.ToShift`):
  - **Facturación por personal de sala:** `RevenuePerWaiterSala = Revenue / StaffFloor` (cuando `StaffFloor > 0` y `Revenue > 0`).
  - **Facturación por personal de cocina:** `RevenuePerWaiterCocina = Revenue / StaffKitchen` (cuando `StaffKitchen > 0` y `Revenue > 0`).
- **Dificultad:** `DifficultyScore` (sala, a partir de V,R,M,D) y `DifficultyScoreKitchen` (cocina, Q5). Escala 1–5; turnos "difíciles" = puntuación ≥ 4.

---

## Cómo se calcula el límite cómodo (backend)

Servicio: **`StaffRevenueComfortService.GetAggregatesAsync`** (API `GET /api/analytics/staff-revenue-comfort?minShifts=1`).

### Sala (por esquema sala-cocina)

1. **Filtro:** turnos con `RevenuePerWaiterSala`, `DifficultyScore` y `StaffFloor > 0`.
2. **Agrupación:** por esquema **sala-cocina** `"StaffFloor-StaffKitchen"` (ej. `"1-1"`, `"2-1"`, `"2-2"`). Esquemas considerados: `1-1`, `1-2`, `2-1`, `2-2`, `2-3`, `3-2`, `3-3`.
3. **Bandas de facturación:** para cada esquema, los turnos se clasifican en bandas de **facturación por personal de sala** (€/persona sala):
   - Límites de banda: 0, 400, 500, 600, 700, 800, 1000, 9999 (€).
4. **Por cada banda:** se calcula número de turnos, dificultad media y % de turnos con dificultad ≥ 4.
5. **Límite cómodo aproximado (sala):** primera banda (min de la banda) donde la **dificultad media ≥ 3,5**. Indica hasta aproximadamente cuántos € por personal de sala suele estar "cómodo" ese esquema.

Resultado por esquema: `schema`, `bands[]` (min, max, count, avg_difficulty, pct_difficult), `comfort_limit_approx`.

### Cocina (por número de personas de cocina)

1. **Filtro:** turnos con `RevenuePerWaiterCocina`, `DifficultyScoreKitchen` y `StaffKitchen > 0`.
2. **Agrupación:** por **número de personas de cocina** (1, 2, 3…), no por par sala-cocina.
3. **Bandas:** mismas bandas de **facturación por personal de cocina** (€/persona cocina): 0–400, 400–500, etc.
4. **Por cada banda:** count, dificultad media cocina, % difíciles (≥4).
5. **Límite cómodo aproximado (cocina):** primera banda donde la **dificultad media cocina ≥ 3,5**. Indica hasta cuántos € por personal de cocina suele estar cómodo ese número de cocineros.

Resultado por esquema cocina: `schema` (ej. `"1"`, `"2"`), `bands[]`, `comfort_limit_approx`.

---

## Uso del límite cómodo en la app

- **Vista Límite cómodo** (`limite-comodo.js`): muestra las tablas Sala y Cocina por separado; cada una con sus esquemas, bandas y límite aproximado. La terminología correcta en UI es **facturación por cada personal de sala** y **facturación por cada personal de cocina** (no "por camarero"/"por cocinero").
- **Estimaciones / predicción de personal** (`StaffByTurnoPredictionService`, `estimaciones.js`): se usan `comfort_limit_approx` por esquema (sala) y por número de cocina para no recomendar esquemas donde el €/persona sala o €/persona cocina supere el límite cómodo (con margen ~5%).

---

## Resumen

| Ámbito   | Agrupación              | Métrica de facturación              | Dificultad        | Límite cómodo approx                          |
|--------|-------------------------|-------------------------------------|-------------------|-----------------------------------------------|
| **Sala**   | Esquema sala-cocina (1-1, 2-1…) | Facturación por cada personal de sala (€/persona) | DifficultyScore (V,R,M,D) | Primera banda con dificultad media ≥ 3,5     |
| **Cocina** | Nº personas cocina (1, 2, 3…)   | Facturación por cada personal de cocina (€/persona) | DifficultyScoreKitchen (Q5) | Primera banda con dificultad media ≥ 3,5     |

Sala y cocina tienen **límites cómodos calculados por separado**; no comparten el mismo valor ni la misma agrupación (sala por esquema completo, cocina por número de cocineros).
