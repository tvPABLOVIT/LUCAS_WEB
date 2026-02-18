# Mejoras del bloque "Qué puede afectar la semana siguiente"

Propuestas de **nueva información** que puede afectar a la semana siguiente, agrupadas por viabilidad y valor.

---

## Ya implementadas en esta iteración

| Alerta | Descripción |
|--------|-------------|
| **Temperatura extrema** | Cuando hay 2+ días con máx > 30 °C o mín < 5 °C: "Ola de calor/frío prevista (X días)." Ayuda a preparar terraza, climatización, demanda. |
| **Misma semana, año anterior** | Si hay datos de las mismas fechas del año pasado: "Hace un año (misma semana): facturaste X € (Y% más/menos que la estimación actual)." Estacionalidad interanual. |
| **Días fuertes y flojos** | Resumen a partir de la predicción diaria: "Días con mayor estimación: Sábado, Viernes. Días con menor estimación: Lunes, Martes." Útil para planificar personal y turnos. |

---

## Propuestas adicionales (futuras)

### Con datos que ya tenemos o es fácil obtener

| Idea | Descripción | Dificultad |
|------|-------------|------------|
| **Concentración en fin de semana** | "% de la facturación estimada en viernes–domingo (ej. 65%)." Útil para saber si reforzar personal en finde. | Baja: se calcula desde dailyPredictionsJson. |
| **Coste personal vs estimación** | Si el % de coste personal sobre facturación estimada es muy alto/bajo: "Coste personal estimado: X% de la facturación (por encima/por debajo del histórico)." | Baja: ya está en dashboard/week; se puede reenviar en alertas. |
| **Calidad de la predicción** | "Predicción basada en N semanas con datos." o "Poca historia reciente: revisa expectativas." Cuando hay pocas semanas completas. | Media: NextWeekPredictionService tendría que exponer número de semanas usadas. |
| **Patrones aplicados** | Si hay patrones guardados (lluvia, festivos, temp): "Según patrones: los días de lluvia suelen facturar un X% menos." Solo cuando hay patrones con confianza. | Media: leer DetectedPatterns y resumir en una frase. |

### Con nuevas fuentes o configuración

| Idea | Descripción | Dificultad |
|------|-------------|------------|
| **Eventos deportivos / grandes eventos** | Partidos, conciertos, ferias que afecten la zona. Requiere API o tabla de eventos por fecha. | Alta: fuente externa o mantenimiento manual. |
| **Vacaciones escolares** | "Semana de vacaciones en [comunidad]." Afecta demanda familiar. Requiere calendario escolar (p. ej. por comunidad). | Alta: fuente externa. |
| **Cambio de horario** | Cuando aplique cambio de hora (primavera/otoño): una línea de contexto. | Baja: lógica por fechas. |
| **Open Data BCN / GuiaBCN** | Completar eventos y obras con APIs externas (ya documentado; actualmente solo BD para eventos). | Media: integración ya diseñada. |

### Mejoras de presentación (sin nueva lógica)

| Idea | Descripción |
|------|-------------|
| **Orden por impacto** | Ordenar alertas por "impacto" estimado (ej. tendencia y clima primero) o permitir al usuario elegir orden. |
| **Expandir/colapsar** | Alertas largas (ej. eventos) colapsables con "Ver más". |
| **Tooltip o ayuda** | En cada tipo de alerta, un "?" con breve explicación: "Por qué importa: ..." |

---

## Resumen

- **Implementadas ahora:** temperatura extrema, misma semana año anterior, días fuertes/flojos.
- **Siguiente paso recomendado:** concentración en fin de semana y/o coste personal en el bloque (poco esfuerzo, buen valor).
- **A medio plazo:** calidad de la predicción (N semanas) y resumen de patrones aplicados.
- **A largo plazo:** eventos externos, vacaciones escolares, integración Open Data BCN/GuiaBCN.
