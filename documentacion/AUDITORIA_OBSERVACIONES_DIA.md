# Auditoría: generación de los párrafos de Observaciones del Día

Objetivo: comprobar si los párrafos que generamos reflejan la realidad de los datos y si hay incoherencias, redundancias o fuentes de error.

---

## 1. Flujo de datos (resumen)

```
ExecutionDay (DB) + ShiftFeedbacks (Q1–Q5)
    → DashboardController: effectiveHours, effectiveProductivity, dayAvg (media histórica por día semana), pctVsAvg, trendLabel
    → DayScoringHelper.ComputeDayResumen → daySgt, dayEstado, dayResumenDiario
    → DayScoringHelper.BuildDayConclusion → dayConclusion (texto narrativo)
    → DashboardDayItemDto (API)
    → Frontend buildDayObservationsParagraph(d)
        → Apertura (facturación + productividad + "Día X")
        → dayConclusion (backend)
        → conclusionExtra: frase productividad, frase facturación vs media, frase tendencia
```

---

## 2. Fuentes de verdad por frase

| Lo que se muestra | Origen | ¿Refleja la realidad? |
|-------------------|--------|------------------------|
| "El Lunes se facturaron X €" | `d.revenue` (TotalRevenue del día) | ✅ Correcto |
| "la productividad del día fue de X €/h" | `d.effectiveProductivity` (Revenue/EffectiveHours; horas = Excel > PDF > calculadas) | ✅ Correcto |
| "Día productivo" (en apertura) | `d.dayEstado` = `GetDayEstadoFromAvgSgt(avgSgt)` en backend | ⚠️ Ver §3 |
| "Fue un día productivo, estable en todos los turnos, con flujo controlado..." | `d.dayConclusion` = `BuildDayConclusion(shifts)` | ⚠️ Ver §3 y §5 |
| "La productividad del día se situó en X €/h, en línea con lo habitual" | Umbrales fijos en frontend: &gt;55 = buena, &lt;45 = baja, resto = en línea | ⚠️ Ver §4 |
| "La facturación estuvo ligeramente por encima de la media de los lunes..." | `d.pctVsAvgHistoric`, `d.dayName`; media = promedio de ese día de la semana en últimas 12 semanas | ✅ Correcto |
| "Los lunes llevan varias semanas al alza" | `d.trendLabel` (mitad reciente vs antigua de ese día en 12 semanas) | ✅ Correcto; solo se muestra si el texto contiene "alza/subida/ascendente" |

---

## 3. Inconsistencia: estado del día vs estado por turno

- **Por turno** (`FeedbackScoring.GetEstadoFromSgt`): SGT 6–10 → Infrautilizado; 11–14 → Tranquilo; 15–18 → Equilibrado; 19–22 → Productivo; 23–26 → Exigente; 27–31 → Crítico.
- **Por día** (`DayScoringHelper.GetDayEstadoFromAvgSgt`): promedio SGT &lt; 11 → Tranquilo; &lt; 15 → Equilibrado; &lt; 19 → Productivo; &lt; 23 → Exigente; ≥ 23 → Crítico.

Efecto: el **nombre del estado del día** no coincide con el estado que tendría un turno con ese mismo SGT. Por ejemplo, si el promedio SGT del día es 15 (turnos “Equilibrado”), el día se etiqueta **“Productivo”**. Así, “Día productivo” y “Fue un día productivo” pueden referirse a un día cuyos turnos fueron en promedio “Equilibrado”, no “Productivo” a nivel turno. No es un error de cálculo, pero puede confundir: el estado del día está “subido” un escalón respecto al estado por turno.

Recomendación: documentar esta diferencia (día = agregado, un escalón por encima) o unificar criterios si se quiere que “productivo” signifique lo mismo en turno y día.

---

## 4. Productividad: umbrales fijos vs realidad

- En **observaciones del día** se usa:
  - &gt; 55 €/h → “en el buen nivel habitual”
  - &lt; 45 €/h → “algo baja respecto a lo habitual”
  - Entre 45 y 55 → “en línea con lo habitual”
- En el **resumen de la semana** (mismo dashboard) se usa:
  - &gt; 80 → semana buena
  - &gt; 50 → semana normal
  - ≤ 50 → semana baja

Problemas:

1. **Umbrales distintos**: 45/55 para el día vs 50/80 para la semana. Un día a 52 €/h se dice “en línea”; una semana a 52 €/h se dice “normal”. Coherente en idea pero los números no están alineados (55 vs 50 como “límite bueno”).
2. **“Lo habitual” sin historia**: “en línea con lo habitual” no se basa en la productividad histórica de ese día de la semana, sino en bandas fijas (45–55). No podemos decir “por encima de la media de los lunes” en productividad porque no calculamos media de productividad por día de la semana.

Recomendación: alinear umbrales día/semana donde tenga sentido (p. ej. 50 como frontera “normal”) y, si se quiere mayor precisión, calcular productividad media por día de la semana en backend y usar “por encima/debajo de la media de los lunes” también para productividad.

---

## 5. Redundancias en el párrafo

1. **Productividad dos veces**: en la apertura (“la productividad del día fue de 51,5 €/h”) y en el bloque enriquecido (“La productividad del día se situó en 51,5 €/h, en línea con lo habitual”). Se repite el mismo dato con distinta redacción.
2. **Estado del día dos veces**: “Día productivo.” en la apertura y “Fue un día productivo, …” en `dayConclusion`. Mismo concepto repetido.

Recomendación: elegir un solo lugar para la productividad (por ejemplo solo en el bloque enriquecido, con valor + juicio) y para el estado del día (solo en la conclusión narrativa o solo en la apertura), para que el texto no suene repetitivo.

---

## 6. Estabilidad y “variación entre turnos” con un solo turno

En `BuildDayConclusion`:

- “Estable en todos los turnos” se da cuando `distinctCount <= 1 && validEstados.Count >= 2` (mismo estado en al menos 2 turnos).
- Si solo hay **un turno** con feedback, `validEstados.Count == 1`, no se cumple `>= 2`, y se usa la rama de “variación entre turnos” (“con variación entre turnos pero controlada” o “con variación entre turnos”).

En ese caso no hay variación real; solo hay un dato. El mensaje puede inducir a pensar que hubo varios turnos con distintos estados.

Recomendación: si solo hay un turno con feedback, usar un texto distinto, por ejemplo “Solo hay feedback de un turno” o no afirmar estabilidad ni variación.

---

## 7. Tendencia: solo “al alza”

La frase “Los lunes llevan varias semanas al alza” solo se muestra cuando `trendLabel` contiene “alza”, “subida” o “ascendente”. Si la tendencia es “A la baja” o “Estable”, no se añade ninguna frase. La realidad (baja o estable) no se refleja en el párrafo.

Recomendación: incluir frases para “a la baja” y “estable” (o no mostrar tendencia si no se quiere comentar las tres), para que el párrafo no solo hable de subidas.

---

## 8. Comparación con la media: solo facturación

`pctVsAvgHistoric` se calcula como `(Revenue - media_revenue_día_semana) / media_revenue_día_semana * 100` sobre las últimas 12 semanas antes de la semana seleccionada. Eso está bien y la frase de facturación (“por encima/debajo de la media de los lunes”) refleja la realidad. No se compara productividad ni horas con ninguna media histórica por día de la semana.

---

## 9. Conclusión: ¿reflejan la realidad?

| Aspecto | ¿Refleja la realidad? | Notas |
|---------|------------------------|-------|
| Facturación del día | ✅ Sí | TotalRevenue del día |
| Productividad (valor €/h) | ✅ Sí | Revenue/EffectiveHours correctos |
| Facturación vs media del día de la semana | ✅ Sí | Media histórica por día de la semana, 12 semanas |
| Estado del día (Día X / Fue un día X) | ⚠️ Parcial | Coherente con el SGT promedio, pero el “nombre” del estado del día no coincide con el del turno (día un escalón por encima) |
| Estable / variación entre turnos | ✅ Sí, salvo 1 turno | Con un solo turno se dice “variación” sin haberla |
| Flujo / momentos críticos | ✅ Sí | Basado en si algún turno es Exigente/Crítico |
| Productividad “en línea / buena / baja” | ⚠️ Parcial | Basado en 45/55, no en media histórica de productividad por día |
| Tendencia | ⚠️ Parcial | Solo se menciona cuando hay “al alza” |

En conjunto, los párrafos **sí reflejan la realidad** en facturación, productividad (valor), comparación de facturación con la media y en la parte operativa (estable/variación, flujo). Las desviaciones son: nomenclatura día vs turno, umbrales fijos de productividad, redundancias, mensaje de “variación” con un solo turno y tendencia solo “al alza”. Ajustando esos puntos, el texto sería más preciso y alineado con los datos.
