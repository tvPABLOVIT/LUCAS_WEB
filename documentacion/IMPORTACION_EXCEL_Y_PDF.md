# Importación Excel

## Excel — Facturación y horas reales por turno

Cada semana puedes cargar un Excel con la facturación y las horas reales por turno. El Dashboard incluye un botón **Cargar Excel** que envía el archivo a `POST /api/import/excel`.

**Plantillas de ejemplo:** Los archivos **s1_2026.xlsx**, **s2_2026.xlsx**, **s3_2026.xlsx**, etc. (y s45_2025, s46_2025…) son la plantilla que usamos. El nombre indica la semana y el año: `s{N}_{año}` (ej. s1_2026 = semana 1 de 2026). El programa detecta este formato y lee la hoja según la estructura descrita en **C)**.

### Formatos soportados

**A) Una fila por turno** (columnas):

- **Fecha** (o Date): fecha del día (yyyy-MM-dd o dd/MM/yyyy).
- **Turno** (o Shift): `Mediodia`, `Tarde` o `Noche` (también LUNCH, MERIENDA, DINNER).
- **Facturacion** (o Revenue, Facturación): facturación del turno en €.
- **Horas** (o HoursWorked, Horas reales): horas trabajadas del turno.

**B) Una fila por día** (columnas):

- **Fecha** (o Date).
- Por cada turno: **Mediodia_Fact**, **Mediodia_Horas**, **Tarde_Fact**, **Tarde_Horas**, **Noche_Fact**, **Noche_Horas** (o variantes).

**C) Plantilla sN_YYYY** (s1_2026, s2_2026, etc.):

- Una fila con los días de la semana: **LUNES**, **MARTES**, **MIÉRCOLES**, **JUEVES**, **VIERNES**, **SÁBADO**, **DOMINGO** (columnas C–I).
- La fila siguiente tiene las fechas (serial Excel o fecha) para cada día (C–I).
- Después, bloques de dos filas por turno:
  - Columna A con el nombre del turno: **LUNCH** (Mediodía), **MERIENDA** (Tarde), **DINNER** (Noche).
  - Primera fila: facturación por día (columnas C–I = Lunes–Domingo).
  - Segunda fila: horas por día (columnas C–I).
- Solo se importa el primer bloque de cada turno (LUNCH, MERIENDA, DINNER).

El programa crea o actualiza los días de ejecución y los turnos (ShiftFeedbacks) con Revenue y HoursWorked. La facturación total del día y las horas totales se recalculan automáticamente.

---

## Personal y reparto de horas (informes / exportación)

- **Personal por turno:** En cada día/turno se guardan **personal sala** y **personal cocina** (0–3). Las **horas de equipo** del día se calculan como (Sala + Cocina) × horas por turno (configuración, p. ej. 4 h).
- **Horas reales:** Vienen del Excel o se introducen en Registro. La API reparte esas horas entre sala y cocina de forma proporcional al personal: si en un turno hay 2 en sala y 1 en cocina, de 12 h reales salen **8 h sala** y **4 h cocina**.
- **Uso en informes/exportación:** Al obtener un día de ejecución (`GET /api/execution/day?date=...`), cada turno incluye `hoursSalaEstimated` y `hoursCocinaEstimated`. Puedes usarlos en informes, exportación a Excel/Google Sheets o cualquier vista que necesite horas por área (sala vs cocina).

---

## Resumen de endpoints

| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | `/api/import/excel?weekStart=...` | Subir Excel (facturación + horas reales). `weekStart` opcional. |

Requiere autenticación (Bearer token) y el archivo en el cuerpo con nombre de campo `file`.
