# Análisis completo: predicción de personal por turno (1-1-1, 1-2-2, etc.)

## 1. Visión general del flujo

La predicción de **cuánto personal poner en cada turno** (Sala: X-X-X, Cocina: X-X-X) se hace en **dos fases**:

| Fase | Qué se calcula | Dónde |
|------|----------------|-------|
| **1. Facturación** | Revenue total por día y por turno (mediodía, tarde, noche) | NextWeekPredictionService + PredictionEnrichmentService |
| **2. Personal** | Personas en sala y cocina por turno para cubrir esa facturación | StaffByTurnoPredictionService |

El backend devuelve por cada día: `staffSala` (ej. `"2-2-3"`), `staffCocina` (ej. `"1-2-2"`), más los campos desglosados `staffSalaMed`, `staffSalaTar`, `staffSalaNoc`, `staffCocinaMed`, `staffCocinaTar`, `staffCocinaNoc`. El frontend usa esos valores si existen; si no, hace fallback con lógica propia (comfort + productividad).

---

## 2. Entrada al cálculo de personal

- **Por día:** `revenue` (o `predictedRevenue`), `min`, `max`, `mediodia`, `tarde`, `noche` (facturación por turno ya enriquecida con clima, festivos, eventos).
- **Configuración:** `ProductividadIdealEurHora` (ej. 50 €/h), `HorasPorTurno` (ej. 4).
- **Comfort:** límites por esquema sala-cocina y por número de cocineros (`/api/analytics/staff-revenue-comfort`).
- **Histórico:** ShiftFeedbacks de las últimas 12 semanas (mediana de StaffFloor y StaffKitchen por (DOW, turno)).

**Decisión importante:** para “diagramar al equipo” se usa la **parte baja del rango** de facturación. Si el día tiene `min` y `rev`, se toma `revenueDiaForStaff = min` y la facturación por turno se escala con el mismo ratio (`medForStaff = med * (min/rev)`, etc.). Así no se sobre-dimensiona el equipo si la facturación real baja.

---

## 3. Cálculo por turno (Mediodía, Tarde, Noche)

Para **cada turno** del día se resuelve (sala, cocina) con este orden de prioridad.

### 3.1 Número total de personas del turno (sala + cocina)

No se usa un simple `ceil(revenue_turno / (prod × horas))`. Se usan **umbrales escalonados**:

- **Base:** `step1To2 = productividadEurHora × horasPorTurno` (ej. 50×4 = 200 €).
- **De 2 a 3 personas:** hace falta **50 % más** de facturación que para 1→2: `step2To3 = step1To2 × 1.5`.
- Umbrales acumulados:
  - 1 persona: revenue_turno &lt; step1To2
  - 2: &lt; step1To2 + step2To3
  - 3: &lt; + step2To3
  - 4, 5, 6: mismo incremento cada vez.

Ejemplo con 50 €/h y 4 h: step1To2=200, step2To3=300. Para 2 personas hace falta &lt; 500 €; para 3, &lt; 800 €; etc. Total personas por turno: entre 1 y 6.

### 3.2 Reparto entre sala y cocina

- **Si hay histórico** para (DOW, turno): se usa la **mediana** de StaffFloor y StaffKitchen (últimas 12 semanas, mínimo 3 muestras).  
  - `ratioSala = hist.Sala / (hist.Sala + hist.Cocina)`.  
  - `salaMin = ceil(totalPeople × ratioSala)`, `cocinaMin = totalPeople - salaMin` (con mínimo 1 en cada uno).  
  - **Ajuste por turno:**  
    - **Tarde:** cocina ≤ sala (si cocinaMin &gt; salaMin se rebalancea a mitad).  
    - **Mediodía y Noche:** cocina ≥ sala (si salaMin &gt; cocinaMin se rebalancea).
- **Si no hay histórico:** se usa `SalaCocinaService.TotalToCocinaSalaByShift(totalPeople, shiftName)`:
  - Mediodía/Noche: cocina ≥ sala (ej. 3 → (1,2), 4 → (2,2), 5 → (2,3)).
  - Tarde: cocina ≤ sala (ej. 3 → (2,1), 5 → (3,2)).

### 3.3 Tope por facturación del día

Si **revenue del día &lt; 3000 €**, no se recomienda más de **2 personas en sala ni 2 en cocina** en ningún turno (equipo tipo 2-2-2 como máximo).  
Luego se aplica `Clamp(sala, 1, 3)` y `Clamp(cocina, 1, 3)`.

### 3.4 Histórico vs mínimo vs comfort

En `ResolveStaffForShift`:

1. Se calcula `(salaMin, cocinaMin)` con **MinStaffByProductivity** (umbrales escalonados + ratio histórico o TotalToCocinaSalaByShift + tope 3000 €).
2. **Si hay histórico** para (DOW, turno): se toma `sala = max(hist.Sala, salaMin)`, `cocina = max(hist.Cocina, cocinaMin)` (clamp 1–3). Origen: `"historic"`.
3. **Si no hay histórico:** se prueba **ApplyComfortLimit**: se recorre los esquemas permitidos `1-1`, `1-2`, `2-1`, `2-2`, `2-3`, `3-2`, `3-3` y se elige el **menor** (menor número total) donde:
   - `revenue_turno / S ≤ limitSala × 1.05`
   - `revenue_turno / C ≤ limitCocina × 1.05`
   Si se encuentra, se usa ese (S, C) asegurando al menos (salaMin, cocinaMin). Origen: `"heuristic"`.
4. Si no hay comfort válido, se devuelve (salaMin, cocinaMin). Origen: `"heuristic"`.

Los límites de comfort vienen de **StaffRevenueComfortService**: por esquema (sala) y por número de cocineros (cocina), se calcula `comfort_limit_approx` como el **mínimo de la primera banda** (0–400, 400–500, 500–600, …) donde la **dificultad media** (DifficultyScore / DifficultyScoreKitchen) **≥ 3.5**. Así se evita recomendar esquemas que históricamente han sido “duros”.

---

## 4. Combinaciones permitidas (SnapToAllowed)

El resultado por turno (salaMed, salaTar, salaNoc) y (cocinaMed, cocinaTar, cocinaNoc) se **ajusta** a listas fijas para evitar combinaciones raras.

### 4.1 Sala (Mediodía–Tarde–Noche)

Tuplas permitidas (M, T, N):

- (1,1,1), (1,1,2), (2,1,2), (2,2,2), (1,2,1), (2,1,1), (3,1,3), (1,2,3), (3,1,2).

**No** está permitido, por ejemplo, 3-2-2.

### 4.2 Cocina (Mediodía–Tarde–Noche)

- (1,1,1), (2,1,2), (1,1,2), (2,2,2), (3,1,3), (1,2,3).

### 4.3 Consistencia con la facturación

Solo se consideran combinaciones **consistentes** con el orden de facturación por turno:  
si un turno tiene **más** facturación que otro, ese turno debe tener **al menos** tantas personas como el otro.

- Si revMed ≥ revTar → no permitir M &lt; T.
- Si revTar ≥ revNoc → no permitir T &lt; N.
- Y análogamente para el resto de pares.

Entre las combinaciones consistentes se elige:
1. Las que **cubren** (M≥m, T≥t, N≥n) y de ellas la de **menor suma** M+T+N y luego la más cercana al (m,t,n) calculado.
2. Si ninguna cubre, la **más cercana** en distancia (|M−m|+|T−t|+|N−n|) y luego menor suma.

Así se obtienen esquemas del tipo 1-1-1, 1-2-2, 2-2-3, etc., siempre dentro de las listas anteriores y respetando que a más facturación no haya menos personal.

---

## 5. Esquemas “por pareja” (comfort) en backend

En **ApplyComfortLimit** los esquemas son **por turno** (S sala, C cocina en ese turno): `1-1`, `1-2`, `2-1`, `2-2`, `2-3`, `3-2`, `3-3`.  
Se comparan **por turno** `revenue_turno/S` y `revenue_turno/C` con los límites.  
Eso puede dar, por ejemplo, Mediodía 2-1, Tarde 2-2, Noche 3-2. Después **SnapToAllowed** fuerza que la terna (sala o cocina) esté en la lista permitida y sea consistente con el orden de facturación.

---

## 6. Servicio estático SalaCocinaService (fallback sin histórico)

Se usa cuando **no hay histórico** para (DOW, turno) en el reparto sala/cocina:

- **TotalToCocinaSalaByShift(total, shiftName):**
  - total 1 → (1,0); 2 → (1,1).
  - Tarde: 3→(2,1), 4→(2,2), 5→(3,2), 6→(3,3).
  - Mediodía/Noche: 3→(1,2), 4→(2,2), 5→(2,3), 6→(3,3).

- **GetSalaCocinaScheme** (usado en frontend como fallback):  
  Personas por turno = `round(revenue_turno / (prod × horas))`, clamp 1–6.  
  Umbrales día: ≥ 2400 € → mín 2 sala y 2 cocina por turno; &gt; 3000 € → máx cocina 3; ≥ 3500 € → máx sala 3.  
  Turno &gt; 600 € → mín sala 2.  
  Reparto con **TotalToCocinaSala** (no por turno): 2→(1,1), 3→(2,1), 4→(2,2), 5→(3,2), 6→(3,3).  
  Este método **no** distingue mediodía/tarde/noche en el reparto sala/cocina (usa el mismo TotalToCocinaSala para los tres).

---

## 7. Frontend (estimaciones.js)

- **Origen del esquema por día:**  
  Si el día trae `d.staffSala` y `d.staffCocina` (del backend), se usan directamente.  
  Si no:
  1. **getSalaCocinaSchemeFromComfort(med, tar, noc, comfortBySchema, comfortByCocina):**  
     Recorre los mismos esquemas 1-1 … 3-3 y elige el primero donde  
     `max(med/S, tar/S, noc/S) ≤ limitSala×1.05` y `max(med/C, tar/C, noc/C) ≤ limitCocina×1.05`.  
     **Limitación:** devuelve **el mismo** S y C para los tres turnos: sala = S-S-S, cocina = C-C-C (ej. 2-2-2 y 1-1-1). No da 2-1-2 ni 1-2-2.
  2. **getSalaCocinaScheme(med, tar, noc, prod, horas, rev):**  
     Lógica similar a SalaCocinaService.GetSalaCocinaScheme (personas por turno, umbrales 2400/3000/3500/600, TotalToCocinaSala).  
     **Sí** devuelve sala y cocina **por turno** (ej. 2-2-3, 1-2-2).

Cuando la predicción viene del backend con personal ya rellenado, el frontend no usa comfort ni getSalaCocinaScheme para ese día.

---

## 8. Resumen de fórmulas clave

| Concepto | Fórmula / regla |
|----------|------------------|
| Personas totales por turno | Umbrales: 1→2 = prod×horas; 2→3 = +50%; luego mismo paso hasta 6. No es un único ceil(revenue/(prod×horas)). |
| Reparto sala/cocina (con histórico) | ratioSala = hist.Sala/(Sala+Cocina); sala = ceil(total×ratioSala); cocina = total−sala; luego ajuste tarde (cocina≤sala) o med/noche (cocina≥sala). |
| Reparto sin histórico | TotalToCocinaSalaByShift(total, "mediodia"|"tarde"|"noche"). |
| Tope bajo facturación | Si revenue_día &lt; 3000 € → máx 2 sala y 2 cocina por turno. |
| Comfort (backend) | Menor esquema S-C donde revenue_turno/S ≤ limitSala×1.05 y revenue_turno/C ≤ limitCocina×1.05. |
| Límite comfort (analytics) | Primera banda (ej. 0–400, 400–500…) donde avg_difficulty ≥ 3.5 → comfort_limit_approx = min de esa banda. |
| Base para “diagramar” | Se usa **min** (parte baja del rango de revenue) y revenue por turno escalada con ratio min/rev. |

---

## 9. Posibles mejoras o incoherencias (estado actual)

1. **Doble lógica frontend/backend:**  
   ~~El frontend tiene getSalaCocinaSchemeFromComfort que devuelve mismo S y C para los 3 turnos (S-S-S, C-C-C).~~ **APLICADO:** El frontend ahora calcula por turno (minSchemaForRevenue por med/tar/noc) y hace snap a ALLOWED_SALA / ALLOWED_COCINA con consistencia de facturación, igual que el backend.

2. **SalaCocinaService.GetSalaCocinaScheme** (frontend/estático):  
   ~~No aplica la regla “tarde cocina≤sala; med/noche cocina≥sala”.~~ **APLICADO:** GetSalaCocinaScheme (C#) y getSalaCocinaScheme (JS) usan TotalToCocinaSalaByShift(total, turno) por turno, aplicando esa regla.

3. **Comfort por turno vs por día:**  
   ~~En el frontend se usaba el máximo de los tres turnos.~~ **APLICADO:** getSalaCocinaSchemeFromComfort aplica el límite cómodo **por turno** (minSchemaForRevenue(med), minSchemaForRevenue(tar), minSchemaForRevenue(noc)) y luego snap; ya no usa un único S-C para los tres.

4. **Listas AllowedSala / AllowedCocina:**  
   **APLICADO (parcial):** Se añadió **3-2-2** a AllowedSala y AllowedCocina en backend y a ALLOWED_SALA / ALLOWED_COCINA en frontend. Siguen siendo listas fijas; si el negocio necesita más combinaciones, se pueden ampliar igual.

5. **Histórico: solo mediana, sin tendencia:**  
   **APLICADO.** Además de la mediana por (DOW, turno) se calcula la **media de facturación** (AvgRevenue) en la misma ventana. Al resolver personal, si hay AvgRevenue y revenue_turno, se ajusta la mediana con el ratio `revenue_turno / AvgRevenue` (limitado a [0,7, 1,4]) antes de aplicar el mínimo por productividad: así en épocas de más/menos volumen la recomendación sigue la tendencia de facturación reciente.

6. **Evaluación de la predicción de personal:**  
   **APLICADO.** Existe el flujo: al evaluar la semana (EvaluatePredictionsService) se calcula StaffAccuracyJson (MAE personal, % coincidencia exacta recomendado vs real desde ShiftFeedbacks). El endpoint accuracy-history devuelve staffSalaMae y staffExactMatchPct, y la tabla “Historial de precisión” en Estimaciones muestra las columnas “MAE personal” y “Coinc. personal %”.

Con este documento se tiene una visión completa y profunda de cómo se predice el personal por turno (1-1-1, 1-2-2, etc.) y de los puntos donde se podría refinar o unificar criterios entre backend y frontend.
