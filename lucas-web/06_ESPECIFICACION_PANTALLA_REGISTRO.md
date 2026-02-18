# Lucas Web — Especificación pantalla Registro

**Versión:** 1.0  
**Fecha:** Febrero 2026

---

## Objetivo

Permitir ver y editar un día de ejecución (facturación total, horas trabajadas, turnos Mediodía/Tarde/Noche con revenue, personal sala/cocina y 4 preguntas de feedback) desde la web.

---

## Campos

### Nivel día
| Campo | Tipo | Obligatorio | Origen |
|-------|------|-------------|--------|
| Fecha | date (yyyy-MM-dd) | Sí | Usuario |
| Facturación total | decimal | Sí (TotalRevenue > 0 para día completo) | Usuario |
| Horas trabajadas | decimal | Sí | Usuario |
| Notas | texto | No | Usuario |
| Personal total | int | Calculado (suma sala+cocina por turno) | No editable |

### Por turno (Mediodía, Tarde, Noche)
| Campo | Tipo | Obligatorio | Origen |
|-------|------|-------------|--------|
| Facturación turno | decimal | Sí (para día completo) | Usuario |
| Horas trabajadas | decimal | Sí | Usuario |
| Personal sala | int (0–3) | Sí | Usuario |
| Personal cocina | int (0–3) | Sí | Usuario |
| Q1 Volumen | enum (5 opciones) | No | Usuario |
| Q2 Ritmo | enum (5 opciones) | No | Usuario |
| Q3 Margen | enum (5 opciones) | No | Usuario |
| Q4 Dificultad | enum (5 opciones) | No | Usuario |

Opciones Q1–Q4: ver `docs/DOCUMENTACION_COMPLETA_PROGRAMA.md` sección 2.1.

---

## APIs

| Acción | API |
|--------|-----|
| Cargar día | GET `/api/execution/{date}` |
| Crear día | POST `/api/execution` (body: date, total_revenue, total_hours_worked, shifts, notes) |
| Actualizar día | PATCH `/api/execution/{date}` (body: total_revenue, total_hours_worked, shifts, notes) |

---

## Validaciones

- Fecha: formato yyyy-MM-dd.
- TotalRevenue > 0, TotalHoursWorked > 0 para día financieramente completo.
- Cada turno: Revenue > 0, HoursWorked > 0 si se considera completo.
- StaffFloor y StaffKitchen: 0–3 por turno.
- Si el día ya existe, usar PATCH; si no, POST (409 si se intenta POST sobre día existente).
