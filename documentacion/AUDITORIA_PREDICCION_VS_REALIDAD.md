# Auditoría: Bloque "Predicción vs realidad"

## Cambios aplicados (solución)

### 1. Backend – DashboardController.cs
- **Antes:** `dayItems` se construía con `days` (todos los ExecutionDay de la semana en BD), por lo que el front podía recibir 6 o 7 días y sumarlos → 12.031,59 €.
- **Ahora:** `dayItems` se construye con **`daysInRange`** (solo días con `Date <= effectiveAsOf`). La respuesta envía los mismos días que se usan para totalRevenue y prevWeekRevenue.
- **Efecto:** `data.days` tiene solo los días "hasta hoy" (ej. 5) y la suma coincide con el total mostrado.

### 2. Frontend – dashboard.js
- **Safeguard en prioridad 2:** Al calcular `sumFromDays` se limita la suma a `maxDaysToSum = Math.min(data.days.length, data.daysIncludedCount)` cuando `data.daysIncludedCount` está definido, para no sumar más días de los incluidos en "hasta hoy".
- **Efecto:** Aunque en el futuro el backend enviara más días, el "real facturado" del bloque seguiría basado en los mismos N días que el resto de totales.

### 3. Versión de script
- `index.html`: `dashboard.js?v=51` para forzar recarga en el navegador.

---

## Re-auditoría: origen de cada valor del párrafo

| Valor en el párrafo | Origen / Cálculo | Comprobación |
|---------------------|------------------|--------------|
| **11.122,16 €** (predicción acumulada) | `predHastaHoy`: suma de `predByDate[dStr]` para cada día en `data.days`; si falta, `(comparativas.baseRevenue * numDaysWithData) / 7`. | Coherente con mismos días que datos reales. |
| **Real facturado (10.936,72 €)** | `realForBlock` → Prioridad 1: `realSumDisplayed` (suma de `dayObj.revenue` de días con pred+real). Prioridad 2: `sumFromDays` (suma de `data.days[0..maxDaysToSum-1].revenue`). Prioridad 3/4: solo si no hay días. | Backend envía solo `daysInRange` → `data.days` = 5 días → suma = 10.936,72 €. |
| **-9,1%** (nota ajuste) | `ajustePct` desde `data.ajusteFacturacionManualPct` (configurable; default 9.1). Texto: `"ajustado -" + ajustePct.toFixed(1).replace('.', ',') + "%..."`. | Configurable en Configuración. |
| **+X%** (diferencia vs predicción) | `diffPct = (realForBlock - baseForDiff) / baseForDiff * 100` con `baseForDiff = predHastaHoy`. | Calculado con el mismo `realForBlock` (10.936,72 €). |
| **X% por encima/debajo semana anterior** | `pctVsSemAntHoy = (realForBlock - data.prevWeekRevenue) / data.prevWeekRevenue * 100`. `data.prevWeekRevenue` = mismo número de días que la semana actual (backend). | Mismo `realForBlock` y mismos días que el backend. |
| **Por días: Lunes X €, Martes Y €...** | `daysWithBoth`: días con `predDay` y `realDay`; `x.real` = `realDayForParagraph` (ajustado con `factorManual` si manual). | Solo días con predicción y real; importes por día alineados con ajuste. |
| **Predicción completa de la semana** | `predVal` = `comparativas.baseRevenue` (predicción semanal total). | Solo informativo. |

---

## Flujo de datos verificado

1. **GET /api/dashboard/week?weekStart=...&asOf=...**
   - `daysInRange` = días con `Date <= effectiveAsOf`.
   - `Days` = DTOs de `daysInRange` (mismo conjunto).
   - `TotalRevenue`, `PrevWeekRevenue`, `DaysIncludedCount` se calculan sobre `daysInRange`.

2. **Frontend**
   - `data.days` = lista enviada por el backend (solo días incluidos).
   - `realSumDisplayed` = suma de real de días que tienen pred+real (mismos días que "Por días").
   - `sumFromDays` = suma de hasta `data.daysIncludedCount` días de `data.days`.
   - `realForBlock` = realSumDisplayed o sumFromDays; no se usa `comparativas.actual.revenue` cuando hay `data.days` en semana actual.

3. **GET /api/estimaciones/comparativas?weekStart=...&asOf=...**
   - Filtra por `Date <= asOfDate`; mismo criterio "hasta hoy". El front ya no usa `comparativas.actual.revenue` para el bloque cuando hay `data.days`.

---

## Posibles mejoras futuras (no bloqueantes)

- **Predicciones por día:** Si `predByDate` no tiene datos (API o formato), `daysWithBoth` queda vacío y se usa prioridad 2 (`sumFromDays`). Con el backend enviando solo `daysInRange`, prioridad 2 ya da el total correcto. Revisar que `/api/predictions/by-week` devuelva `dailyPredictionsJson` con fechas en formato `yyyy-MM-dd` para maximizar días en "Por días".
- **Consistencia de fechas:** El front usa `todayYmd` del momento de carga; si el usuario deja la pestaña abierta y cambia el día, no se actualiza hasta "Actualizar". Aceptable para el caso de uso actual.

---

## Conclusión

Con los cambios aplicados:
- El "real facturado" del bloque es la suma de los días mostrados (10.936,72 € para L–V).
- Los porcentajes (diferencia vs predicción y vs semana anterior) se calculan con ese mismo valor.
- La lista de días del backend y la tabla "Días de la semana" están alineadas con los totales.
