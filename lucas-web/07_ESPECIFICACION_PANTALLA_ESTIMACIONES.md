# Lucas Web — Especificación pantalla Estimaciones

**Versión:** 1.0  
**Fecha:** Febrero 2026

---

## Objetivo

Mostrar la predicción de la semana siguiente (facturación por día, min/max, personal sugerido sala/cocina por turno), KPIs semanales, resumen y alertas generadas por tendencias y patrones.

---

## Bloques de la pantalla

1. **Cabecera:** Selector de semana (◀ ▶), rango de fechas, número de semana.
2. **4 tarjetas KPI:** Facturación, Productividad, Horas, Costo de personal (o "—" si no configurado).
3. **Resumen de la semana:** Clasificación (ej. "🟢 Semana buena") y texto explicativo.
4. **Semana siguiente:** 7 tarjetas de días (Lun–Jue arriba, Vie–Dom abajo) con:
   - Predicción por día (min, max, etiqueta de confianza).
   - Por turno: predicción de facturación.
   - Esquema personal sala/cocina (ej. "2-2-2" sala, "1-2-1" cocina).
   - Clima previsto y festivos si hay configuración.
5. **Contexto general / Alertas:** Recomendaciones y patrones que afectan la semana.

---

## APIs

| Acción | API |
|--------|-----|
| KPIs y resumen semanal | GET `/api/dashboard/week?weekStart=yyyy-MM-dd` |
| Estimaciones (cache app Windows) | GET `/api/estimaciones` |
| Predicción semana siguiente | GET `/api/predictions/next-week` |
| Recomendaciones/alertas | GET `/api/recommendations` |
| Versión (refresco) | GET `/api/recommendations/version` |

---

## Refresco

- Consultar `/api/recommendations/version` periódicamente (ej. cada 60 s).
- Si `version` cambia respecto al valor anterior, recargar `/api/estimaciones`, `/api/dashboard/week`, `/api/predictions/next-week`, `/api/recommendations`.
- El cache de estimaciones lo publica la app Windows al cargar la pestaña Estimaciones; si solo hay web, los datos vendrán de `/api/dashboard/week` y `/api/predictions/next-week` (la predicción puede estar vacía si la app Windows no ha corrido el análisis).

---

## Reglas de personal sugerido

- Si el máximo del rango del día ≥ 2.400 €: al menos 2 sala y 2 cocina por turno.
- 3ª en cocina solo si día > 3.000 €; 3ª en sala solo si día ≥ 3.500 €.
- Si en un turno se estima facturar > 600 €: al menos 2 camareros (sala) en ese turno.
- Ver `docs/GUIA_MEJORAS_DASHBOARD_ESTIMACIONES_PATRONES.md` sección 4.2.
