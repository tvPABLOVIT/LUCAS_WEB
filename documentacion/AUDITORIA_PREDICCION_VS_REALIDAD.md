# Auditoría: Bloque "Predicción vs realidad"

## Por qué seguía viéndose 12.031,59 €

- El backend definía **"hasta hoy"** como **todos los días con `Date <= effectiveAsOf`** (fecha de hoy en calendario).
- Si hoy es **sábado** y en BD existe una fila de ejecución para el sábado (aunque sea con 0 € o datos parciales), ese día entraba en `daysInRange`.
- Resultado: **6 días** (L–S) en la respuesta → suma 12.031,59 € y comparativas incoherentes con el texto "último día con facturación (Viernes) — 5 días".

---

## Concepto correcto y coherente

**"Hasta hoy" = hasta el último día con facturación**

- No es “hasta la fecha de hoy en el calendario”.
- Es **hasta el último día de la semana para el que hay facturación (TotalRevenue > 0)**.
- Ejemplo: hoy sábado, pero el sábado aún no tiene facturación → se usa **Lunes a Viernes** (5 días) en totales, lista de días, comparativa vs semana anterior y bloque "Predicción vs realidad".

Así:
- **Facturación total** = suma de esos N días (ej. 10.936,72 €).
- **Real facturado** en el párrafo = misma suma (suma de los días listados).
- **Comparativa vs sem. ant.** = mismos N días esta semana vs mismos N días semana anterior.
- **Diferencia vs pred.** = real (esos N días) vs predicción acumulada (esos N días).

---

## Cambios aplicados

### 1. Backend – DashboardController.cs (definición de `daysInRange`)

- **Antes (semana actual):**  
  `daysInRange = days.Where(d => d.Date <= effectiveAsOf)`  
  → Incluía cualquier día con fila en BD hasta “hoy” en calendario.

- **Ahora (semana actual):**
  - Se toma el **último día con facturación**: máximo `Date` entre los días de la semana con `Date <= effectiveAsOf` y `TotalRevenue > 0`.
  - `daysInRange` = todos los días con `Date <= ese último día`.
  - Si no hay ningún día con facturación, se mantiene el criterio `Date <= effectiveAsOf`.

Con esto, si el sábado no tiene facturación, solo entran L–V; la API devuelve 5 días y totales coherentes.

### 2. Backend – Lista de días enviada al front

- La lista de días que se envía en la respuesta sigue siendo la de **`daysInRange`** (ya implementado antes).
- `TotalRevenue`, `PrevWeekRevenue`, `DaysIncludedCount` y `Days` usan el mismo conjunto de días.

### 3. Frontend – dashboard.js

- **Prioridad 1:** `realForBlock` = suma de los días que tienen predicción y real (`realSumDisplayed`), mismos que se listan en "Por días".
- **Prioridad 2:** Si no hay esos días, `realForBlock` = suma de `data.days` (limitada a `data.daysIncludedCount`).
- **Prioridad 3/4:** Solo se usa `comparativas.actual.revenue` cuando no es semana actual o no hay `data.days`.

Con el backend corrigiendo `daysInRange`, `data.days` ya son solo los días con facturación; la suma coincide con el total y con el texto del bloque.

---

## Re-auditoría: origen de cada valor del párrafo

| Valor en el párrafo | Origen / Cálculo | Coherencia |
|---------------------|------------------|------------|
| **Predicción acumulada (11.122,16 €)** | `predHastaHoy`: suma de predicción por día para los mismos días que `data.days`; si falta, prorrateo de `comparativas.baseRevenue` por N días. | Mismos N días que el real. |
| **Real facturado (10.936,72 €)** | `realForBlock` = suma de `data.days[].revenue` (o de los días con pred+real). Nunca `comparativas.actual.revenue` cuando hay `data.days` en semana actual. | Igual que la suma de la tabla "Por días" y que Facturación total (los mismos días). |
| **Nota "-9,1%"** | `data.ajusteFacturacionManualPct` (configurable). Solo informativa en el párrafo; el importe mostrado es el real (sin reducir en pantalla). | Configurable en Configuración. |
| **Diferencia vs predicción (+X%)** | `(realForBlock - predHastaHoy) / predHastaHoy * 100`. | Mismo `realForBlock` y misma predicción acumulada (mismos días). |
| **% vs semana anterior** | `(realForBlock - data.prevWeekRevenue) / data.prevWeekRevenue * 100`. `prevWeekRevenue` = suma de los mismos N días de la semana anterior. | Mismo `realForBlock` y mismos N días. |
| **Por días: Lunes X €, …** | `daysWithBoth`: días con predicción y real; importes por día (ajustados con `factorManual` si aplica para la comparativa por día). | Solo días que tienen pred y real; total coherente con `realForBlock` cuando hay coincidencia. |
| **Predicción completa (15.571,02 €)** | `comparativas.baseRevenue`. | Referencia semanal completa; el párrafo deja claro "con datos actuales (parciales)". |

---

## Diferencia 39,4% (KPI) vs 53,3% (párrafo)

- **KPI "Facturación total":** puede usar **real ajustado** (`realAdjustedForComparison`) para el % vs sem. ant. cuando hay ajuste de facturación manual.
- **Párrafo "Predicción vs realidad":** usa **real sin ajustar** (`realForBlock` = suma de `data.days`) para el % vs sem. ant.

Si se desea que ambos den el mismo porcentaje, hay que unificar criterio (por ejemplo usar en ambos el mismo real: o siempre el ajustado para comparativas, o siempre el real facturado mostrado). Con el backend corregido, al menos el párrafo y la tabla usan el mismo conjunto de días y la misma suma.

---

## Comprobaciones

- Backend: si no hay ningún día con `TotalRevenue > 0` en la semana, se usa `Date <= effectiveAsOf` para no dejar la semana vacía cuando hay datos con 0.
- Frontend: `realForBlock` en semana actual con `data.days` no usa nunca `comparativas.actual.revenue`.
- Versión de script en `index.html`: `dashboard.js?v=51` (o superior) para evitar caché.

---

## Conclusión

- **Causa raíz:** "Hasta hoy" se interpretaba como fecha de hoy en calendario, incluyendo días sin facturación (ej. sábado con fila en BD).
- **Solución:** "Hasta hoy" = hasta el **último día con facturación (TotalRevenue > 0)** en el backend; la lista de días y todos los totales usan ese mismo conjunto.
- Con esto, el bloque "Predicción vs realidad" muestra el **real facturado** como suma de los mismos días que la tabla y los totales (ej. 10.936,72 € para L–V), y los porcentajes se calculan de forma coherente.
